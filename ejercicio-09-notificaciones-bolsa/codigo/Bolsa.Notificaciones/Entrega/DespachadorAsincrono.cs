using System.Threading.Channels;
using Bolsa.Notificaciones.Diagnostico;

namespace Bolsa.Notificaciones.Entrega;

/// <summary>
/// Despachador asíncrono del inciso c). La alerta solo deja la notificación en
/// una cola acotada y regresa; el envío real lo hacen varios consumidores en
/// segundo plano. Así el hilo que cambió el precio no espera a ningún canal, y
/// un canal lento (SMS de 2 s) no retrasa a los demás suscriptores.
///
/// Cada envío va aislado en su propio try/catch: si el canal falla, el error se
/// registra, la notificación se reencola con espera creciente (backoff) y, al
/// agotar <see cref="MaxIntentos"/>, pasa a fallo definitivo (<i>dead
/// letter</i>). Aquí todo eso vive en memoria; la restricción E9-R lo persiste
/// para garantizar la entrega at-least-once.
/// </summary>
public sealed class DespachadorAsincrono : IDespachadorNotificaciones, IAsyncDisposable
{
    private readonly Channel<EnvioPendiente> _cola;
    private readonly IRepositorioNotificaciones _repositorio;
    private readonly IRegistroFallos _registro;
    private readonly Task[] _consumidores;
    private readonly TimeSpan _esperaBase;
    private readonly object _bloqueo = new();
    private readonly List<Notificacion> _fallosDefinitivos = [];
    private readonly List<Task> _temporizadoresPendientes = [];

    private int _enProceso;
    private int _entregadas;
    private int _fallosDeEnvio;
    private int _reintentos;
    private int _rechazadas;

    /// <param name="repositorio">Donde se registra cada notificación entregada.</param>
    /// <param name="registro">Donde se anota cada fallo aislado.</param>
    /// <param name="consumidores">Hilos de envío en segundo plano.</param>
    /// <param name="capacidad">Tamaño máximo de la cola; al llenarse se rechaza (inciso d).</param>
    /// <param name="maxIntentos">Intentos por notificación antes del fallo definitivo.</param>
    /// <param name="esperaBase">Espera del primer reintento; se duplica en cada uno.</param>
    public DespachadorAsincrono(
        IRepositorioNotificaciones repositorio,
        IRegistroFallos? registro = null,
        int consumidores = 4,
        int capacidad = 10_000,
        int maxIntentos = 3,
        TimeSpan? esperaBase = null)
    {
        ArgumentNullException.ThrowIfNull(repositorio);
        ArgumentOutOfRangeException.ThrowIfLessThan(consumidores, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(capacidad, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxIntentos, 1);

        _repositorio = repositorio;
        _registro = registro ?? new RegistroFallos();
        _esperaBase = esperaBase ?? TimeSpan.FromMilliseconds(50);
        Capacidad = capacidad;
        MaxIntentos = maxIntentos;

        _cola = Channel.CreateBounded<EnvioPendiente>(new BoundedChannelOptions(capacidad)
        {
            // La cola acotada es la pieza del backpressure: cuando se llena hay
            // que decidir qué pasa. Aquí siempre se usa TryWrite (Encolar y
            // Reprogramar), que nunca espera: devuelve false y el llamador
            // rechaza o descarta. FullMode.Wait solo afecta a WriteAsync y a
            // WaitToWriteAsync, que no usamos; como nunca escribimos con
            // WriteAsync, este modo no se activa en ningún camino.
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false,
        });

        _consumidores = Enumerable.Range(0, consumidores)
            .Select(_ => Task.Run(ConsumirAsync))
            .ToArray();
    }

    /// <summary>Tamaño máximo de la cola.</summary>
    public int Capacidad { get; }

    /// <summary>Intentos por notificación antes de darla por perdida.</summary>
    public int MaxIntentos { get; }

    /// <summary>Consumidores en segundo plano que hacen los envíos.</summary>
    public int Consumidores => _consumidores.Length;

    /// <summary>Notificaciones entregadas por su canal.</summary>
    public int Entregadas => Volatile.Read(ref _entregadas);

    /// <summary>Envíos que lanzaron una excepción, contando los reintentos.</summary>
    public int FallosDeEnvio => Volatile.Read(ref _fallosDeEnvio);

    /// <summary>Veces que una notificación se reencoló tras fallar.</summary>
    public int Reintentos => Volatile.Read(ref _reintentos);

    /// <summary>Notificaciones rechazadas porque la cola estaba llena.</summary>
    public int Rechazadas => Volatile.Read(ref _rechazadas);

    /// <summary>Notificaciones encoladas o en vuelo, todavía sin desenlace.</summary>
    public int EnProceso => Volatile.Read(ref _enProceso);

    /// <summary>Notificaciones que agotaron los intentos (<i>dead letter</i>).</summary>
    public IReadOnlyList<Notificacion> FallosDefinitivos
    {
        get
        {
            lock (_bloqueo)
            {
                return _fallosDefinitivos.ToArray();
            }
        }
    }

    /// <summary>
    /// Deja la notificación en la cola y regresa de inmediato: es lo único que
    /// paga el hilo que cambió el precio. Si la cola está llena se rechaza y se
    /// registra, porque esperar aquí sería volver a bloquear al sujeto.
    /// </summary>
    public void Encolar(Notificacion notificacion)
    {
        ArgumentNullException.ThrowIfNull(notificacion);

        Interlocked.Increment(ref _enProceso);
        if (_cola.Writer.TryWrite(new EnvioPendiente(notificacion, Intentos: 0)))
        {
            return;
        }

        Interlocked.Decrement(ref _enProceso);
        Interlocked.Increment(ref _rechazadas);
        _registro.Registrar(
            nameof(DespachadorAsincrono),
            $"cola llena ({Capacidad}); se rechaza el aviso de {notificacion.Cliente.Contacto}");
    }

    /// <summary>
    /// Espera a que la cola se vacíe. Solo se usa para cerrar la simulación y
    /// las pruebas de forma ordenada; no está en el camino de la notificación.
    /// </summary>
    public async Task EsperarVaciadoAsync(TimeSpan? limite = null)
    {
        DateTimeOffset tope = DateTimeOffset.UtcNow + (limite ?? TimeSpan.FromSeconds(30));

        while (EnProceso > 0 && DateTimeOffset.UtcNow < tope)
        {
            await Task.Delay(5);
        }
    }

    /// <summary>
    /// Cierra la cola, espera a que se resuelvan los reintentos programados y
    /// luego a que los consumidores terminen. Con la cola cerrada, un reintento
    /// que no puede reencolarse se registra y pasa a fallo definitivo en vez de
    /// quedar en el aire.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        _cola.Writer.TryComplete();

        // Resuelve primero los temporizadores de reintento pendientes: con la
        // cola cerrada, su TryWrite falla, se registra y pasan a dead letter.
        // Después los consumidores drenan lo que quede en vuelo y salen.
        Task[] temporizadores;
        lock (_bloqueo)
        {
            temporizadores = _temporizadoresPendientes.ToArray();
        }
        await Task.WhenAll(temporizadores);

        await Task.WhenAll(_consumidores);
    }

    private async Task ConsumirAsync()
    {
        await foreach (EnvioPendiente envio in _cola.Reader.ReadAllAsync())
        {
            await IntentarEnviarAsync(envio);
        }
    }

    private async Task IntentarEnviarAsync(EnvioPendiente envio)
    {
        try
        {
            await envio.Notificacion.Cliente.Canal.EnviarAsync(envio.Notificacion);
            _repositorio.Guardar(envio.Notificacion);
            Interlocked.Increment(ref _entregadas);
            Interlocked.Decrement(ref _enProceso);
        }
        catch (Exception error)
        {
            // El fallo de un cliente no puede tumbar al consumidor ni impedir
            // que los siguientes de la cola se envíen.
            Interlocked.Increment(ref _fallosDeEnvio);
            int intentos = envio.Intentos + 1;
            _registro.Registrar(
                nameof(DespachadorAsincrono),
                $"intento {intentos}/{MaxIntentos} del aviso de {envio.Notificacion.Cliente.Contacto}",
                error);

            if (intentos >= MaxIntentos)
            {
                DarPorPerdida(envio.Notificacion);
                return;
            }

            Reprogramar(envio with { Intentos = intentos });
        }
    }

    /// <summary>
    /// Reencola la notificación tras una espera que se duplica en cada intento
    /// (50 ms, 100 ms, 200 ms…). La espera no bloquea al consumidor: se
    /// programa y el consumidor sigue con el resto de la cola.
    /// </summary>
    private void Reprogramar(EnvioPendiente envio)
    {
        Interlocked.Increment(ref _reintentos);
        TimeSpan espera = _esperaBase * Math.Pow(2, envio.Intentos - 1);

        Task temporizador = Task.Delay(espera).ContinueWith(
            _ =>
            {
                if (!_cola.Writer.TryWrite(envio))
                {
                    // La cola está llena o se cerró al disponer el despachador:
                    // se registra antes de pasar el aviso a fallo definitivo,
                    // nunca en silencio.
                    _registro.Registrar(
                        nameof(DespachadorAsincrono),
                        $"no se pudo reencolar el aviso de {envio.Notificacion.Cliente.Contacto}; la cola está llena o cerrada, se descarta");

                    DarPorPerdida(envio.Notificacion);
                }
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        lock (_bloqueo)
        {
            _temporizadoresPendientes.Add(temporizador);
        }

        _ = temporizador.ContinueWith(
            _ =>
            {
                lock (_bloqueo)
                {
                    _temporizadoresPendientes.Remove(temporizador);
                }
            });
    }

    private void DarPorPerdida(Notificacion notificacion)
    {
        lock (_bloqueo)
        {
            _fallosDefinitivos.Add(notificacion);
        }

        Interlocked.Decrement(ref _enProceso);
    }
}
