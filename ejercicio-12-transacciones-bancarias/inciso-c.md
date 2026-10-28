# Ejercicio 12, inciso c): Integración con Composite e Iterator

**Issue:** #8

> *Integra con Composite para manejar grupos de cuentas (ej. "Cuentas de una sucursal") y con Iterator para recorrer las transacciones de una cuenta. ¿Cómo afecta el Composite a la ejecución de operaciones sobre grupos?*

Se integra sobre el código del [inciso b](inciso-b.md), en [`codigo/`](codigo/). El grupo de cuentas entra por la interfaz `ICuenta` del [inciso a](inciso-a.md), que solo declara `Aceptar`.

## Composite: grupos de cuentas

Composite compone objetos en estructuras de árbol y permite que el cliente trate igual a un objeto individual y a una composición de objetos (Gamma et al., 1994, p. 183). Aquí el objeto individual es una cuenta y la composición es un grupo: una sucursal, una región o el banco entero.

| Rol (GoF) | Clase | Qué hace |
|---|---|---|
| Component | `ICuenta` | Lo que tienen en común una cuenta y un grupo: `Aceptar(IVisitanteCuenta)`. |
| Leaf | las 4 cuentas (a través de `Cuenta`) | Llaman al `Visitar` de su clase. |
| Composite | `GrupoCuentas` | Guarda sus hijos (cuentas u otros grupos) y en su `Aceptar` le pasa el visitante a cada uno. |
| Client | `ProcesadorTransacciones`, `Program.cs` | Le pasan un visitante a una cuenta o a un grupo sin distinguirlos. |

```csharp
public void Aceptar(IVisitanteCuenta visitante)
{
    visitante.EntrarEnGrupo(this);
    foreach (ICuenta hijo in _hijos)
    {
        hijo.Aceptar(visitante);   // si el hijo es un grupo, recorre a su vez los suyos
    }

    visitante.SalirDeGrupo(this);
}
```

- **El cliente trata igual a una cuenta y a un grupo.** `cuenta.Aceptar(v)` y `banco.Aceptar(v)` se escriben igual, y `EjecutarEnCadaCuenta(ICuenta destino, …)` acepta cualquiera de los dos. GoF lo presenta como la ventaja principal del patrón: el cliente no sabe, ni le importa, si tiene una hoja o un compuesto (p. 186).
- **Los grupos se anidan** (sucursal → región → banco), y el recorrido es **recursivo**: el `Aceptar` de un grupo llama al `Aceptar` de cada hijo. `Agregar` rechaza los ciclos (un grupo dentro de sí mismo o de uno de sus descendientes), porque el recorrido no terminaría.
- **Es el uso de Visitor que GoF señala** (p. 380): el elemento compuesto recorre sus hijos en su `Accept` (p. 373), y así el recorrido del árbol es responsabilidad de la estructura, no del visitante (p. 374).

### Cuánto costó agregar el Composite

`IVisitanteCuenta` ganó dos métodos, `EntrarEnGrupo` y `SalirDeGrupo`, con una **implementación vacía por omisión**. Los visitantes que no distinguen grupos (ejecutar, revertir, auditar y calcular intereses) heredan esa implementación y **no cambiaron**. GoF admite esta implementación por omisión como excepción (p. 371). El costo total fue:

- 1 clase nueva (`GrupoCuentas`);
- 1 interfaz con 2 métodos nuevos;
- 0 cuentas y 0 visitantes modificados.

El inciso d usa estos números.

## Iterator: las transacciones de una cuenta

Iterator permite recorrer los elementos de un agregado **sin exponer su representación interna** (Gamma et al., 1994, p. 289), y admitir varios recorridos distintos del mismo agregado (p. 291). En C# es `IEnumerable<T>`, como en las colecciones de .NET que muestra la conferencia (diap. 35–37).

| Rol (GoF) | Aquí |
|---|---|
| Aggregate | `Cuenta`, que guarda su historial de asientos en una lista privada |
| CreateIterator | `Historial()`, y también `GrupoCuentas.Hijos` para los hijos de un grupo |
| Iterator | el `IEnumerator<Asiento>` que genera el compilador a partir de `yield return` |

```csharp
public IEnumerable<Asiento> Historial()
{
    foreach (Asiento asiento in _asientos)
    {
        yield return asiento;
    }
}
```

- **No se expone la lista interna.** `Historial()` no devuelve la `List<Asiento>`: convertirla a `ICollection<Asiento>` para agregar o quitar asientos da `null` (hay una prueba).
- **Hay iteradores filtrados:** `Historial(desde, hasta)` filtra por fecha, y `Historial<Transferencia>()` por tipo de transacción.
- **Incluye los asientos revertidos**, marcados como tales: el historial es el rastro completo de la cuenta.
- **Si la cuenta cambia mientras se recorre, el recorrido falla** en vez de devolver datos a medias. Esto importa para la restricción (#10), donde varias transacciones corren en paralelo.
- **Interno y externo.** `Historial()` es un iterador *externo*: el cliente controla el bucle. El recorrido del árbol de grupos, en cambio, es *interno*: lo hace `Aceptar`. GoF explica que los iteradores externos son difíciles de implementar sobre estructuras recursivas como las de Composite, porque la posición abarca varios niveles (p. 295), y por eso el árbol lo recorre el propio Composite.

## ¿Cómo afecta el Composite a la ejecución de operaciones sobre grupos?

### 1. La operación llega a todas las hojas, en profundidad y en el orden de inserción

Una operación sobre un grupo se aplica a **todas las cuentas del árbol**, no a los grupos: los grupos solo reenvían el visitante. El orden es un recorrido en profundidad que respeta el orden en que se agregaron los hijos. Con el árbol de la simulación:

```text
Banco
├─ Región Occidente
│  ├─ Sucursal Centro:  AHO-001, COR-001
│  └─ Sucursal Playa:   INV-001, AHO-002
└─ Sucursal Empresas:   EMP-001
```

el orden es AHO-001, COR-001, INV-001, AHO-002 y EMP-001. El visitante recibe además `EntrarEnGrupo` y `SalirDeGrupo` alrededor de cada grupo; la prueba `Aceptar_RecorreLosGruposAnidadosEnProfundidadYEnOrden` fija esa secuencia completa.

### 2. Si falla en una cuenta del grupo: mejor esfuerzo con reporte

`ProcesadorTransacciones.EjecutarEnCadaCuenta(destino, crear)` aplica una operación a una cuenta o a un grupo. Funciona así:

1. Obtiene las cuentas del destino con `RecolectarCuentas`, un visitante que recorre el árbol.
2. Crea **una transacción por cuenta** y la ejecuta con el «todo o nada» del inciso b.
3. Si una cuenta rechaza la suya, **las demás siguen**, y el resultado de cada una (aplicada, o rechazada con su motivo) queda en el reporte.

**Por qué mejor esfuerzo y no todo o nada para el grupo:**

- **Cada cuenta es de un cliente distinto.** Un cobro de mantenimiento a una sucursal son N hechos independientes, no uno solo. Si un cliente no tiene fondos, no hay razón para dejar de cobrarles a los demás.
- **Con todo o nada, una sola cuenta bloquearía al banco entero.** En la simulación, 2 de 5 cuentas rechazan el cobro (una agotó sus débitos del mes y otra no tiene saldo). Con todo o nada no se habría cobrado a ninguna, y con miles de cuentas casi siempre habrá alguna que rechace.
- **No se pierde ni se oculta nada.** Cada transacción por cuenta sigue siendo atómica, una cuenta que rechaza queda intacta, y el reporte dice qué cuentas fallaron y por qué, para reintentarlas o avisar al cliente.

**Cuándo sí conviene todo o nada:** cuando la operación es **un solo hecho** que toca varias cuentas, como una transferencia o una nómina (un débito en la empresa y un crédito a cada empleado). Ahí no tiene sentido aplicar una parte. Eso se modela como **una** transacción con varios movimientos, y `ProcesadorTransacciones.Ejecutar` ya la aplica con todo o nada (inciso b).

| | Mejor esfuerzo (`EjecutarEnCadaCuenta`) | Todo o nada (`Ejecutar`) |
|---|---|---|
| Qué es | N transacciones independientes, una por cuenta | 1 transacción con N movimientos |
| Si una cuenta rechaza | las demás se aplican; el reporte la señala | se revierten las ya aplicadas |
| Ejemplos | cobro de mantenimiento, abono de intereses | transferencia, pago de nómina |

**Por qué no basta con `grupo.Aceptar(new EjecutarTransaccion(t))`:** `EjecutarTransaccion` aplica los movimientos de *una* transacción, que no tiene movimientos en las demás cuentas del grupo. Además, el procesador necesita el resultado de cada cuenta, para reportarlo o para revertirlo. Por eso primero obtiene las hojas con un visitante y después trabaja cuenta por cuenta.

### 3. Cómo un visitante acumula resultados al recorrer un grupo

`ResumirPorGrupo` calcula en **un solo recorrido** los totales de cada grupo: cuántas cuentas tiene, su saldo y los cargos cobrados.

- En `EntrarEnGrupo` abre un acumulador para el grupo, y en `SalirDeGrupo` lo cierra.
- Cada cuenta visitada **suma en todos los grupos abiertos**, es decir, en todos los que la contienen: una cuenta de la Sucursal Centro cuenta en su sucursal, en la Región Occidente y en el Banco.
- **Cada tipo de cuenta aporta sus cargos a su manera:**
  - ahorro no cobra;
  - corriente aporta el cargo por cada vez que quedó en sobregiro;
  - inversión, sus penalizaciones;
  - empresarial, sus comisiones.

  Es el doble despacho otra vez.

Es la consecuencia de Visitor que GoF llama acumular estado durante el recorrido (p. 372). Un visitante que no distingue grupos también acumula: `CalcularIntereses`, del inciso b, calcula los intereses de todas las cuentas del banco con `banco.Aceptar(...)`, **sin ningún cambio**.

## Resultados de la simulación

`Program.cs` agrega las secciones 5 y 6 al final de la simulación del inciso b, con una cuenta más (AHO-002, con 3.00 de saldo):

```text
――― 5. Composite: totales por grupo en un solo recorrido (ResumirPorGrupo) ―――
  Banco                  5 cuentas  saldo  69022.00  cargos cobrados 101.00
    Región Occidente     4 cuentas  saldo  35303.00  cargos cobrados  20.00
      Sucursal Centro    2 cuentas  saldo   1320.00  cargos cobrados   0.00
      Sucursal Playa     2 cuentas  saldo  33983.00  cargos cobrados  20.00
    Sucursal Empresas    1 cuenta   saldo  33719.00  cargos cobrados  81.00

  CalcularIntereses sobre el banco entero (mismo visitante, sin cambios): total 183.89

  Cobro de mantenimiento de 5.00 a todas las cuentas del banco (mejor esfuerzo):
  Rechazada  Pago de Mantenimiento por 5.00 desde AHO-001
             motivo: AHO-001: alcanzó el límite de 3 retiros del mes
  Aplicada   Pago de Mantenimiento por 5.00 desde COR-001
  Aplicada   Pago de Mantenimiento por 5.00 desde INV-001
  Rechazada  Pago de Mantenimiento por 5.00 desde AHO-002
             motivo: AHO-002: saldo insuficiente (saldo 3.00, a debitar 5.00)
  Aplicada   Pago de Mantenimiento por 5.00 desde EMP-001
  → 3 aplicadas y 2 rechazadas; las rechazadas no tocaron sus cuentas.

――― 6. Iterator: historial de COR-001 ―――
  Retiro de 600.00 de COR-001                    Debito  efecto  -605.00  (revertido)
  Transferencia de 50.00 de COR-001 a INV-001    Debito  efecto   -55.00  (revertido)
  Pago de Mantenimiento por 5.00 desde COR-001   Debito  efecto    -5.00

  Solo las transferencias de COR-001:
  Transferencia de 50.00 de COR-001 a INV-001
```

- **Los totales cuadran por niveles:** Centro (1320.00) más Playa (33983.00) dan la Región (35303.00), y la Región más Empresas (33719.00) dan el Banco (69022.00).
- **El cobro sigue la política del punto 2:** las 3 cuentas que pueden pagar pagan; AHO-001 (ya hizo 3 débitos este mes) y AHO-002 (sin saldo) quedan intactas y aparecen en el reporte con su motivo.
- **El historial de COR-001** muestra los dos débitos revertidos del inciso b y el cobro de mantenimiento; el filtro por tipo deja solo la transferencia.

## Pruebas

`dotnet test` ejecuta **57 pruebas**, todas correctas: las 46 del inciso b y 11 nuevas.

| Prueba | Qué comprueba |
|---|---|
| `Aceptar_RecorreLosGruposAnidadosEnProfundidadYEnOrden` | Un grupo anidado en tres niveles recorre todas sus hojas en profundidad y en orden, con los avisos de entrada y salida. |
| `Agregar_RechazaLosCiclos` | No se puede meter un grupo dentro de sí mismo ni dentro de un descendiente. |
| `Hijos_NoExponeLaListaInterna` | Los hijos se recorren sin poder modificar la lista. |
| `ResumirPorGrupo_SumaCadaCuentaEnTodosLosGruposQueLaContienen` | Totales de saldo y cargos correctos en cada nivel de un árbol anidado. |
| `UnVisitanteQueNoDistingueGrupos_FuncionaSobreUnGrupoSinCambios` | `CalcularIntereses` recorre un grupo entero sin ningún cambio. |
| `EjecutarEnCadaCuenta_ConFalloParcialSigueConLasDemasYReportaCadaResultado` | **Fallo parcial:** la cuenta que rechaza queda intacta, las demás se cobran y el reporte da cada resultado. |
| `EjecutarEnCadaCuenta_TrataIgualAUnaCuentaSola` | La misma operación funciona con una cuenta sola. |
| `Historial_RecorreLosAsientosEnOrdenIncluidosLosRevertidos` | El historial sale en orden, incluidos los asientos revertidos. |
| `Historial_NoExponeLaListaInterna` | El historial no se puede convertir en una colección modificable. |
| `Historial_FiltraPorFecha` | El filtro por fecha devuelve solo los asientos del intervalo. |
| `Historial_FiltraPorTipoDeTransaccion` | El filtro por tipo devuelve solo las transferencias. |

## Referencias

Gamma, E., Helm, R., Johnson, R., y Vlissides, J. (1994). *Design patterns: Elements of reusable object-oriented software*. Addison-Wesley.

Ingeniería de Software. *Marcos de Trabajo y Patrones de Diseño* [diapositivas de la conferencia].

> Los números de página del libro corresponden a la copia de la bibliografía de la asignatura (`Documentacion Patrones`), igual que en los incisos a y b.
