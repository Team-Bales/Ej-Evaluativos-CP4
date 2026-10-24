# Ejercicio 12, inciso b): Implementación

**Issue:** #7

> *Implementa con 4 tipos de elementos (CuentaAhorro, CuentaCorriente, CuentaInversión, CuentaEmpresarial) y 3 operaciones (EjecutarTransacción, RevertirTransacción, AuditarTransacción). Cada operación debe comportarse de manera diferente según el tipo de cuenta.*

La implementación sigue el diseño del [inciso a](inciso-a.md): tiene las mismas clases y relaciones que sus dos diagramas de clases, el [del patrón Visitor](diagramas/inciso-a-visitor.png) y el [de transacciones y auditoría](diagramas/inciso-a-transacciones.png). El código está en [`codigo/`](codigo/).

## Cómo ejecutarlo

Requisito: [SDK de .NET 10](https://dotnet.microsoft.com/download). Desde `ejercicio-12-transacciones-bancarias/codigo/`:

```sh
dotnet run --project Banco.Procesamiento   # la simulación
dotnet test                                 # las pruebas unitarias (xUnit)
```

Hay que indicar `--project` porque la carpeta tiene dos proyectos (el programa y sus pruebas). Las fechas de la simulación son fijas, así que la salida es la misma en cada ejecución.

## Estructura del código

| Carpeta / fichero | Clase del diagrama | Rol |
|---|---|---|
| `Cuentas/ICuenta.cs` | `ICuenta` | Element |
| `Cuentas/Cuenta.cs` | `Cuenta` | Base común de las cuentas: número, titular, saldo e historial |
| `Cuentas/CuentaAhorro.cs`, `CuentaCorriente.cs`, `CuentaInversion.cs`, `CuentaEmpresarial.cs` | las 4 cuentas | ConcreteElement |
| `Cuentas/Asiento.cs` | `Asiento` | Lo que una transacción le hizo a una cuenta; lo que se guarda para revertir y auditar |
| `Visitantes/IVisitanteCuenta.cs` | `IVisitanteCuenta` | Visitor |
| `Visitantes/EjecutarTransaccion.cs`, `RevertirTransaccion.cs`, `AuditarTransaccion.cs` | las 3 operaciones | ConcreteVisitor |
| `Visitantes/CalcularIntereses.cs` | `CalcularIntereses` | ConcreteVisitor nuevo: demuestra la extensibilidad |
| `Visitantes/RegistroAuditoria.cs`, `EstadoTransaccion.cs`, `OperacionRechazadaException.cs` | ídem | Resultados de auditar y de rechazar |
| `Transacciones/Transaccion.cs` y sus 4 subclases | `Transaccion`, `Deposito`, `Retiro`, `Transferencia`, `PagoServicio` | Peticiones encapsuladas como objetos (al estilo Command) |
| `Transacciones/Movimiento.cs`, `Naturaleza.cs` | `Movimiento`, `Naturaleza` | Los débitos y créditos de cada transacción |
| `ProcesadorTransacciones.cs`, `ResultadoTransaccion.cs` | `ProcesadorTransacciones`, `ResultadoTransaccion` | Client: crea los visitantes y garantiza «todo o nada» |
| `Dinero.cs` | — | Clase auxiliar interna: redondeo y formato de importes (no aparece en los diagramas) |
| `Program.cs` | — | La simulación |
| `Banco.Procesamiento.Tests/` | — | Pruebas unitarias |

## Cada operación se comporta distinto según la cuenta

Las reglas de negocio son inventadas, pero explícitas: cada una está documentada en el `Visitar` que la aplica, y sus valores son parámetros del constructor de la cuenta.

| Cuenta | `EjecutarTransaccion` | `RevertirTransaccion` | `AuditarTransaccion` |
|---|---|---|---|
| **Ahorro** | Admite 3 débitos por mes (retiros, transferencias salientes y pagos), según la fecha de la transacción: el cuarto del mismo mes se rechaza y un mes nuevo empieza de cero. Sin sobregiro: nunca deja el saldo en negativo. | Devuelve el monto y, si era un débito del mes en curso, descuenta 1 de `RetirosDelMes`. | Conforme si la transacción (o su reversión) no dejó el saldo en negativo. |
| **Corriente** | Permite quedar en negativo hasta −500. Cada débito que deja el saldo en negativo cobra un cargo fijo de 5 y suma 1 a `VecesEnSobregiro`; el tope se comprueba con el cargo incluido. | Devuelve el monto y el cargo y, si el débito había dejado la cuenta en negativo, descuenta 1 de `VecesEnSobregiro`. | Comprueba que el cargo se cobrara solo si el débito dejó la cuenta en negativo, y que el saldo no bajara del tope. |
| **Inversión** | Los créditos tienen un mínimo de 100. Un débito antes de la fecha de vencimiento paga una penalización del 2 %. | Devuelve el monto y la penalización y la descuenta de `PenalizacionesCobradas`. | Comprueba la penalización según la fecha, el depósito mínimo y que el saldo no quedara negativo. |
| **Empresarial** | Cada débito paga una comisión del 0,5 %. Los débitos de más de 10 000 necesitan la autorización de alguien distinto de quien pide la transacción. | Devuelve el monto y la comisión y, si era un monto alto, descuenta 1 de `OperacionesAutorizadas`. | Comprueba la comisión, que los montos altos vengan autorizados por otra persona y que el saldo no quedara negativo; anota quién autorizó. |

En las cuatro cuentas se cumple además lo siguiente:

- **Revertir restaura exactamente el estado anterior**, saldo y contadores, porque aplica la operación inversa del `Asiento` que dejó la ejecución (inciso a). La única excepción es deliberada: si se revierte un débito de ahorro de un mes que ya terminó, el contador del mes en curso no cambia.
- **Auditar no modifica la cuenta.** Por cada cuenta genera un `RegistroAuditoria` con quién pidió la transacción (`Usuario`), qué se pidió (`Descripcion`), cuándo (`Fecha`) y el resultado (`Estado`: aplicada, revertida o no aplicada), además de si lo que la transacción le hizo a la cuenta cumple las reglas de su tipo.
- **La auditoría juzga la transacción, no el estado actual de la cuenta.** Revisa lo que dice el asiento: el cargo cobrado, la autorización y el saldo en que la transacción (o su reversión) dejó la cuenta. Una transacción correcta sigue siendo conforme aunque después otras cambien la cuenta.

### Ejemplo: `EjecutarTransaccion` en una cuenta corriente

```csharp
public void Visitar(CuentaCorriente cuenta)
{
    Movimiento movimiento = MovimientoPendiente(cuenta);
    if (movimiento.Naturaleza == Naturaleza.Credito)
    {
        Aplicar(cuenta, movimiento, cargo: 0m);
        return;
    }

    bool quedaEnNegativo = cuenta.Saldo - movimiento.Monto < 0m;
    decimal cargo = quedaEnNegativo ? cuenta.CargoPorSobregiro : 0m;
    ExigirSaldo(cuenta, movimiento.Monto + cargo, saldoMinimo: -cuenta.LimiteSobregiro,
                $"supera el sobregiro permitido de {Dinero.Formato(cuenta.LimiteSobregiro)}");
    Aplicar(cuenta, movimiento, cargo);
    if (quedaEnNegativo)
    {
        cuenta.VecesEnSobregiro++;
    }
}
```

El método recibe una `CuentaCorriente` y no una cuenta genérica: lo eligió el doble despacho. Por eso puede leer `LimiteSobregiro` y `CargoPorSobregiro` sin preguntar por el tipo ni hacer un cast. Ningún visitante usa `is`, `switch` ni casts por tipo de cuenta; se puede comprobar con:

```sh
grep -rnE "\bis\b|\bswitch\b|\bas\b" Banco.Procesamiento/Visitantes/   # no devuelve nada
```

Ningún `Visitar` es una copia de otro: cada uno aplica la regla de su cuenta. Las partes comunes (aplicar el asiento y exigir saldo) son métodos privados del visitante (`Aplicar`, `ExigirSaldo`).

## «Todo o nada» en una transacción

Una transferencia afecta a dos cuentas. `ProcesadorTransacciones` le pasa el visitante a cada una y, si una rechaza su movimiento, revierte las que ya lo aplicaron, en orden inverso, con `RevertirTransaccion`:

```csharp
public ResultadoTransaccion Ejecutar(Transaccion transaccion)
{
    EjecutarTransaccion ejecutar = new(transaccion);
    List<ICuenta> aplicadas = [];
    try
    {
        foreach (ICuenta cuenta in transaccion.Cuentas)
        {
            cuenta.Aceptar(ejecutar);
            aplicadas.Add(cuenta);
        }

        return ResultadoTransaccion.Aplicada(transaccion);
    }
    catch (OperacionRechazadaException rechazo)
    {
        Deshacer(transaccion, aplicadas);
        return ResultadoTransaccion.Rechazada(transaccion, rechazo.Message);
    }
    catch
    {
        Deshacer(transaccion, aplicadas);
        throw;
    }
}
```

- Las reglas comprueban todo **antes** de cambiar la cuenta: una cuenta que rechaza su movimiento queda intacta, y solo hay que deshacer las anteriores.
- Si la transacción falla por otra causa (por ejemplo, porque ya se había ejecutado en una de sus cuentas), también se deshace lo aplicado, y después se propaga el error.
- Los constructores de las transacciones garantizan que haya como mucho un movimiento por cuenta, y que los montos no tengan fracciones de centavo.
- La restricción del enunciado (issue #10) parte de este mecanismo para garantizar la atomicidad cuando las transacciones corren en paralelo.

## Extensibilidad: una operación nueva sin tocar las cuentas

`CalcularIntereses` es una cuarta operación. Para agregarla **solo se creó la clase `Visitantes/CalcularIntereses.cs`**: no se modificó ninguna cuenta, ninguna transacción ni ninguno de los otros visitantes. También se comporta distinto según la cuenta:

| Cuenta | Interés del mes |
|---|---|
| Ahorro | 3 % anual sobre el saldo |
| Corriente | No paga intereses; si está en sobregiro, cobra el 18 % anual sobre lo que debe |
| Inversión | 6 % anual sobre el saldo |
| Empresarial | 1 % anual, solo sobre lo que pase de 20 000 |

```csharp
CalcularIntereses intereses = new();
foreach (Cuenta cuenta in cuentas)
{
    cuenta.Aceptar(intereses);   // el mismo Aceptar de siempre: las cuentas no cambiaron
}
```

## Resultados de la simulación

`Program.cs` crea una cuenta de cada tipo y ejecuta 12 transacciones de los 4 tipos. Algunas están pensadas para que se rechacen, una por cada regla:

```text
――― 1. EjecutarTransaccion ―――
  Aplicada   Depósito de 250.00 en AHO-001
  Aplicada   Retiro de 100.00 de AHO-001
  Aplicada   Retiro de 50.00 de AHO-001
  Aplicada   Pago de Electricidad por 80.00 desde AHO-001
  Rechazada  Retiro de 20.00 de AHO-001
             motivo: AHO-001: alcanzó el límite de 3 retiros del mes
  Aplicada   Retiro de 600.00 de COR-001
  Rechazada  Retiro de 300.00 de COR-001
             motivo: COR-001: supera el sobregiro permitido de 500.00 (saldo -305.00, a debitar 305.00)
  Aplicada   Retiro de 1000.00 de INV-001
  Rechazada  Transferencia de 50.00 de COR-001 a INV-001
             motivo: INV-001: el depósito mínimo es 100.00
  Rechazada  Transferencia de 15000.00 de EMP-001 a INV-001
             motivo: EMP-001: los débitos de más de 10000.00 necesitan la autorización de otra persona
  Aplicada   Transferencia de 15000.00 de EMP-001 a INV-001
  Aplicada   Pago de Telefonía por 1200.00 desde EMP-001
```

- **8 aplicadas y 4 rechazadas.** Cada rechazo corresponde a una regla distinta: el límite de retiros, el tope de sobregiro, el depósito mínimo y la autorización.
- **Todo o nada.** La transferencia de 50 de COR-001 a INV-001 llegó a aplicar el débito en COR-001, con su cargo por sobregiro. Cuando INV-001 rechazó el crédito, el procesador revirtió ese débito, y COR-001 siguió en −305, con 1 vez en sobregiro.
- **Revertir** el retiro de 600 deja COR-001 en 300 y con 0 veces en sobregiro: el mismo estado que antes del retiro.
- **Auditar** genera 15 registros, uno por transacción y cuenta. Las cuentas a las que no llegó una transacción aparecen como `NoAplicada`. COR-001 aparece como `Revertida` en el retiro de 600 y en la transferencia rechazada, porque en las dos aplicó el débito y después se deshizo. Cada registro indica el saldo en que la transacción, o su reversión, dejó la cuenta.
- **Intereses del mes:** 2.55 (ahorro), 0.00 (corriente, ya sin sobregiro), 169.90 (inversión) y 11.43 (empresarial).

## Pruebas

`dotnet test` ejecuta **45 pruebas xUnit**, todas correctas. Para cada combinación de cuenta y operación (4 × 3) hay al menos una prueba:

| Cuenta | Ejecutar | Ejecutar y luego revertir | Auditar |
|---|---|---|---|
| Ahorro | retiro cuenta como débito del mes; depósito no; rechaza el cuarto débito del mes; un mes nuevo empieza de cero; rechaza sin saldo; permite dejar el saldo en cero | un retiro y un depósito dejan el estado original; revertir un retiro de un mes anterior no cambia el mes en curso | registra quién, qué, cuándo y resultado sin modificar la cuenta |
| Corriente | sin cargo si no queda en negativo; cargo al quedar en negativo; llega justo al tope; rechaza si pasa del tope | devuelve monto y cargo y descuenta el sobregiro | informa el sobregiro y el cargo sin modificar la cuenta; juzga la transacción y no el estado actual |
| Inversión | penalización antes del vencimiento; sin penalización desde el vencimiento; rechaza depósito menor que el mínimo; rechaza si la penalización deja sin saldo | devuelve monto y penalización | señala el retiro anticipado y su penalización; marca no conforme la reversión que deja la cuenta en negativo |
| Empresarial | cobra comisión en débitos; no en créditos; rechaza monto alto sin autorización; rechaza que alguien se autorice a sí mismo; acepta monto alto autorizado; un monto igual al umbral no necesita autorización | devuelve monto y comisión y descuenta la autorización | registra la comisión y quién autorizó sin modificar la cuenta |

Cada prueba de «ejecutar y luego revertir» compara el saldo **y** los contadores de la cuenta con los de antes de ejecutar. Además:

- **`ProcesadorTransaccionesTests`** (7 pruebas):
  - una transferencia aplica el débito y el crédito;
  - si el crédito se rechaza, se revierte el débito (todo o nada);
  - si la transacción falla por otra causa, se deshace lo aplicado y se propaga el error;
  - revertir no borra lo que otra transacción hizo después en la misma cuenta;
  - revertir o ejecutar dos veces la misma transacción falla sin tocar la cuenta;
  - un depósito ya gastado se revierte igual, y la auditoría lo marca como no conforme.
- **`TransaccionTests`** (3 pruebas): como mucho un movimiento por cuenta, ninguna transferencia a la misma cuenta y ningún monto con fracciones de centavo.
- **`DobleDespachoTests`**: a través de `ICuenta`, `Aceptar` llama al `Visitar` de la clase concreta.
- **`CalcularInteresesTests`**: la operación nueva aplica la regla de cada cuenta y no modifica nada.
