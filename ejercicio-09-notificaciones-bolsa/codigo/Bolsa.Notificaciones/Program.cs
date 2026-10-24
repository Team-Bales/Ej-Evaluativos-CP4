using Bolsa.Notificaciones.Entrega;
using Bolsa.Notificaciones.Observer;

// ===========================================================================
// Ejercicio 9, inciso b): simulación con 5 acciones, 1000 clientes con
// umbrales personalizados y notificaciones por email, SMS y push.
// La semilla del generador de precios es fija: la simulación es reproducible
// (los mismos precios y los mismos avisos en cada ejecución).
// ===========================================================================

const int NumeroClientes = 1000;
const int NumeroAcciones = 5;
const int NumeroTicks = 10;
const int ClientesADesuscribir = 25;       // se desuscriben tras el tick 8
const decimal VolatilidadPorTick = 0.03m;  // ±1,5 % por tick en la fase normal

// Las 5 acciones: cada una es un Subject independiente con su propia lista de
// observadores, tal como pide el enunciado.
Accion[] acciones =
[
    new("AAPL", 150.00m),
    new("MSFT", 310.00m),
    new("GOOG", 140.00m),
    new("AMZN", 180.00m),
    new("TSLA", 250.00m),
];

// El último tick simula un evento de mercado: saltos fuertes que garantizan
// cruces de umbral en ambas direcciones. Los umbrales alejados más del 15 %
// no se alcanzan: no todos los clientes reciben aviso, solo los suyos.
Dictionary<string, decimal> shockFinal = new()
{
    ["AAPL"] = 9.00m,
    ["MSFT"] = -7.00m,
    ["GOOG"] = 12.00m,
    ["AMZN"] = -5.00m,
    ["TSLA"] = 8.00m,
};

var repositorio = new RepositorioMemoria();
var despachador = new DespachadorNotificaciones(repositorio);
ICanalNotificacion[] canales = [new CanalEmail(), new CanalSms(), new CanalPush()];

// --------------------------------------------------------------------------
// 1. Los 1000 clientes: canal por reparto (i % 3) y 1 o 2 acciones por cliente.
// --------------------------------------------------------------------------
Cliente[] clientes = Enumerable.Range(1, NumeroClientes)
    .Select(i =>
    {
        ICanalNotificacion canal = canales[i % canales.Length];
        return new Cliente($"C{i:D4}", Contacto(i, canal), canal);
    })
    .ToArray();

// --------------------------------------------------------------------------
// 2. Las suscripciones: cada alerta es un observador con umbral propio.
// El umbral es único dentro de cada par (acción, dirección): precio inicial
// ± (0,1 % + 0,12 % por orden de llegada). Así ningún cliente comparte umbral
// con otro sobre la misma acción y dirección, y ninguno coincide con el precio
// inicial: un aviso exige un cruce real.
// --------------------------------------------------------------------------
var umbralesAsignados = new Dictionary<(string Simbolo, Direccion Direccion), int>();
var suscripciones = new List<(AlertaUmbral Alerta, Accion Accion)>(2 * NumeroClientes);

foreach ((Cliente cliente, int i) in clientes.Select((c, i) => (c, i)))
{
    int numeroSuscripciones = 1 + i % 2;

    for (int k = 0; k < numeroSuscripciones; k++)
    {
        Accion accion = acciones[(i + 2 * k) % NumeroAcciones];
        Direccion direccion = (i + k) % 2 == 0 ? Direccion.Sube : Direccion.Baja;

        int orden = umbralesAsignados.GetValueOrDefault((accion.Simbolo, direccion));
        umbralesAsignados[(accion.Simbolo, direccion)] = orden + 1;

        decimal porcentaje = 0.1m + 0.12m * orden;
        decimal umbral = Math.Round(
            direccion == Direccion.Sube
                ? accion.Precio * (1 + porcentaje / 100m)
                : accion.Precio * (1 - porcentaje / 100m),
            2);

        AlertaUmbral alerta = new(cliente, umbral, direccion, despachador);
        accion.Suscribir(alerta);
        suscripciones.Add((alerta, accion));
    }
}

Console.WriteLine($"{suscripciones.Count} suscripciones de {NumeroClientes} clientes sobre {NumeroAcciones} acciones.");
Console.WriteLine("Cada línea [CANAL → contacto] es una notificación entregada porque su umbral se cruzó.");

// --------------------------------------------------------------------------
// 3. Simulación de 10 ticks. Los avisos se imprimen debajo de la línea de la
// acción que los dispara: se notifica en el mismo tick en que ocurre el cruce.
// --------------------------------------------------------------------------
var aleatorio = new Random(20261016); // semilla fija: simulación reproducible
int avisosTotales = 0;
Dictionary<string, int> avisosPorAccion = acciones.ToDictionary(a => a.Simbolo, _ => 0);
HashSet<string> idsDesuscritos = [];
int avisosDeDesuscritosAntes = 0;

for (int tick = 1; tick <= NumeroTicks; tick++)
{
    Console.WriteLine($"\n――― Tick {tick} ―――");

    foreach (Accion accion in acciones)
    {
        decimal precioAnterior = accion.Precio;
        decimal precioNuevo = tick == NumeroTicks
            ? Math.Round(precioAnterior * (1 + shockFinal[accion.Simbolo] / 100m), 2)
            : Math.Round(precioAnterior * (1 + (decimal)(aleatorio.NextDouble() - 0.5) * VolatilidadPorTick), 2);

        Console.WriteLine($"  {accion.Simbolo}: {AlertaUmbral.Formato(precioAnterior)} → {AlertaUmbral.Formato(precioNuevo)}");

        int avisosAntes = repositorio.Pendientes().Count;
        accion.ActualizarPrecio(precioNuevo);
        int avisos = repositorio.Pendientes().Count - avisosAntes;
        avisosTotales += avisos;
        avisosPorAccion[accion.Simbolo] += avisos;
    }

    // Después del tick 8, 25 clientes se desuscriben de todas sus alertas:
    // los ticks 9 y 10 deben mostrar que ya no reciben avisos.
    if (tick == NumeroTicks - 2)
    {
        idsDesuscritos = clientes.Take(ClientesADesuscribir).Select(c => c.Id).ToHashSet();
        int alertasQuitadas = 0;

        foreach ((AlertaUmbral alerta, Accion accion) in suscripciones)
        {
            if (!idsDesuscritos.Contains(alerta.Cliente.Id))
            {
                continue;
            }

            accion.Desuscribir(alerta);
            alertasQuitadas++;
        }

        avisosDeDesuscritosAntes = repositorio.Pendientes().Count(n => idsDesuscritos.Contains(n.Cliente.Id));
        Console.WriteLine($"\n  … {ClientesADesuscribir} clientes se desuscriben ({alertasQuitadas} alertas quitadas).");
    }
}

// --------------------------------------------------------------------------
// 4. Resumen: qué se notificó, por dónde, y las verificaciones del inciso.
// --------------------------------------------------------------------------
IReadOnlyList<Notificacion> entregadas = repositorio.Pendientes();
int avisosDeDesuscritosDespues = entregadas.Count(n => idsDesuscritos.Contains(n.Cliente.Id)) - avisosDeDesuscritosAntes;

bool umbralesUnicos = suscripciones
    .Select(s => (s.Accion.Simbolo, s.Alerta.Direccion, s.Alerta.Umbral))
    .Distinct()
    .Count() == suscripciones.Count;

Console.WriteLine("\n═══════════════════ RESUMEN ═══════════════════");
Console.WriteLine($"Notificaciones entregadas: {avisosTotales} (de {suscripciones.Count} alertas suscritas).");
Console.WriteLine($"Clientes notificados: {entregadas.Select(n => n.Cliente.Id).Distinct().Count()} de {NumeroClientes}.");

foreach ((string canal, int cantidad) in entregadas
    .GroupBy(n => n.Cliente.Canal.GetType().Name)
    .Select(g => (Canal: NombreDeCanal(g.Key), Cantidad: g.Count()))
    .OrderBy(t => t.Canal))
{
    Console.WriteLine($"  • por {canal}: {cantidad} avisos.");
}

foreach (Accion accion in acciones)
{
    Console.WriteLine($"  • {accion.Simbolo}: {avisosPorAccion[accion.Simbolo]} avisos (precio final {AlertaUmbral.Formato(accion.Precio)}).");
}

Console.WriteLine($"Desuscripción: los {ClientesADesuscribir} clientes desuscritos recibieron {avisosDeDesuscritosAntes} avisos antes y {avisosDeDesuscritosDespues} después (debe ser 0).");
Console.WriteLine($"Umbrales personalizados: {(umbralesUnicos ? "todos distintos" : "¡HAY REPETIDOS!")} ({suscripciones.Count} alertas).");
Console.WriteLine($"Repositorio: {entregadas.Count} notificaciones persistidas (base de la entrega at-least-once de la restricción E9-R).");

// ---------------------------------------------------------------------------
// Funciones locales
// ---------------------------------------------------------------------------

static string Contacto(int numero, ICanalNotificacion canal) => canal switch
{
    CanalEmail => $"cliente{numero:D4}@correo.cu",
    CanalSms => $"+53 5{numero:D7}",
    CanalPush => $"dispositivo-{numero:D4}",
    _ => $"contacto-{numero:D4}",
};

static string NombreDeCanal(string tipo) => tipo switch
{
    nameof(CanalEmail) => "email",
    nameof(CanalSms) => "SMS",
    nameof(CanalPush) => "push",
    _ => tipo,
};
