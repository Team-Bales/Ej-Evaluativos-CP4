using System.Diagnostics;
using System.Globalization;
using Bolsa.Notificaciones.Diagnostico;
using Bolsa.Notificaciones.Entrega;
using Bolsa.Notificaciones.Observer;

namespace Bolsa.Notificaciones.Demos;

/// <summary>
/// Inciso c): demostración de las tres preguntas del enunciado.
///
///  1. Qué pasa si un cliente no puede ser notificado: el bucle clásico se
///     corta y los demás se quedan sin aviso; con aislamiento no.
///  2. Que la notificación fallida no se pierde: se reintenta con espera
///     creciente y, si el canal nunca responde, acaba en fallo definitivo.
///  3. Cómo no bloquear el hilo principal: se mide el mismo cambio de precio
///     con entrega síncrona y con entrega asíncrona.
/// </summary>
public static class DemoAsincrona
{
    private const decimal PrecioInicial = 150.00m;
    private const decimal PrecioTrasElSalto = 155.00m;
    private const decimal Umbral = 151.00m;

    public static async Task EjecutarAsync()
    {
        Console.WriteLine("Inciso c): aislamiento de fallos y entrega asíncrona.");
        Console.WriteLine("La demostración tarda unos segundos: los canales lentos esperan de verdad.\n");

        MostrarBucleClasico();
        MostrarBucleAislado();
        await MostrarReintentosAsync();
        await MedirUnCanalLentoAsync();
        await MedirVariosCanalesLentosAsync();
        await MostrarOrdenDeLlegadaAsync();

        Console.WriteLine("\n═══════════════ CONCLUSIÓN ═══════════════");
        Console.WriteLine("1. Un cliente no notificable solo se pierde a sí mismo: Accion aísla cada");
        Console.WriteLine("   observador y el despachador reintenta su notificación antes de darla por perdida.");
        Console.WriteLine("2. El hilo que cambia el precio no espera a ningún canal: solo encola.");
        Console.WriteLine("3. El Observer de GoF es SÍNCRONO: Notificar llama a Actualizar en el hilo del");
        Console.WriteLine("   sujeto. Lo que se vuelve asíncrono es el envío, detrás de la cola, y el precio");
        Console.WriteLine("   de eso es que el orden de llegada deja de estar garantizado.");
    }

    /// <summary>Milisegundos con punto decimal, para que la salida no dependa de la cultura del equipo.</summary>
    private static string Ms(TimeSpan tiempo) =>
        tiempo.TotalMilliseconds.ToString("0.0", CultureInfo.InvariantCulture);

    // ----------------------------------------------------------------------
    // 1. ¿Qué pasa si un cliente no puede ser notificado?
    // ----------------------------------------------------------------------

    /// <summary>
    /// Observer clásico: el cliente del medio tiene un canal que siempre falla
    /// (un teléfono dado de baja). Su excepción sube por Actualizar, corta el
    /// bucle de Notificar y el tercer cliente nunca recibe su aviso.
    /// </summary>
    private static void MostrarBucleClasico()
    {
        Console.WriteLine("――― 1a. Observer clásico: un fallo corta el bucle ―――");

        var despachador = new DespachadorNotificaciones(new RepositorioMemoria());
        var accion = new AccionSinAislamiento("AAPL", PrecioInicial);
        foreach (AlertaUmbral alerta in TresAlertas(despachador))
        {
            accion.Suscribir(alerta);
        }

        try
        {
            accion.ActualizarPrecio(PrecioTrasElSalto);
        }
        catch (Exception error)
        {
            Console.WriteLine($"  ActualizarPrecio lanzó {error.GetType().Name}: {error.Message}");
        }

        Console.WriteLine("  → Solo se entregó el aviso del primer cliente. El tercero se quedó sin avisar.\n");
    }

    /// <summary>
    /// La misma escena con <see cref="Accion"/>: el fallo del segundo cliente se
    /// registra y el recorrido sigue, así que el tercero sí recibe su aviso.
    /// </summary>
    private static void MostrarBucleAislado()
    {
        Console.WriteLine("――― 1b. Con aislamiento: el fallo no contagia a los demás ―――");

        var registro = new RegistroFallos();
        var despachador = new DespachadorNotificaciones(new RepositorioMemoria());
        var accion = new Accion("AAPL", PrecioInicial, registro);
        foreach (AlertaUmbral alerta in TresAlertas(despachador))
        {
            accion.Suscribir(alerta);
        }

        accion.ActualizarPrecio(PrecioTrasElSalto);

        Console.WriteLine($"  → Clientes 1 y 3 avisados; 1 fallo registrado (total: {registro.Total}).");
        Console.WriteLine("  Pero con entrega síncrona la notificación del cliente 2 se perdió: no hay reintento.\n");
    }

    /// <summary>Tres alertas sobre el mismo umbral; la del medio tiene un canal que siempre falla.</summary>
    private static AlertaUmbral[] TresAlertas(IDespachadorNotificaciones despachador) =>
    [
        new(new Cliente("C1", "cliente0001@correo.cu", new CanalEmail()), Umbral, Direccion.Sube, despachador),
        new(new Cliente("C2", "+53 50000002", new CanalQueFalla(new CanalSms(), int.MaxValue)), Umbral, Direccion.Sube, despachador),
        new(new Cliente("C3", "dispositivo-0003", new CanalPush()), Umbral, Direccion.Sube, despachador),
    ];

    // ----------------------------------------------------------------------
    // 2. La notificación fallida no se pierde: reintentos y fallo definitivo.
    // ----------------------------------------------------------------------

    private static async Task MostrarReintentosAsync()
    {
        Console.WriteLine("――― 2. Reintentos con espera creciente y fallo definitivo ―――");

        var registro = new RegistroFallos();
        var repositorio = new RepositorioMemoria();
        await using var despachador = new DespachadorAsincrono(
            repositorio, registro, consumidores: 2, maxIntentos: 3, esperaBase: TimeSpan.FromMilliseconds(50));

        var accion = new Accion("AAPL", PrecioInicial, registro);

        // El proveedor del cliente 4 se cae dos veces y se recupera; el del 5 no responde nunca.
        var canalIntermitente = new CanalQueFalla(new CanalEmail(), fallosIniciales: 2);
        var canalCaido = new CanalQueFalla(new CanalSms(), int.MaxValue);

        accion.Suscribir(new AlertaUmbral(
            new Cliente("C4", "cliente0004@correo.cu", canalIntermitente), Umbral, Direccion.Sube, despachador));
        accion.Suscribir(new AlertaUmbral(
            new Cliente("C5", "+53 50000005", canalCaido), Umbral, Direccion.Sube, despachador));
        accion.Suscribir(new AlertaUmbral(
            new Cliente("C6", "dispositivo-0006", new CanalPush()), Umbral, Direccion.Sube, despachador));

        accion.ActualizarPrecio(PrecioTrasElSalto);
        await despachador.EsperarVaciadoAsync();

        Console.WriteLine($"  → Entregadas: {despachador.Entregadas} de 3 (C4 llegó en el intento {canalIntermitente.Intentos}).");
        Console.WriteLine($"  → Intentos fallidos: {despachador.FallosDeEnvio}; reintentos programados: {despachador.Reintentos}.");
        Console.WriteLine($"  → Fallo definitivo (dead letter): {despachador.FallosDefinitivos.Count} " +
                          $"tras {canalCaido.Intentos} intentos. La restricción E9-R lo persiste para no perderlo.\n");
    }

    // ----------------------------------------------------------------------
    // 3. ¿Cómo evitar bloquear el hilo principal?
    // ----------------------------------------------------------------------

    /// <summary>
    /// Un cliente con un canal de 2 s, como una pasarela de SMS. Con entrega
    /// síncrona el cambio de precio paga esos 2 s; con la cola, no.
    /// </summary>
    private static async Task MedirUnCanalLentoAsync()
    {
        Console.WriteLine("――― 3a. Un canal lento (SMS de 2 s) y dos clientes rápidos ―――");
        TimeSpan retardo = TimeSpan.FromSeconds(2);

        TimeSpan sincrono = MedirCambioDePrecio(
            new DespachadorNotificaciones(new RepositorioMemoria()), retardo, clientesLentos: 1);

        await using var asincrono = new DespachadorAsincrono(
            new RepositorioMemoria(), new RegistroFallos(), consumidores: 4);
        TimeSpan encolado = MedirCambioDePrecio(asincrono, retardo, clientesLentos: 1);

        var reloj = Stopwatch.StartNew();
        await asincrono.EsperarVaciadoAsync();
        reloj.Stop();

        Console.WriteLine($"  Síncrono:  ActualizarPrecio tardó {Ms(sincrono)} ms (el hilo esperó al SMS).");
        Console.WriteLine($"  Asíncrono: ActualizarPrecio tardó {Ms(encolado)} ms (solo encoló 3 notificaciones).");
        Console.WriteLine($"  La entrega terminó {Ms(reloj.Elapsed)} ms después, en segundo plano: " +
                          $"{asincrono.Entregadas} notificaciones.\n");
    }

    /// <summary>
    /// Cuatro clientes lentos y cuatro consumidores: la entrega síncrona los
    /// suma uno detrás de otro, la cola los solapa.
    /// </summary>
    private static async Task MedirVariosCanalesLentosAsync()
    {
        Console.WriteLine("――― 3b. Cuatro canales lentos (500 ms cada uno) ―――");
        TimeSpan retardo = TimeSpan.FromMilliseconds(500);

        TimeSpan sincrono = MedirCambioDePrecio(
            new DespachadorNotificaciones(new RepositorioMemoria()), retardo, clientesLentos: 4);

        await using var asincrono = new DespachadorAsincrono(
            new RepositorioMemoria(), new RegistroFallos(), consumidores: 4);
        TimeSpan encolado = MedirCambioDePrecio(asincrono, retardo, clientesLentos: 4);

        var reloj = Stopwatch.StartNew();
        await asincrono.EsperarVaciadoAsync();
        reloj.Stop();

        Console.WriteLine($"  Síncrono:  {Ms(sincrono)} ms (4 × 500 ms, uno detrás de otro).");
        Console.WriteLine($"  Asíncrono: {Ms(encolado)} ms para encolar y {Ms(reloj.Elapsed)} ms de entrega " +
                          "con 4 consumidores en paralelo.\n");
    }

    /// <summary>
    /// Mide lo que tarda un cambio de precio con un despachador dado. Los
    /// clientes lentos llevan un <see cref="CanalLento"/> y el resto un canal
    /// inmediato; todos son silenciosos, porque escribir en la consola cuesta
    /// más que el envío simulado y falsearía la medición.
    /// </summary>
    private static TimeSpan MedirCambioDePrecio(
        IDespachadorNotificaciones despachador, TimeSpan retardo, int clientesLentos)
    {
        var accion = new Accion("AAPL", PrecioInicial);

        for (int i = 1; i <= clientesLentos; i++)
        {
            ICanalNotificacion canal = new CanalLento(new CanalSilencioso(), retardo);
            accion.Suscribir(new AlertaUmbral(
                new Cliente($"L{i}", $"+53 5000{i:D4}", canal), Umbral, Direccion.Sube, despachador));
        }

        for (int i = 1; i <= 2; i++)
        {
            accion.Suscribir(new AlertaUmbral(
                new Cliente($"R{i}", $"dispositivo-{i:D4}", new CanalSilencioso()), Umbral, Direccion.Sube, despachador));
        }

        var reloj = Stopwatch.StartNew();
        accion.ActualizarPrecio(PrecioTrasElSalto);
        reloj.Stop();

        return reloj.Elapsed;
    }

    /// <summary>
    /// El precio de la asincronía: tres clientes suscritos en el orden 1, 2, 3
    /// con canales de 300, 100 y 200 ms. Síncrono entrega en el orden de
    /// suscripción; con la cola llega primero el canal más rápido.
    /// </summary>
    private static async Task MostrarOrdenDeLlegadaAsync()
    {
        Console.WriteLine("――― 3c. El orden de llegada ya no está garantizado ―――");
        int[] retardos = [300, 100, 200];

        Console.WriteLine("  Síncrono (orden de suscripción):");
        Notificar(new DespachadorNotificaciones(new RepositorioMemoria()), retardos);

        Console.WriteLine("  Asíncrono (orden de respuesta de cada canal):");
        await using var asincrono = new DespachadorAsincrono(
            new RepositorioMemoria(), new RegistroFallos(), consumidores: 4);
        Notificar(asincrono, retardos);
        await asincrono.EsperarVaciadoAsync();

        Console.WriteLine();

        static void Notificar(IDespachadorNotificaciones despachador, int[] retardos)
        {
            var accion = new Accion("AAPL", PrecioInicial);

            for (int i = 0; i < retardos.Length; i++)
            {
                ICanalNotificacion canal = new CanalLento(
                    new CanalPush(), TimeSpan.FromMilliseconds(retardos[i]));
                accion.Suscribir(new AlertaUmbral(
                    new Cliente($"C{i + 1}", $"cliente{i + 1} ({retardos[i]} ms)", canal),
                    Umbral,
                    Direccion.Sube,
                    despachador));
            }

            accion.ActualizarPrecio(PrecioTrasElSalto);
        }
    }
}
