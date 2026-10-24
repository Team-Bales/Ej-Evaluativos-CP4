# Cómo leer los diagramas de clases del inciso a

El diseño está en dos figuras:

- **Figura 1, el patrón Visitor:** las cuentas, las operaciones y el cliente que las une.
- **Figura 2, transacciones y auditoría:** cómo se describe una transacción y qué devuelven la ejecución y la auditoría.

Algunas clases aparecen en las dos figuras. En la figura 2 se dibujan como una caja gris discontinua que solo tiene el nombre: su detalle está en la figura 1.

Las clases que tienen un rol en el patrón llevan en la esquina una etiqueta de color con ese rol (GoF).

## Figura 1: el patrón Visitor

![Figura 1: el patrón Visitor](inciso-a-visitor.png)

Se lee de arriba abajo, siguiendo lo que pasa al ejecutar una transacción.

1. **`ProcesadorTransacciones`** (`Client`, en verde) recibe la transacción. Tiene dos flechas punteadas:
   - **crea el visitante** de la operación: `EjecutarTransaccion`, `RevertirTransaccion` o `AuditarTransaccion`. La flecha llega a `IVisitanteCuenta`, la interfaz que los tres implementan;
   - **llama a `Aceptar(v)`** en cada cuenta de la transacción. Solo conoce la interfaz `ICuenta`.
2. **`ICuenta`** (`Element`) solo tiene `Aceptar`. La flecha punteada horizontal hacia **`IVisitanteCuenta`** (`Visitor`) es el doble despacho: `Aceptar(v)` llama a `v.Visitar(this)`, y el tipo de `this` elige cuál de los cuatro `Visitar` se ejecuta.
3. **Lado izquierdo, las cuentas.**
   - `Cuenta` (abstracta, en cursiva) implementa `ICuenta` y tiene lo común: número, titular, saldo y el historial de asientos.
   - Las cuatro cuentas (`ConcreteElement`) heredan de `Cuenta` y solo agregan sus parámetros y contadores. Las flechas de herencia forman un árbol que sube hasta `Cuenta`.
   - Su `Aceptar` es siempre la misma línea: `v.Visitar(this)`.
   - Los contadores (por ejemplo, `RetirosDelMes`) tienen visibilidad pública (`+`) porque se leen desde fuera, pero solo los visitantes los cambian: su `set` es interno.
4. **Lado derecho, las operaciones.** Los cuatro visitantes (`ConcreteVisitor`) implementan `IVisitanteCuenta`; sus flechas se juntan en una línea a la derecha que sube hasta la interfaz. Cada uno tiene los cuatro `Visitar` (`+Visitar(…) × 4`), uno por tipo de cuenta.
   - `EjecutarTransaccion`, `RevertirTransaccion` y `AuditarTransaccion` son las tres operaciones del enunciado. Su atributo `+Transaccion` es la transacción que procesan: la asociación con `Transaccion`, escrita como atributo.
   - **`CalcularIntereses`** (morado, borde discontinuo) es una operación nueva agregada en el inciso b. Para agregarla no se tocó ninguna cuenta: es la prueba del requisito de extensibilidad.
5. **En el centro, `Asiento`** (ámbar) es lo que una transacción le hizo a una cuenta: débito o crédito, monto, cargo cobrado y el saldo en que la dejó (y, si se revirtió, el saldo tras revertir).
   - **`Cuenta` ◆→ `Asiento`, 1 → 0..\***: cada cuenta es dueña de su historial (composición, rombo lleno).
   - `EjecutarTransaccion` **crea** el asiento, y `RevertirTransaccion` lo **deshace**: resta su efecto del saldo y lo marca como revertido.

## Figura 2: transacciones, auditoría y «todo o nada»

![Figura 2: transacciones, auditoría y «todo o nada»](inciso-a-transacciones.png)

### Zona 1: transacciones (ámbar)

- **`Transaccion`** (abstracta) es la petición encapsulada como objeto, al estilo Command. Guarda quién la pidió (`Usuario`), cuándo (`Fecha`) y sus movimientos.
- **`Transaccion` ◆→ `Movimiento`, 1 → 1..\***: toda transacción se describe con uno o más débitos o créditos, como mucho uno por cuenta.
- **`Movimiento` → `Cuenta`, 0..\* → 1**: cada movimiento afecta a una cuenta. **`Movimiento` → `Naturaleza`**: débito o crédito.
- Debajo, las **cuatro transacciones** del enunciado heredan de `Transaccion`. En la parte inferior de cada caja, en cursiva, están los movimientos que genera: por ejemplo, la transferencia genera un débito en el origen y un crédito en el destino.

### Zona 2: ejecución y auditoría (verde)

- `ProcesadorTransacciones` **devuelve** un `ResultadoTransaccion`: si la transacción se aplicó, o el motivo por el que se rechazó.
- `EjecutarTransaccion` **lanza** `OperacionRechazadaException` cuando una regla de la cuenta impide aplicar el movimiento. El procesador la **captura** y **deshace** con `RevertirTransaccion` las cuentas que ya lo habían aplicado: **todo o nada**.
- `AuditarTransaccion` **acumula** un `RegistroAuditoria` por cada cuenta visitada (1 → 0..\*). Las notas en gris a la derecha de los campos indican **quién, qué, cuándo y resultado**. `Estado` es uno de los tres valores de `EstadoTransaccion`.

## Leyenda de flechas

| Flecha | Significado | Ejemplo |
|---|---|---|
| Punteada con triángulo vacío | implementa una interfaz | `Cuenta` → `ICuenta` |
| Continua con triángulo vacío | hereda de una clase | `CuentaAhorro` → `Cuenta` |
| Continua con flecha abierta | asociación (lo tiene como atributo) | `Movimiento` → `Cuenta` |
| Punteada con flecha abierta | dependencia (lo crea, lo usa o lo llama) | `EjecutarTransaccion` → `Asiento` |
| Rombo lleno | composición (es dueño de la colección) | `Cuenta` → `Asiento` |

## Colores

| Color | Qué agrupa |
|---|---|
| Azul | clases con un rol en el patrón Visitor |
| Verde | el cliente, y lo que devuelven la ejecución y la auditoría |
| Ámbar | las transacciones y lo que dejan en cada cuenta |
| Morado | la operación nueva que demuestra la extensibilidad |
| Gris | enumeraciones |
| Gris discontinuo | clase detallada en la figura 1 |
