# Ejercicio 12, restricción: ejecución en paralelo y atomicidad por cuenta

**Issue:** #10

> *Las operaciones deben poder ejecutarse en paralelo sobre diferentes cuentas (considera concurrencia y estado mutable del Visitor). Además, el sistema debe garantizar que las transacciones sobre una misma cuenta sean atómicas y consistentes (¿cómo lo resuelves con el patrón elegido?).*

Se construye sobre el código de los incisos [b](inciso-b.md) y [c](inciso-c.md). Todo el cambio está en `ProcesadorTransacciones` (el cliente del patrón) y en un candado por cuenta: **ningún visitante cambió**.

## Respuesta corta

| Pregunta | Respuesta |
|---|---|
| ¿Cómo corren en paralelo las operaciones sobre cuentas distintas? | `EjecutarEnParalelo` reparte las transacciones con `Parallel.For`. Cada transacción solo bloquea sus propias cuentas, así que las de cuentas distintas no se esperan entre sí. |
| ¿Qué pasa con el estado mutable del visitante? | `EjecutarTransaccion` y `RevertirTransaccion` no tienen estado mutable y se pueden compartir. `AuditarTransaccion`, `CalcularIntereses` y `ResumirPorGrupo` acumulan resultados: se usa **una instancia por tarea** y los resultados se combinan al final. |
| ¿Cómo se garantiza que las transacciones sobre una misma cuenta sean atómicas y consistentes? | **Un candado por cuenta.** Lo toma el cliente del patrón, que envuelve las visitas, **en un orden fijo** (por número de cuenta) y no lo suelta hasta terminar, incluida la reversión si algo falla. |
| ¿Cómo se resuelve con el patrón elegido? | Visitor separa las reglas de negocio (los visitantes) de quién recorre las cuentas (el cliente). La concurrencia es responsabilidad de ese cliente: los visitantes siguen sin saber nada de hilos. |

## 1. Paralelismo sobre cuentas distintas

```csharp
public IReadOnlyList<ResultadoTransaccion> EjecutarEnParalelo(IReadOnlyList<Transaccion> transacciones)
{
    ResultadoTransaccion[] resultados = new ResultadoTransaccion[transacciones.Count];
    Parallel.For(0, transacciones.Count, i => resultados[i] = Ejecutar(transacciones[i]));
    return resultados;
}
```

- **Cada `Ejecutar` toma solo los candados de sus cuentas.** Dos transacciones sobre cuentas distintas no comparten ningún candado y corren a la vez. Dos que comparten una cuenta se ejecutan de una en una.
- **Los resultados vuelven en el orden de la lista,** aunque los hilos terminen en cualquier orden: cada tarea escribe en su posición del arreglo.
- **Las operaciones sobre un grupo (inciso c) también corren en paralelo.** `EjecutarEnCadaCuenta` crea una transacción por cuenta del grupo y las ejecuta con `EjecutarEnParalelo`. Como cada una toca una sola cuenta, ninguna espera a otra.

## 2. El estado mutable de los visitantes

GoF describe al visitante concreto como el que guarda el estado local del algoritmo, que a menudo acumula resultados durante el recorrido (Gamma et al., 1994, p. 369). Ese estado es el que no se puede compartir entre hilos:

| Visitante | Estado mutable | ¿Se puede compartir entre hilos? | Solución |
|---|---|---|---|
| `EjecutarTransaccion` | ninguno: solo guarda la transacción, que no cambia | sí | se crea uno por transacción de todos modos |
| `RevertirTransaccion` | ninguno | sí | ídem |
| `AuditarTransaccion` | la lista de registros | **no** | una instancia por tarea (`AuditarEnParalelo`) |
| `CalcularIntereses` | el diccionario de intereses | **no** | una instancia por recorrido |
| `ResumirPorGrupo` | los acumuladores por grupo | **no** | una instancia por recorrido |
| `RecolectarCuentas` | la lista de cuentas | **no** | `RecolectarCuentas.De` crea uno en cada llamada |

**Qué pasaría si se compartiera un `AuditarTransaccion` entre hilos:** dos hilos llamando a `List.Add` a la vez pueden perder registros, sobrescribirse o lanzar una excepción. Lo mismo pasa con `+=` en un acumulador: leer, sumar y escribir son tres pasos, y dos hilos pueden leer el mismo valor viejo.

**Solución elegida: una instancia por tarea, y combinar al final.**

```csharp
public IReadOnlyList<RegistroAuditoria> AuditarEnParalelo(IReadOnlyList<Transaccion> transacciones)
{
    IReadOnlyList<RegistroAuditoria>[] porTarea = new IReadOnlyList<RegistroAuditoria>[transacciones.Count];
    Parallel.For(0, transacciones.Count, i => porTarea[i] = Auditar(transacciones[i]));  // cada Auditar crea su visitante
    return porTarea.SelectMany(registros => registros).ToList();                         // se combinan al final, en orden
}
```

Se descartó usar colecciones concurrentes (`ConcurrentBag`) dentro del visitante por dos razones:

- **Perderían el orden de los registros,** y la auditoría lo necesita.
- **No resolverían lo importante.** El estado compartido que de verdad importa no está en los visitantes, sino en las **cuentas** (saldo, contadores e historial), y eso lo protege el candado del punto 3.

## 3. Atomicidad y consistencia en la misma cuenta

### Un candado por cuenta

Cada `Cuenta` tiene un objeto privado, `Candado`, y `ProcesadorTransacciones` lo toma con `Monitor` (lo que usa `lock` en C#) antes de pasar el visitante.

- **Es un objeto propio y no la cuenta misma,** para que ningún código de fuera pueda bloquearlo por accidente.
- **Todo lo que cambia la cuenta pasa con el candado tomado:** aplicar el movimiento, cobrar el cargo, sumar el contador y anotar el asiento. Ningún otro hilo ve la cuenta a medio cambiar.

### Dónde se toma: en el cliente que envuelve las visitas, no en `Aceptar`

Esta es la respuesta a «¿cómo lo resuelves con el patrón elegido?». Visitor ya separa dos responsabilidades: los visitantes tienen las reglas de cada tipo de cuenta, y el cliente decide qué cuentas visitar y con qué visitante. La atomicidad es una propiedad de la **transacción entera**, que puede tocar varias cuentas, así que el candado lo toma quien conoce todas esas cuentas: el cliente.

```csharp
public ResultadoTransaccion Ejecutar(Transaccion transaccion) =>
    ConCandados(transaccion, () => EjecutarConCandados(transaccion));   // candados alrededor de todas las visitas
```

Tomar el candado dentro de `Aceptar` de cada cuenta no alcanzaría, por tres razones:

1. **Una transferencia necesita las dos cuentas a la vez.** Con el candado en `Aceptar`, la cuenta de origen se soltaría después del débito y antes del crédito, y otro hilo podría ver el débito hecho y el crédito no. Si el crédito se rechaza, la reversión del débito correría sin protección.
2. **No se podría fijar el orden de los candados.** Cada `Aceptar` tomaría el suyo en el orden de los movimientos, que es justo lo que provoca interbloqueos (punto siguiente).
3. **Mezclaría responsabilidades.** Las cuentas (elementos) y los visitantes (reglas) no tienen por qué saber de hilos. Con el candado en el cliente, `EjecutarTransaccion` sigue suponiendo que tiene la cuenta para él solo, y eso es cierto.

### Orden fijo: sin interbloqueo

```csharp
Cuenta[] cuentas = [.. transaccion.Cuentas.OrderBy(c => c.Numero, StringComparer.Ordinal)];
// se toman en ese orden, se ejecuta la acción y se sueltan en orden inverso
```

**Qué pasaría sin el orden fijo:** dos transferencias cruzadas, A→B y B→A, al mismo tiempo. La primera toma A y espera B; la segunda toma B y espera A. Ninguna avanza nunca.

**Con el orden fijo:** las dos piden primero el candado de la cuenta de número menor. La que lo consigue toma también el otro, termina y suelta los dos; la otra espera y después sigue. Así se elimina la espera circular, la condición que hace falta para que haya interbloqueo. Por eso el número de cuenta es único.

### Si falla a mitad: todo o nada con `RevertirTransaccion`

Si el débito ya se aplicó y el crédito se rechaza, el procesador deshace el débito con el mismo `RevertirTransaccion` del inciso b, **todavía con los candados tomados**. Nadie llega a ver el estado intermedio: desde fuera, la transacción se aplicó entera o no se aplicó.

Por indicación de la revisión del inciso b, si deshacer también fallara, el procesador ya no tapa el error original: lanza una `AggregateException` con los dos errores.

### Lecturas consistentes

- **`Auditar` y `Revertir` también toman los candados de sus cuentas,** así que no leen ni deshacen una cuenta a medio cambiar.
- **El historial no se puede recorrer mientras otra transacción escribe en la cuenta.** El iterador del inciso c falla si la lista cambia durante el recorrido, en vez de devolver datos a medias. Para leerlo con transacciones en curso hay que hacerlo con el candado de la cuenta tomado, como hace la auditoría.

## 4. Límites y alternativas

- **`Monitor` es síncrono.** Los visitantes no esperan nada (solo hacen cuentas), así que alcanza. Si una regla necesitara `await`, por ejemplo para consultar un servicio externo, haría falta `SemaphoreSlim(1, 1)` con `WaitAsync`, porque un `Monitor` no se puede mantener a través de un `await`.
- **El candado es por cuenta, no global.** Una cuenta muy activa hace esperar solo a sus propias transacciones. Un candado global sería correcto, pero quitaría todo el paralelismo.

## Demostración

La sección 7 de la simulación lanza 1000 transacciones a la vez en cada caso. La salida no depende del orden en que corran los hilos:

```text
――― 7. Restricción: transacciones en paralelo ―――
  1000 depósitos y retiros de 10.00 a la vez sobre COR-101: 1000 aplicados, saldo 10000.00 (esperado 10000.00), 1000 asientos
  1000 transferencias cruzadas entre COR-101 y COR-102 a la vez: 1000 aplicadas, sin interbloqueo; suma de las dos cuentas 20000.00 (se conserva)
  Auditoría en paralelo de esas transferencias, un AuditarTransaccion por tarea: 2000 registros (2 por transferencia), 2000 conformes
```

`dotnet test` ejecuta **62 pruebas**, todas correctas: las 57 de los incisos b y c y 5 de concurrencia. Las ejecuté 5 veces seguidas sin ningún fallo.

| Prueba | Qué comprueba |
|---|---|
| `DepositosYRetirosEnParaleloSobreLaMismaCuenta_DanElSaldoEsperado` | 1000 depósitos y retiros a la vez sobre **la misma** cuenta: el saldo final es el esperado y no se pierde ningún asiento. |
| `TransferenciasCruzadasEnParalelo_TerminanSinInterbloqueoYConservanElDinero` | 1000 transferencias A→B y B→A a la vez terminan antes de 30 s (sin interbloqueo) y la suma de las dos cuentas no cambia. |
| `TransferenciasRechazadasEnParalelo_NoDejanRastroEnLaCuentaDeOrigen` | 500 transferencias que fallan a mitad, mezcladas con 500 depósitos: el origen solo refleja los depósitos (todo o nada bajo concurrencia). |
| `ReversionesEnParaleloSobreLaMismaCuenta_DejanElEstadoOriginal` | 1000 retiros y sus 1000 reversiones en paralelo devuelven el saldo, las comisiones y las autorizaciones originales. |
| `AuditoriasEnParalelo_ConUnVisitantePorTarea_NoPierdenRegistros` | 1000 auditorías en paralelo dan los 1000 registros, en orden. |

## Referencias

Gamma, E., Helm, R., Johnson, R., y Vlissides, J. (1994). *Design patterns: Elements of reusable object-oriented software*. Addison-Wesley.

> Los números de página del libro corresponden a la copia de la bibliografía de la asignatura (`Documentacion Patrones`), igual que en los incisos anteriores.
