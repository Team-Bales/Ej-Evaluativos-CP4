# Restricción E9-R: entrega at-least-once y persistencia de notificaciones fallidas

> El sistema debe garantizar entrega *at-least-once* y persistir notificaciones fallidas.

La entrega asíncrona del inciso c aísla los fallos y reintenta, pero toda esa estructura vive **en memoria**: si el proceso se cae, lo que estaba en vuelo se pierde. La restricción pide dos cosas que se apoyan en la misma decisión de diseño: **escribir la notificación en un almacén durable antes de intentar enviarla** (patrón *outbox*) y **no borrar nada que no se haya confirmado**. Con eso, un aviso que falló sigue en el disco y un aviso a medias se recupera al reiniciar.

La implementación es real y está en el repositorio: repositorio persistente en archivo, estados durables, recuperación al arrancar y siete pruebas que lo comprueban.

## Respuesta corta

| Pregunta | Respuesta |
|---|---|
| ¿Cómo se garantiza *at-least-once*? | La notificación se escribe en el outbox **antes** de encolarse para el envío y solo se marca como `Entregada` cuando el canal confirma. Si el proceso se cae, lo pendiente sigue en el disco y otro proceso lo envía al arrancar. |
| ¿Se puede enviar dos veces? | Sí: *at-least-once* lo permite. Por eso la fila del outbox se identifica por el `Id` de la notificación y el registro es un *upsert*: reenviar el mismo aviso no crea una segunda fila. |
| ¿Qué pasa si el envío sigue fallando? | Se reintenta con espera creciente (*backoff* exponencial). Agotados los intentos, la notificación queda como `FalloDefinitivo` (*dead letter*) con su último error, y no se borra: un operador puede reintentarla. |
| ¿Qué cambia respecto al inciso c? | Solo el almacén. El despachador sigue hablando con `IRepositorioNotificaciones` y, si el repositorio que recibe además implementa `IRepositorioOutbox`, persiste antes de enviar. Con un repositorio en memoria, el comportamiento es idéntico al del inciso c. |

## El outbox: escribir antes de enviar

El despacho del inciso c ya tenía la forma correcta: `Encolar` no espera a ningún canal y los consumidores envían en segundo plano. Lo que le faltaba era la **durabilidad**. La solución es una capacidad opcional del repositorio, `IRepositorioOutbox`, que el despachador detecta sin cambiar de contrato:

```csharp
public void Encolar(Notificacion notificacion)
{
    // Outbox: la notificación queda en el disco ANTES de que se intente enviar.
    _outbox?.RegistrarPendiente(notificacion, intentos: 0);

    Interlocked.Increment(ref _enProceso);
    if (_cola.Writer.TryWrite(new EnvioPendiente(notificacion, Intentos: 0)))
    {
        return;
    }
    // ... cola llena: el aviso sigue pendiente en el outbox, no se pierde.
}
```

La línea que decide todo es la primera: entre `RegistrarPendiente` y `TryWrite` hay una ventana en la que el aviso **ya está en el disco y todavía no se ha enviado**. Si el proceso muere ahí, el aviso se recupera al siguiente arranque. Ese es el sentido de *at-least-once*: la entrega ocurre *al menos una vez*, y el precio de esa garantía es que puede ocurrir dos veces.

## Los estados durables

Cada fila del outbox tiene tres estados, que son los que permiten saber qué se puede borrar y qué no:

| Estado | Cuándo se pone | Qué implica |
|---|---|---|
| `Pendiente` | al encolar, antes de intentar el envío | se reintentará; si el proceso se cae, se recupera al reiniciar |
| `Entregada` | cuando el canal confirma | ya no hay nada que reintentar |
| `FalloDefinitivo` | al agotar los intentos (*dead letter*) | no se borra: queda con su último error para que un operador lo resuelva |

## Idempotencia

Un aviso reenviado no puede crear una segunda fila. La clave es el `Id` de la `Notificacion`, que se genera al crearla y viaja con ella por todo el flujo:

```csharp
private void Upsert(RegistroOutbox registro)
{
    if (!_registros.ContainsKey(registro.Id))
    {
        _orden.Add(registro.Id);
    }

    _registros[registro.Id] = registro;
    Volcar();
}
```

Y al registrar un aviso que ya estaba entregado, el repositorio no lo reabre:

```csharp
public void RegistrarPendiente(Notificacion notificacion, int intentos)
{
    RegistroOutbox? existente = Buscar(notificacion.Id);

    // Idempotencia: un aviso ya entregado no se vuelve a abrir. Si el mismo
    // Id llega otra vez, es un reenvío, no una notificación nueva.
    if (existente is not null && existente.Estado == EstadoNotificacion.Entregada)
    {
        return;
    }
    // ...
}
```

## Reintentos con backoff y *dead letter* persistido

El backoff del inciso c se conserva intacto; lo que se añade es que cada desenlace queda escrito:

```csharp
// El canal confirma:
_repositorio.Guardar(envio.Notificacion);
_outbox?.MarcarEntregada(envio.Notificacion.Id);

// El canal falla y quedan intentos:
_outbox?.MarcarReintento(envio.Notificacion.Id, intentos, error.Message);
Reprogramar(envio with { Intentos = intentos });

// Se agotaron los intentos: dead letter, no se borra.
DarPorPerdida(envio.Notificacion, intentos, error.Message);
```

El contador de intentos **vive en el outbox**, no en la cola: por eso un reinicio no reinicia la cuenta. Un aviso que ya falló dos veces y se recupera al tercer intento sigue llevando `intentos = 2` después de la caída.

## Reinicio y recuperación

Al arrancar, el proceso abre el outbox y devuelve a la cola lo que quedó pendiente:

```csharp
public int RecuperarPendientes()
{
    IReadOnlyList<EnvioPendiente> pendientes = _outbox.PendientesParaReintentar();
    int recuperadas = 0;

    foreach (EnvioPendiente envio in pendientes)
    {
        if (_cola.Writer.TryWrite(envio))
        {
            Interlocked.Increment(ref _enProceso);
            recuperadas++;
        }
    }

    return recuperadas;
}
```

Un detalle que no es trivial: **el canal no se serializa**. Lo que se guarda en el archivo es el *tipo* del canal (por ejemplo `CanalEmail`), y al recuperar el aviso se reconstruye con una fábrica:

```csharp
public sealed class FabricaCanales : IFabricaCanales
{
    public ICanalNotificacion? Crear(string tipoCanal) => tipoCanal switch
    {
        nameof(CanalEmail) => new CanalEmail(),
        nameof(CanalSms)    => new CanalSms(),
        nameof(CanalPush)   => new CanalPush(),
        _ => null,   // tipo desconocido: el aviso no se pierde, queda pendiente
    };
}
```

Si el tipo no se puede reconstruir, el aviso **no se descarta**: sigue pendiente en el outbox y queda listado en `NoRecuperables` para que un operador lo resuelva.

## Dónde vive la persistencia

En este ejercicio, en un archivo JSON con reemplazo atómico: se escribe a un archivo temporal y se renombra, para que un corte a mitad de guardado no deje el outbox a medias.

```csharp
private void Volcar()
{
    string temporal = _ruta + ".tmp";
    File.WriteAllText(temporal, json);
    File.Move(temporal, _ruta, overwrite: true);
}
```

Si el archivo está dañado, el repositorio **falla en voz alta** en vez de continuar como si el outbox estuviera vacío: un archivo corrupto significa notificaciones perdidas en silencio, y eso es justo lo que la restricción quiere evitar.

## Límites del diseño

Conviene ser explícito sobre lo que esta solución *no* resuelve:

- **Un solo proceso escritor.** Dos instancias sobre el mismo archivo se pisan, porque cada una mantiene su propia copia en memoria. En producción esto se resuelve con un *outbox* transaccional en una base de datos, donde la escritura del aviso y la del evento de negocio van en la misma transacción.
- **No es exactly-once.** La garantía es *at-least-once*: el cliente puede recibir dos avisos si el proceso se cae entre la confirmación del canal y la escritura del estado. Para evitarlo harían falta claves de idempotencia del lado del proveedor.
- **El outbox no sustituye al proveedor.** Si la dirección de correo ya no existe, el aviso se reintenta, agota los intentos y queda en *dead letter*: la persistencia garantiza que la *notificación* no se pierde, no que el *cliente* la reciba.

## Demostración

La demostración usa el código real:

```sh
dotnet run --project Bolsa.Notificaciones -- restriccion
```

Salida:

```
  1) Entrega normal: la alerta persiste el aviso y el canal lo confirma.
[EMAIL → cliente0001@correo.cu] AAPL subió por encima de su umbral 151.00: 150.00 → 155.00
  → Outbox: cliente0001@correo.cu → Entregada (intentos: 0)

  2) Canal caído: se reintenta con backoff y, al agotar los intentos, queda en dead letter.
[FALLO DespachadorAsincrono] intento 1/2 del aviso de +53 50000009 (InvalidOperationException: ...)
[FALLO DespachadorAsincrono] intento 2/2 del aviso de +53 50000009 (InvalidOperationException: ...)
  → Outbox: +53 50000009 → FalloDefinitivo (intentos: 2)
     último error: El proveedor rechazó el aviso de +53 50000009

  3) El proceso se cae con el aviso ya persistido y sin enviar.
  → Outbox antes de la caída: cliente0002@correo.cu → Pendiente (intentos: 0)
  [CAÍDA] el proceso se detiene.

  ── Reinicio: otro proceso abre el mismo archivo ──
  Pendientes leídos del disco: 1
  Avisos recuperados por el despachador: 1
[EMAIL → cliente0002@correo.cu] AAPL subió por encima de su umbral 151.00: 150.00 → 155.00
  → Outbox: cliente0002@correo.cu → Entregada (intentos: 0)

  4) Idempotencia: el mismo aviso encolado dos veces deja una sola fila en el outbox.
[EMAIL → cliente0003@correo.cu] AAPL 151.00 → 155.00
[EMAIL → cliente0003@correo.cu] AAPL 151.00 → 155.00
  → Filas del outbox: 3 antes, 4 después.
     Filas con ese mismo Id: 1 (el reenvío no duplica el registro).

  ── Resumen del outbox ──
     Entregadas: 3   Pendientes: 0   Fallo definitivo: 1
```

La escena 3 es la importante: el aviso se entrega *después* del reinicio, porque estaba en el disco antes de que el proceso muriera. Y la escena 4 muestra el otro lado de la moneda: el cliente puede recibir el aviso dos veces, pero el outbox guarda una sola fila.

## Pruebas

`dotnet test` ejecuta **35 pruebas, todas correctas**; 7 son de esta restricción:

| Prueba | Qué comprueba |
|---|---|
| `LaNotificacionQuedaPersistidaAntesDeEnviar` | Con el canal bloqueado dentro del envío, la fila del outbox está `Pendiente`; cuando el canal confirma, pasa a `Entregada`. |
| `ElPendienteSobreviveAlReinicio` | Otro proceso abre el mismo archivo y encuentra el aviso pendiente, con su mensaje, su contacto y su contador de intentos. |
| `LaNotificacionRecuperadaSeEntrega` | `RecuperarPendientes()` reencola el aviso, el canal lo recibe y la fila queda `Entregada`. |
| `ElFalloDefinitivoQuedaPersistido` | Canal siempre caído: la fila queda `FalloDefinitivo` con los 2 intentos y el último error. |
| `ElContadorDeIntentosSeConservaEnElDisco` | Canal que falla una vez y se recupera: la fila queda `Entregada` con `intentos = 1`. |
| `ElReenvioNoDuplicaLaFilaDelOutbox` | Registrar y guardar el mismo aviso varias veces deja una sola fila. |
| `UnOutboxDanadoNoSeDescartaEnSilencio` | Un archivo corrupto hace fallar la carga en vez de vaciar el outbox en silencio. |

Las 28 pruebas de los incisos b, c y d siguen pasando: el cambio no altera su comportamiento.

## Ficheros nuevos y modificados

| Fichero | Qué es |
|---|---|
| `Entrega/EstadoNotificacion.cs` | **Nuevo.** Los tres estados durables: `Pendiente`, `Entregada`, `FalloDefinitivo`. |
| `Entrega/RegistroOutbox.cs` | **Nuevo.** La fila del outbox: la notificación serializable más el estado del envío. |
| `Entrega/IRepositorioOutbox.cs` | **Nuevo.** La capacidad opcional del repositorio: registrar pendiente, marcar entregada, reintento y fallo definitivo. |
| `Entrega/IFabricaCanales.cs` | **Nuevo.** Reconstruye el canal de un aviso al recuperarlo del disco. |
| `Entrega/RepositorioPersistente.cs` | **Nuevo.** El outbox en un archivo JSON, con escritura atómica y carga estricta. |
| `Entrega/DespachadorAsincrono.cs` | **Modificado.** Persiste antes de encolar, marca cada desenlace y añade `RecuperarPendientes()`. |
| `Demos/DemoPersistencia.cs` | **Nuevo.** La demostración de las cuatro escenas. |
| `Program.cs` | **Modificado.** Nuevo modo `restriccion`. |
| `Bolsa.Notificaciones.Tests/` | **Nuevo.** Las 7 pruebas de la restricción. |
