using System.Globalization;
using Bolsa.Notificaciones.Diagnostico;
using Bolsa.Notificaciones.Entrega;
using Bolsa.Notificaciones.Observer;

namespace Bolsa.Notificaciones.Demos;

/// <summary>
/// Restricción E9-R: entrega <em>at-least-once</em> y persistencia de las
/// notificaciones fallidas.
///
/// La demostración usa el código real, no una simulación aparte:
/// <list type="number">
///   <item>Una entrega normal: la alerta persiste el aviso antes de encolarlo
///         y el canal lo confirma.</item>
///   <item>Un canal caído: se reintenta con backoff y, al agotar los intentos,
///         el aviso queda en dead letter, también persistido.</item>
///   <item>Una caída del proceso con el aviso persistido y sin enviar: al
///         reiniciar, otro proceso lee el outbox y lo entrega.</item>
///   <item>El mismo aviso encolado dos veces: el outbox guarda una sola fila
///         (idempotencia), aunque el cliente pueda recibir dos avisos, como
///         permite at-least-once.</item>
/// </list>
/// </summary>
public static class DemoPersistencia
{
    private const decimal PrecioInicial = 150.00m;
    private const decimal PrecioTrasElSalto = 155.00m;
    private const decimal Umbral = 151.00m;

    public static async Task EjecutarAsync()
    {
        Console.WriteLine("Restricción E9-R: entrega at-least-once y persistencia de notificaciones fallidas.");
        Console.WriteLine("Cada aviso se escribe en el outbox antes de intentar enviarlo.\n");

        string carpeta = Path.Combine(
            Path.GetTempPath(),
            $"bolsa-e9r-{Guid.NewGuid().ToString("N")[..8]}");
        string ruta = Path.Combine(carpeta, "notificaciones.json");

        RepositorioPersistente repositorio = new(ruta);
        Console.WriteLine($"  Outbox: {ruta}\n");

        await EscenarioEntregaNormalAsync(repositorio);
        await EscenarioCanalCaidoAsync(repositorio);

        // A partir del reinicio manda la nueva instancia: el outbox tiene un
        // solo proceso escritor, así que dos instancias sobre el mismo archivo
        // se pisarían.
        RepositorioPersistente trasReinicio = await EscenarioCaidaYReinicioAsync(repositorio, ruta);
        await EscenarioIdempotenciaAsync(trasReinicio);

        Console.WriteLine("\n  ── Resumen del outbox ──");
        Resumir(trasReinicio);
        Console.WriteLine();
        Console.WriteLine("  El aviso que falló sigue en el archivo: no se pierde, se puede reintentar.");
        Console.WriteLine("  El aviso pendiente antes de la caída se entregó después del reinicio.");
    }

    // ----------------------------------------------------------------------
    // 1. Entrega normal.
    // ----------------------------------------------------------------------

    private static async Task EscenarioEntregaNormalAsync(RepositorioPersistente repositorio)
    {
        Console.WriteLine("  1) Entrega normal: la alerta persiste el aviso y el canal lo confirma.");

        await using var despachador = new DespachadorAsincrono(
            repositorio, new RegistroFallos(), consumidores: 2, maxIntentos: 3,
            esperaBase: TimeSpan.FromMilliseconds(50));

        var accion = new Accion("AAPL", PrecioInicial);
        accion.Suscribir(new AlertaUmbral(
            new Cliente("C1", "cliente0001@correo.cu", new CanalEmail()),
            Umbral, Direccion.Sube, despachador));

        accion.ActualizarPrecio(PrecioTrasElSalto);
        await despachador.EsperarVaciadoAsync();

        RegistroOutbox registro = Buscar(repositorio, "cliente0001@correo.cu");
        Console.WriteLine($"  → Outbox: {registro.Contacto} → {registro.Estado} (intentos: {registro.Intentos})\n");
    }

    // ----------------------------------------------------------------------
    // 2. Canal caído: reintentos y dead letter persistido.
    // ----------------------------------------------------------------------

    private static async Task EscenarioCanalCaidoAsync(RepositorioPersistente repositorio)
    {
        Console.WriteLine("  2) Canal caído: se reintenta con backoff y, al agotar los intentos, queda en dead letter.");

        await using var despachador = new DespachadorAsincrono(
            repositorio, new RegistroFallos(), consumidores: 1, maxIntentos: 2,
            esperaBase: TimeSpan.FromMilliseconds(50));

        var accion = new Accion("AAPL", PrecioInicial);
        accion.Suscribir(new AlertaUmbral(
            new Cliente("C2", "+53 50000009", new CanalQueFalla(new CanalSms(), int.MaxValue)),
            Umbral, Direccion.Sube, despachador));

        accion.ActualizarPrecio(PrecioTrasElSalto);
        await despachador.EsperarVaciadoAsync();

        RegistroOutbox registro = Buscar(repositorio, "+53 50000009");
        Console.WriteLine($"  → Outbox: {registro.Contacto} → {registro.Estado} (intentos: {registro.Intentos})");
        Console.WriteLine($"     último error: {registro.UltimoError}\n");
    }

    // ----------------------------------------------------------------------
    // 3. Caída del proceso y reinicio.
    // ----------------------------------------------------------------------

    private static async Task<RepositorioPersistente> EscenarioCaidaYReinicioAsync(
        RepositorioPersistente antes,
        string ruta)
    {
        Console.WriteLine("  3) El proceso se cae con el aviso ya persistido y sin enviar.");

        // Esto es exactamente lo que pasa dentro de Encolar: la alerta detectó
        // el cruce, el aviso quedó escrito en el outbox y el proceso se detuvo
        // antes de que ningún consumidor lo enviara.
        Notificacion aviso = new(
            Guid.NewGuid(),
            new Cliente("C3", "cliente0002@correo.cu", new CanalEmail()),
            $"AAPL subió por encima de su umbral {Umbral.ToString(CultureInfo.InvariantCulture)}: " +
            $"{PrecioInicial.ToString(CultureInfo.InvariantCulture)} → {PrecioTrasElSalto.ToString(CultureInfo.InvariantCulture)}");

        antes.RegistrarPendiente(aviso, intentos: 0);
        RegistroOutbox pendiente = Buscar(antes, "cliente0002@correo.cu");
        Console.WriteLine($"  → Outbox antes de la caída: {pendiente.Contacto} → {pendiente.Estado} (intentos: {pendiente.Intentos})");
        Console.WriteLine("  [CAÍDA] el proceso se detiene.\n");

        Console.WriteLine("  ── Reinicio: otro proceso abre el mismo archivo ──");

        RepositorioPersistente despues = new(ruta);
        IReadOnlyList<EnvioPendiente> pendientes = despues.PendientesParaReintentar();
        Console.WriteLine($"  Pendientes leídos del disco: {pendientes.Count}");

        await using var despachador = new DespachadorAsincrono(
            despues, new RegistroFallos(), consumidores: 2, maxIntentos: 3,
            esperaBase: TimeSpan.FromMilliseconds(50));

        int recuperadas = despachador.RecuperarPendientes();
        Console.WriteLine($"  Avisos recuperados por el despachador: {recuperadas}");

        await despachador.EsperarVaciadoAsync();

        RegistroOutbox recuperado = Buscar(despues, "cliente0002@correo.cu");
        Console.WriteLine($"  → Outbox: {recuperado.Contacto} → {recuperado.Estado} (intentos: {recuperado.Intentos})\n");

        return despues;
    }

    // ----------------------------------------------------------------------
    // 4. Idempotencia.
    // ----------------------------------------------------------------------

    private static async Task EscenarioIdempotenciaAsync(RepositorioPersistente repositorio)
    {
        Console.WriteLine("  4) Idempotencia: el mismo aviso encolado dos veces deja una sola fila en el outbox.");

        await using var despachador = new DespachadorAsincrono(
            repositorio, new RegistroFallos(), consumidores: 1, maxIntentos: 3,
            esperaBase: TimeSpan.FromMilliseconds(50));

        Notificacion aviso = new(
            Guid.NewGuid(),
            new Cliente("C4", "cliente0003@correo.cu", new CanalEmail()),
            "AAPL 151.00 → 155.00");

        int filasAntes = repositorio.Todos().Count;
        despachador.Encolar(aviso);
        despachador.Encolar(aviso);
        await despachador.EsperarVaciadoAsync();

        int filasConEseId = repositorio.Todos().Count(registro => registro.Id == aviso.Id);
        Console.WriteLine($"  → Filas del outbox: {filasAntes} antes, {repositorio.Todos().Count} después.");
        Console.WriteLine($"     Filas con ese mismo Id: {filasConEseId} (el reenvío no duplica el registro).");
        Console.WriteLine("     El cliente puede recibir dos avisos: at-least-once lo permite; el outbox no duplica el registro.\n");
    }

    // ----------------------------------------------------------------------
    // Utilidades.
    // ----------------------------------------------------------------------

    private static RegistroOutbox Buscar(RepositorioPersistente repositorio, string contacto) =>
        repositorio.Todos().First(registro => registro.Contacto == contacto);

    private static void Resumir(RepositorioPersistente repositorio)
    {
        RegistroOutbox[] registros = [.. repositorio.Todos()];
        int entregadas = registros.Count(registro => registro.Estado == EstadoNotificacion.Entregada);
        int pendientes = registros.Count(registro => registro.Estado == EstadoNotificacion.Pendiente);
        int fallidas = registros.Count(registro => registro.Estado == EstadoNotificacion.FalloDefinitivo);

        Console.WriteLine($"     Entregadas: {entregadas}   Pendientes: {pendientes}   Fallo definitivo: {fallidas}");
    }
}
