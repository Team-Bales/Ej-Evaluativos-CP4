# Ejercicio 9, inciso b): Implementación

**Issue:** #2

> *Implementa con 5 acciones, 1000 clientes y umbrales personalizados. Notificaciones por email, SMS y push.*

La implementación sigue el diseño del [inciso a](inciso-a.md): mismas clases y mismas relaciones que el [diagrama de clases](diagramas/inciso-a-clases.png). El código está en [`codigo/`](codigo/).

## Cómo ejecutarlo

Requisito: [SDK de .NET 10](https://dotnet.microsoft.com/download). Desde `ejercicio-09-notificaciones-bolsa/codigo/`:

```sh
dotnet run --project Bolsa.Notificaciones   # la simulación
dotnet test                                  # las pruebas unitarias (xUnit)
```

Hay que indicar `--project` porque la carpeta tiene dos proyectos (el programa y sus pruebas). No hace falta ningún paso manual: `dotnet` descarga xUnit la primera vez.

## Estructura del código

| Carpeta / fichero | Clase del diagrama | Rol |
|---|---|---|
| `Observer/ISujetoPrecio.cs` | `ISujetoPrecio` | Subject |
| `Observer/Accion.cs` | `Accion` | ConcreteSubject |
| `Observer/IObservadorPrecio.cs` | `IObservadorPrecio` | Observer |
| `Observer/AlertaUmbral.cs` | `AlertaUmbral` | ConcreteObserver; aplica la regla de disparo |
| `Observer/CambioPrecio.cs`, `Direccion.cs` | `CambioPrecio`, `Direccion` | Dato que viaja (modelo push) y dirección del cruce |
| `Entrega/Cliente.cs`, `Notificacion.cs` | `Cliente`, `Notificacion` | Datos del cliente y del aviso |
| `Entrega/ICanalNotificacion.cs` | `ICanalNotificacion` | Strategy de entrega |
| `Entrega/CanalEmail.cs`, `CanalSms.cs`, `CanalPush.cs` | `CanalEmail`, `CanalSms`, `CanalPush` | Los 3 canales; el envío se simula en la consola |
| `Entrega/DespachadorNotificaciones.cs` | `DespachadorNotificaciones` | Entrega por el canal del cliente y guarda en el repositorio |
| `Entrega/IRepositorioNotificaciones.cs`, `RepositorioMemoria.cs` | `IRepositorioNotificaciones`, `RepositorioMemoria` | Registro de las notificaciones entregadas |
| `Program.cs` | — | La simulación |
| `Bolsa.Notificaciones.Tests/` | — | Pruebas unitarias |

## Regla de disparo

Una alerta **solo notifica cuando el precio cruza su umbral** en su dirección, no en cada cambio mientras el precio siga del otro lado:

| Dirección | Se notifica si… |
|---|---|
| `Sube` | precio anterior **≤** umbral **y** precio nuevo **>** umbral |
| `Baja` | precio anterior **≥** umbral **y** precio nuevo **<** umbral |

```csharp
private bool CruzaElUmbral(CambioPrecio cambio) => Direccion switch
{
    Direccion.Sube => cambio.PrecioAnterior <= Umbral && cambio.PrecioNuevo > Umbral,
    Direccion.Baja => cambio.PrecioAnterior >= Umbral && cambio.PrecioNuevo < Umbral,
    _ => false,
};
```

**Por qué esta regla:**

- **Evita avisos repetidos.** Si se notificara siempre que el precio esté por encima del umbral, un cliente recibiría un aviso en cada tick mientras dure la subida. Con el cruce recibe uno solo, en el momento en que ocurre, que es lo que pide "notificaciones inmediatas" cuando "alcanzan precios específicos".
- **Se rearma sola.** Si el precio vuelve al otro lado y cruza otra vez, hay un aviso nuevo: es otro evento. No hace falta guardar estado en la alerta, porque `CambioPrecio` trae el precio anterior y el nuevo (modelo push).
- **Es decisión del observador, no de la acción.** La acción avisa a todas sus alertas en cada cambio, y cada `AlertaUmbral` decide si el cambio cruza *su* umbral. Así la acción no necesita conocer los umbrales.

Si el precio no cambia, `Accion.ActualizarPrecio` no notifica: no hay evento.

## Datos de la simulación

- **5 acciones**, cada una un Subject independiente con su propia lista de observadores: AAPL (150), MSFT (310), GOOG (140), AMZN (180) y TSLA (250).
- **1000 clientes** generados por código. El canal se reparte por turno (email, SMS, push) y cada cliente se suscribe a **1 o 2 acciones**, alternando entre `Sube` y `Baja`. En total hay **1500 alertas**.
- **Umbrales personalizados:** precio inicial ± (0,1 % + 0,12 % × orden de llegada), distinto para cada alerta de la misma acción y dirección. El programa comprueba que no se repite ninguno.
- **10 ticks de precios:** en los ticks 1 a 9 cada precio varía hasta ±1,5 %; el tick 10 simula un evento de mercado con saltos fuertes (+9 %, −7 %, +12 %, −5 % y +8 %).
- **Desuscripción:** después del tick 8, los primeros 25 clientes se desuscriben de todas sus alertas.
- **Semilla fija** (`20261016`): la simulación es reproducible, da los mismos precios y avisos en cada ejecución.

## Resultados

Cada línea `[CANAL → contacto]` es una notificación entregada. Aparece debajo del cambio de precio que la dispara, en el mismo tick:

```text
――― Tick 1 ―――
  AAPL: 150.00 → 149.44
[EMAIL → cliente0006@correo.cu] AAPL bajó por debajo de su umbral 149.85: 150.00 → 149.44
[SMS → +53 50000016] AAPL bajó por debajo de su umbral 149.67: 150.00 → 149.44
[PUSH → dispositivo-0026] AAPL bajó por debajo de su umbral 149.49: 150.00 → 149.44
  MSFT: 310.00 → 306.49
[PUSH → dispositivo-0002] MSFT bajó por debajo de su umbral 309.69: 310.00 → 306.49
...
  … 25 clientes se desuscriben (37 alertas quitadas).
...
═══════════════════ RESUMEN ═══════════════════
Notificaciones entregadas: 417 (de 1500 alertas suscritas).
Clientes notificados: 306 de 1000.
  • por email: 142 avisos.
  • por push: 135 avisos.
  • por SMS: 140 avisos.
  • AAPL: 94 avisos (precio final 160.83).
  • MSFT: 70 avisos (precio final 292.70).
  • GOOG: 106 avisos (precio final 154.74).
  • AMZN: 84 avisos (precio final 165.36).
  • TSLA: 63 avisos (precio final 255.24).
Desuscripción: los 25 clientes desuscritos recibieron 30 avisos antes y 0 después (debe ser 0).
Umbrales personalizados: todos distintos (1500 alertas).
Repositorio: 417 notificaciones persistidas (base de la entrega at-least-once de la restricción E9-R).
```

Lo que muestra:

- **Solo se notifica a quien cruzó su umbral:** 417 avisos de 1500 alertas, y solo 306 de los 1000 clientes recibieron alguno. Los umbrales más alejados del precio no se alcanzan.
- **Los tres canales funcionan:** 142 avisos por email, 140 por SMS y 135 por push.
- **La desuscripción funciona:** los 25 clientes desuscritos recibieron 30 avisos antes y **0 después**.
- **El aviso es inmediato:** cada notificación sale en el mismo tick del cruce. En los ticks normales hay entre 11 y 37 avisos; en el tick 10, con los saltos fuertes, 231.

## Pruebas unitarias

`dotnet test` ejecuta **11 pruebas, todas correctas**:

| Prueba | Qué comprueba | Criterio del issue |
|---|---|---|
| `Notifica_CuandoElPrecioCruzaElUmbralHaciaArriba` | Acercarse sin cruzar no avisa; cruzar al alza avisa una vez y lo guarda | Notifica al cruzar |
| `Notifica_CuandoElPrecioCruzaElUmbralHaciaAbajo` | Lo mismo a la baja | Notifica al cruzar |
| `NoNotifica_SiElPrecioQuedaPorDebajoDelUmbral` | Subir sin llegar al umbral no avisa | No notifica por debajo |
| `NoNotifica_RepetidasVeces_MientrasElPrecioSigaPorEncimaDelUmbral` | Tres subidas seguidas por encima dan un solo aviso | Regla de disparo |
| `NoNotifica_DespuesDeDesuscribirse` | Tras desuscribirse, un nuevo cruce ya no avisa | Desuscripción |
| `NotificaATodosLosObservadoresSuscritos` | Un cambio avisa a todas las alertas cuyo umbral cruza | Uno-a-muchos |
| `CanalEmail_…`, `CanalSms_…`, `CanalPush_…` | Cada canal entrega el mensaje al contacto de su cliente | Cada canal recibe su mensaje |
| `EntregaCadaNotificacionPorElCanalDeSuCliente` | El despachador usa el canal de cada cliente | Cada canal recibe su mensaje |
| `GuardaEnElRepositorioCadaNotificacionEntregada` | Toda notificación entregada queda en el repositorio | — |

## Decisiones de esta implementación

- **El despachador entrega de forma síncrona.** `Encolar` deja la notificación en la cola (`Channel<Notificacion>`) y la procesa al momento. Así la simulación es determinista y las pruebas no dependen de hilos. El inciso c convierte ese envío en asíncrono sin cambiar las alertas, que solo encolan.
- **El repositorio está en memoria.** `RepositorioMemoria` registra las notificaciones entregadas. La restricción (E9-R) añadirá una implementación persistente y los reintentos; el despachador no cambia, porque solo conoce `IRepositorioNotificaciones`.
- **El envío es simulado:** cada canal escribe el aviso en la consola con su tipo y el contacto del cliente.
