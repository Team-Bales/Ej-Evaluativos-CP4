using System.Diagnostics;
using System.Globalization;
using System.Runtime;
using System.Runtime.InteropServices;
using Bolsa.Notificaciones.Diagnostico;
using Bolsa.Notificaciones.Entrega;
using Bolsa.Notificaciones.Observer;
using Bolsa.Notificaciones.Rendimiento;

namespace Bolsa.Notificaciones.Demos;

/// <summary>
/// Inciso d): rendimiento con hasta 1 000 000 de clientes.
///
/// La medición separa las dos fases, porque solo una cambia de orden de
/// complejidad:
///   · DETECTAR a quién hay que avisar. Recorrer la lista de observadores
///     cuesta O(n); con los umbrales ordenados, O(log n + k).
///   · ENTREGAR los k avisos. Es trabajo proporcional a k se haga como se haga;
///     el thread pool y la cola lo reparten, no lo reducen.
/// </summary>
public static class BenchmarkRendimiento
{
    private const decimal PrecioInicial = 150.00m;

    /// <summary>
    /// Ticks que se promedian y ticks previos que se descartan. Se descartan
    /// los primeros porque en ellos se paga la compilación JIT del camino que
    /// se va a medir. Del resto se toma la <b>mediana</b> y no la media: una
    /// pausa del recolector de basura en un solo tick desplazaría la media.
    /// </summary>
    private const int TicksDeMedicion = 11;

    private const int TicksDeCalentamiento = 3;

    /// <summary>Banda de precios en la que se reparten los umbrales del escenario B.</summary>
    private const decimal BandaConcentrada = 30.00m;

    private static readonly int[] TamanosPorOmision = [1_000, 10_000, 100_000, 1_000_000];
    private static readonly IRegistroFallos Silencioso = new RegistroFallos(TextWriter.Null);

    /// <summary>
    /// Un único canal para todos los clientes: lo que se mide aquí es la
    /// detección, y darle un canal propio a cada uno solo sumaría memoria.
    /// </summary>
    private static readonly ICanalNotificacion CanalCompartido = new CanalSilencioso();

    /// <summary>Cómo se reparten los umbrales y cuánto se mueve el precio en cada tick.</summary>
    private sealed record Escenario(string Nombre, Func<int, decimal> Paso, decimal SaltoPorTick, string Descripcion);

    private sealed record Medicion(double MedianaMs, int Notificados, double ConstruccionMs, double MemoriaMb);

    private static readonly Escenario Dispersos = new(
        "A",
        _ => 0.01m,
        0.50m,
        "umbrales dispersos (un umbral por centavo), así que k no crece con n");

    private static readonly Escenario Concentrados = new(
        "B",
        n => BandaConcentrada / n,
        0.30m,
        "umbrales concentrados (los n repartidos en una banda de 30 $), así que k crece con n");

    public static async Task EjecutarAsync(string? maximo)
    {
        int tope = LeerTope(maximo);
        int[] tamanos = TamanosPorOmision.Where(n => n <= tope).ToArray();

        if (tamanos.Length == 0)
        {
            Console.Error.WriteLine($"No hay ningún tamaño medible por debajo de {tope}.");
            return;
        }

        ImprimirMaquina();
        Calentar();

        MedirEscenario(Dispersos, tamanos);
        MedirEscenario(Concentrados, tamanos);
        MedirParalelo(tamanos[^1]);
        await MedirEntregaAsync();
        await MedirBackpressureAsync();

        Console.WriteLine("\n═══════════════ CONCLUSIÓN ═══════════════");
        Console.WriteLine("1. El recorrido ingenuo cuesta O(n) por tick: con 1M de clientes se revisan");
        Console.WriteLine("   1M de umbrales aunque solo haya que avisar a un puñado.");
        Console.WriteLine("2. Con los umbrales ordenados, los cruzados son un tramo contiguo: O(log n + k).");
        Console.WriteLine("3. El paralelismo baja el tiempo de reloj, no el orden: el trabajo total es el mismo.");
        Console.WriteLine("4. La cola acotada convierte la saturación en una decisión explícita.");
    }

    // ----------------------------------------------------------------------
    // Detección: O(n) frente a O(log n + k)
    // ----------------------------------------------------------------------

    private static void MedirEscenario(Escenario escenario, int[] tamanos)
    {
        Console.WriteLine($"\n═══ Escenario {escenario.Nombre}: {escenario.Descripcion} ═══");
        Console.WriteLine($"Cada tick sube el precio {Numero(escenario.SaltoPorTick, 2)} $; mediana de {TicksDeMedicion} ticks.\n");

        Console.WriteLine("          n |       k | ingenuo O(n) |  indexado O(log n+k) | mejora |  memoria ing./idx. | construcción ing./idx.");
        Console.WriteLine("  ----------+---------+--------------+----------------------+--------+--------------------+-----------------------");

        foreach (int n in tamanos)
        {
            Medicion ingenuo = MedirIngenuo(n, escenario);
            Medicion indexado = MedirIndexado(n, escenario, enParalelo: false);
            double mejora = ingenuo.MedianaMs / Math.Max(indexado.MedianaMs, 0.000001);

            Console.WriteLine(
                $"  {n,9:N0} | {ingenuo.Notificados,7:N0} | " +
                $"{Numero(ingenuo.MedianaMs, 3),9} ms | {Numero(indexado.MedianaMs, 4),17} ms | " +
                $"{Numero(mejora, 1),5}x | " +
                $"{Numero(ingenuo.MemoriaMb, 1),7} /{Numero(indexado.MemoriaMb, 1),6} MB | " +
                $"{Numero(ingenuo.ConstruccionMs, 0),7} /{Numero(indexado.ConstruccionMs, 0),6} ms");
        }
    }

    private static Medicion MedirIngenuo(int n, Escenario escenario)
    {
        decimal paso = escenario.Paso(n);
        var despachador = new DespachadorContador();

        long antes = MemoriaEstable();
        var reloj = Stopwatch.StartNew();

        var accion = new Accion("BENCH", PrecioInicial, Silencioso);
        for (int i = 1; i <= n; i++)
        {
            accion.Suscribir(new AlertaUmbral(ClienteDe(i), PrecioInicial + paso * i, Direccion.Sube, despachador));
        }

        reloj.Stop();
        double memoria = (MemoriaEstable() - antes) / (1024.0 * 1024.0);

        return Ejecutar(accion.ActualizarPrecio, despachador, escenario, reloj.Elapsed.TotalMilliseconds, memoria);
    }

    private static Medicion MedirIndexado(int n, Escenario escenario, bool enParalelo)
    {
        decimal paso = escenario.Paso(n);
        var despachador = new DespachadorContador();

        long antes = MemoriaEstable();
        var reloj = Stopwatch.StartNew();

        var suscripciones = new SuscripcionUmbral[n];
        for (int i = 1; i <= n; i++)
        {
            suscripciones[i - 1] = new SuscripcionUmbral(ClienteDe(i), PrecioInicial + paso * i, Direccion.Sube);
        }

        var accion = new Accion("BENCH", PrecioInicial, Silencioso);
        accion.Suscribir(GestorUmbralesIndexado.Construir(suscripciones, despachador, enParalelo));

        reloj.Stop();
        double memoria = (MemoriaEstable() - antes) / (1024.0 * 1024.0);

        return Ejecutar(accion.ActualizarPrecio, despachador, escenario, reloj.Elapsed.TotalMilliseconds, memoria);
    }

    /// <summary>
    /// Ticks de calentamiento que se descartan y después los que cuentan. De
    /// estos se devuelve la mediana, junto con los avisos del último tick.
    /// </summary>
    private static Medicion Ejecutar(
        Action<decimal> cambiarPrecio,
        DespachadorContador despachador,
        Escenario escenario,
        double construccionMs,
        double memoriaMb)
    {
        decimal precio = PrecioInicial;

        for (int tick = 0; tick < TicksDeCalentamiento; tick++)
        {
            precio += escenario.SaltoPorTick;
            cambiarPrecio(precio);
        }

        double[] tiempos = new double[TicksDeMedicion];
        int notificados = 0;

        for (int tick = 0; tick < TicksDeMedicion; tick++)
        {
            precio += escenario.SaltoPorTick;
            int antes = despachador.Encoladas;

            var reloj = Stopwatch.StartNew();
            cambiarPrecio(precio);
            reloj.Stop();

            tiempos[tick] = reloj.Elapsed.TotalMilliseconds;
            notificados = despachador.Encoladas - antes;
        }

        Array.Sort(tiempos);

        return new Medicion(tiempos[TicksDeMedicion / 2], notificados, construccionMs, memoriaMb);
    }

    // ----------------------------------------------------------------------
    // Thread pool: menos tiempo de reloj, el mismo orden de complejidad
    // ----------------------------------------------------------------------

    private static void MedirParalelo(int n)
    {
        Console.WriteLine($"\n═══ Thread pool: repartir los k avisos entre {Environment.ProcessorCount} hilos ═══");
        Console.WriteLine($"Escenario B con n = {n:N0}, donde k es grande y el reparto se nota.\n");

        Medicion secuencial = MedirIndexado(n, Concentrados, enParalelo: false);
        Medicion paralelo = MedirIndexado(n, Concentrados, enParalelo: true);
        double aceleracion = secuencial.MedianaMs / Math.Max(paralelo.MedianaMs, 0.000001);

        Console.WriteLine($"  Indexado secuencial:  {Numero(secuencial.MedianaMs, 3),9} ms por tick ({secuencial.Notificados:N0} avisos)");
        Console.WriteLine($"  Indexado en paralelo: {Numero(paralelo.MedianaMs, 3),9} ms por tick ({paralelo.Notificados:N0} avisos)");
        Console.WriteLine($"  Aceleración: {Numero(aceleracion, 2)}x sobre {Environment.ProcessorCount} hilos.");
        Console.WriteLine("  El trabajo total no bajó: se repartió. El orden de complejidad sigue siendo O(log n + k).");
    }

    // ----------------------------------------------------------------------
    // Entrega: cuántas notificaciones por segundo salen de la cola
    // ----------------------------------------------------------------------

    private static async Task MedirEntregaAsync()
    {
        int[] consumidoresAProbar = [1, 2, 4, 8];

        Console.WriteLine("\n═══ Entrega: ¿cuántos consumidores conviene poner? ═══");
        Console.WriteLine("  Las notificaciones se crean antes de medir: aquí solo se mide la cola.\n");

        const int SinEspera = 200_000;
        Console.WriteLine($"  a) Canal instantáneo, {SinEspera:N0} avisos (el envío no cuesta nada):");

        foreach (int consumidores in consumidoresAProbar)
        {
            (double encolado, double total) = await VaciarColaAsync(
                SinEspera, consumidores, CanalCompartido, SinEspera);

            Console.WriteLine(
                $"     {consumidores} consumidor(es): encolar {Numero(encolado, 0),4} ms · " +
                $"vaciar {Numero(total, 0),4} ms · {Numero(SinEspera / total, 0),4} mil avisos/s");
        }

        const int ConEspera = 1_000;
        var canalLento = new CanalLento(new CanalSilencioso(), TimeSpan.FromMilliseconds(1));
        Console.WriteLine($"\n  b) Canal de 1 ms, {ConEspera:N0} avisos (como un proveedor real):");

        foreach (int consumidores in consumidoresAProbar)
        {
            (_, double total) = await VaciarColaAsync(ConEspera, consumidores, canalLento, ConEspera);

            Console.WriteLine(
                $"     {consumidores} consumidor(es): vaciar {Numero(total, 0),5} ms · " +
                $"{Numero(ConEspera / total * 1000, 0),5} avisos/s");
        }
    }

    /// <summary>
    /// Encola notificaciones ya creadas y espera a que la cola se vacíe.
    /// Devuelve lo que costó encolar y el tiempo total.
    /// </summary>
    private static async Task<(double EncoladoMs, double TotalMs)> VaciarColaAsync(
        int cuantas, int consumidores, ICanalNotificacion canal, int capacidad)
    {
        var cliente = new Cliente("C1", "c1@correo.cu", canal);
        Notificacion[] avisos = new Notificacion[cuantas];
        for (int i = 0; i < cuantas; i++)
        {
            avisos[i] = new Notificacion(Guid.NewGuid(), cliente, "aviso");
        }

        await using var despachador = new DespachadorAsincrono(
            new RepositorioContador(), Silencioso, consumidores, capacidad);

        var reloj = Stopwatch.StartNew();
        foreach (Notificacion aviso in avisos)
        {
            despachador.Encolar(aviso);
        }

        double encolado = reloj.Elapsed.TotalMilliseconds;
        await despachador.EsperarVaciadoAsync(TimeSpan.FromMinutes(2));
        reloj.Stop();

        return (encolado, reloj.Elapsed.TotalMilliseconds);
    }

    // ----------------------------------------------------------------------
    // Backpressure: cuando los eventos llegan más rápido que los envíos
    // ----------------------------------------------------------------------

    private static async Task MedirBackpressureAsync()
    {
        const int Notificaciones = 20_000;
        const int Capacidad = 1_000;

        Console.WriteLine($"\n═══ Backpressure: {Notificaciones:N0} avisos de golpe con una cola de {Capacidad:N0} ═══");
        Console.WriteLine("  El canal tarda 1 ms, así que los consumidores no pueden seguir el ritmo.\n");

        var canal = new CanalLento(new CanalSilencioso(), TimeSpan.FromMilliseconds(1));
        var cliente = new Cliente("C1", "c1@correo.cu", canal);
        await using var despachador = new DespachadorAsincrono(
            new RepositorioContador(), Silencioso, consumidores: 4, capacidad: Capacidad);

        var reloj = Stopwatch.StartNew();
        for (int i = 0; i < Notificaciones; i++)
        {
            despachador.Encolar(new Notificacion(Guid.NewGuid(), cliente, "aviso"));
        }

        double encolado = reloj.Elapsed.TotalMilliseconds;
        await despachador.EsperarVaciadoAsync(TimeSpan.FromMinutes(2));
        reloj.Stop();

        Console.WriteLine($"  Encolar los {Notificaciones:N0} avisos: {Numero(encolado, 1)} ms (el sujeto nunca esperó).");
        Console.WriteLine($"  Aceptadas y entregadas:     {despachador.Entregadas,7:N0}");
        Console.WriteLine($"  Rechazadas por cola llena:  {despachador.Rechazadas,7:N0}");
        Console.WriteLine($"  Tiempo total hasta vaciar:  {Numero(reloj.Elapsed.TotalMilliseconds, 0),7} ms");
    }

    // ----------------------------------------------------------------------
    // Utilidades
    // ----------------------------------------------------------------------

    private static Cliente ClienteDe(int i) => new($"C{i:D7}", $"cliente{i:D7}@correo.cu", CanalCompartido);

    private static int LeerTope(string? maximo) =>
        int.TryParse(maximo, NumberStyles.Integer, CultureInfo.InvariantCulture, out int valor) && valor > 0
            ? valor
            : TamanosPorOmision[^1];

    /// <summary>Fuerza la compilación JIT de los dos caminos antes de medir nada.</summary>
    private static void Calentar()
    {
        MedirIngenuo(1_000, Dispersos);
        MedirIndexado(1_000, Dispersos, enParalelo: false);
        MedirIndexado(1_000, Dispersos, enParalelo: true);
    }

    private static long MemoriaEstable()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        return GC.GetTotalMemory(forceFullCollection: true);
    }

    private static string Numero(double valor, int decimales) =>
        valor.ToString($"N{decimales}", CultureInfo.InvariantCulture);

    private static string Numero(decimal valor, int decimales) =>
        valor.ToString($"N{decimales}", CultureInfo.InvariantCulture);

    private static void ImprimirMaquina()
    {
        Console.WriteLine("Inciso d): rendimiento de la detección y de la entrega.");
        Console.WriteLine($"  Máquina: {RuntimeInformation.OSDescription.Trim()} · {RuntimeInformation.ProcessArchitecture}");
        Console.WriteLine($"  Hilos:   {Environment.ProcessorCount} procesadores lógicos");
        Console.WriteLine($"  Runtime: {RuntimeInformation.FrameworkDescription}");
        Console.WriteLine($"  GC:      {(GCSettings.IsServerGC ? "servidor" : "estación de trabajo")}");
    }
}
