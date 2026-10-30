using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bolsa.Notificaciones.Entrega;

/// <summary>
/// Repositorio persistente del <i>outbox</i> (restricción E9-R). Guarda las
/// notificaciones en un archivo JSON antes de intentar enviarlas y solo las
/// marca como entregadas cuando el canal confirma, de modo que la entrega sea
/// <em>at-least-once</em> incluso entre reinicios.
///
/// <para>
/// A diferencia de <see cref="RepositorioMemoria"/>, aquí la escritura va al
/// disco: si el proceso se cae, lo que estaba en vuelo sigue ahí y se recupera
/// al arrancar. La escritura es atómica (se escribe a un archivo temporal y se
/// renombra), para que un corte a mitad de guardado no deje el archivo roto.
/// </para>
/// </summary>
public sealed class RepositorioPersistente : IRepositorioNotificaciones, IRepositorioOutbox
{
    private static readonly JsonSerializerOptions Opciones = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly object _bloqueo = new();
    private readonly string _ruta;
    private readonly IFabricaCanales _fabrica;
    private readonly Dictionary<Guid, RegistroOutbox> _registros = [];
    private readonly List<Guid> _orden = [];

    /// <summary>Registros cuyo canal no se pudo reconstruir; no se pierden.</summary>
    private readonly List<RegistroOutbox> _noRecuperables = [];

    /// <param name="ruta">Archivo donde se guarda el outbox.</param>
    /// <param name="fabrica">Cómo reconstruir el canal de cada aviso recuperado.</param>
    public RepositorioPersistente(string ruta, IFabricaCanales? fabrica = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruta);

        _ruta = ruta;
        _fabrica = fabrica ?? new FabricaCanales();
        Cargar();
    }

    /// <summary>Archivo donde se guarda el outbox.</summary>
    public string Ruta => _ruta;

    /// <summary>
    /// Registra una notificación entregada. Es lo que llama el despachador
    /// cuando el canal confirma; con esta implementación la marca queda en el
    /// disco.
    /// </summary>
    public void Guardar(Notificacion notificacion)
    {
        ArgumentNullException.ThrowIfNull(notificacion);

        lock (_bloqueo)
        {
            RegistroOutbox? existente = Buscar(notificacion.Id);
            Upsert(new RegistroOutbox(
                notificacion.Id,
                notificacion.Cliente.Id,
                notificacion.Cliente.Contacto,
                notificacion.Cliente.Canal.GetType().Name,
                notificacion.Mensaje,
                existente?.Intentos ?? 0,
                EstadoNotificacion.Entregada,
                existente?.UltimoError));
        }
    }

    /// <summary>
    /// Las notificaciones que siguen sin entregarse. En esta implementación es
    /// el outbox pendiente; en <see cref="RepositorioMemoria"/> son las ya
    /// registradas, porque allí no hay estados.
    /// </summary>
    public IReadOnlyList<Notificacion> Pendientes()
    {
        lock (_bloqueo)
        {
            List<Notificacion> resultado = [];
            foreach (RegistroOutbox registro in _registros.Values)
            {
                if (registro.Estado != EstadoNotificacion.Pendiente)
                {
                    continue;
                }

                Notificacion? notificacion = Reconstruir(registro);
                if (notificacion is not null)
                {
                    resultado.Add(notificacion);
                }
            }

            return resultado;
        }
    }

    /// <inheritdoc />
    public void RegistrarPendiente(Notificacion notificacion, int intentos)
    {
        ArgumentNullException.ThrowIfNull(notificacion);

        lock (_bloqueo)
        {
            RegistroOutbox? existente = Buscar(notificacion.Id);

            // Idempotencia: un aviso ya entregado no se vuelve a abrir. Si el
            // mismo Id llega otra vez, es un reenvío, no una notificación nueva.
            if (existente is not null && existente.Estado == EstadoNotificacion.Entregada)
            {
                return;
            }

            Upsert(new RegistroOutbox(
                notificacion.Id,
                notificacion.Cliente.Id,
                notificacion.Cliente.Contacto,
                notificacion.Cliente.Canal.GetType().Name,
                notificacion.Mensaje,
                intentos,
                EstadoNotificacion.Pendiente,
                existente?.UltimoError));
        }
    }

    /// <inheritdoc />
    public void MarcarEntregada(Guid id)
    {
        lock (_bloqueo)
        {
            RegistroOutbox? registro = Buscar(id);
            if (registro is null || registro.Estado == EstadoNotificacion.Entregada)
            {
                return;
            }

            Upsert(registro with { Estado = EstadoNotificacion.Entregada });
        }
    }

    /// <inheritdoc />
    public void MarcarReintento(Guid id, int intentos, string error)
    {
        lock (_bloqueo)
        {
            RegistroOutbox? registro = Buscar(id);
            if (registro is null)
            {
                return;
            }

            Upsert(registro with
            {
                Estado = EstadoNotificacion.Pendiente,
                Intentos = intentos,
                UltimoError = error,
            });
        }
    }

    /// <inheritdoc />
    public void MarcarFalloDefinitivo(Guid id, int intentos, string error)
    {
        lock (_bloqueo)
        {
            RegistroOutbox? registro = Buscar(id);
            if (registro is null)
            {
                return;
            }

            Upsert(registro with
            {
                Estado = EstadoNotificacion.FalloDefinitivo,
                Intentos = intentos,
                UltimoError = error,
            });
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<EnvioPendiente> PendientesParaReintentar()
    {
        lock (_bloqueo)
        {
            List<EnvioPendiente> resultado = [];
            _noRecuperables.Clear();

            foreach (RegistroOutbox registro in _registros.Values)
            {
                if (registro.Estado != EstadoNotificacion.Pendiente)
                {
                    continue;
                }

                Notificacion? notificacion = Reconstruir(registro);
                if (notificacion is null)
                {
                    // El tipo de canal no se conoce: el aviso no se pierde, queda
                    // en el outbox a la espera de que un operador lo resuelva.
                    _noRecuperables.Add(registro);
                    continue;
                }

                resultado.Add(new EnvioPendiente(notificacion, registro.Intentos));
            }

            return resultado;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<RegistroOutbox> Todos()
    {
        lock (_bloqueo)
        {
            return _orden.Select(id => _registros[id]).ToArray();
        }
    }

    /// <summary>
    /// Los avisos pendientes cuyo canal no se pudo reconstruir. No se pierden:
    /// siguen en el outbox, pero el sistema no puede reencolarlos solo.
    /// </summary>
    public IReadOnlyList<RegistroOutbox> NoRecuperables
    {
        get
        {
            lock (_bloqueo)
            {
                return _noRecuperables.ToArray();
            }
        }
    }

    /// <summary>Reconstruye el aviso a partir de la fila persistida.</summary>
    private Notificacion? Reconstruir(RegistroOutbox registro)
    {
        ICanalNotificacion? canal = _fabrica.Crear(registro.TipoCanal);
        if (canal is null)
        {
            return null;
        }

        return new Notificacion(
            registro.Id,
            new Cliente(registro.ClienteId, registro.Contacto, canal),
            registro.Mensaje);
    }

    private RegistroOutbox? Buscar(Guid id) =>
        _registros.TryGetValue(id, out RegistroOutbox? registro) ? registro : null;

    private void Upsert(RegistroOutbox registro)
    {
        if (!_registros.ContainsKey(registro.Id))
        {
            _orden.Add(registro.Id);
        }

        _registros[registro.Id] = registro;
        Volcar();
    }

    /// <summary>Lee el outbox del disco. Un archivo que no existe es un outbox vacío.</summary>
    private void Cargar()
    {
        if (!File.Exists(_ruta))
        {
            return;
        }

        string json = File.ReadAllText(_ruta);
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        List<RegistroOutbox>? registros;
        try
        {
            registros = JsonSerializer.Deserialize<List<RegistroOutbox>>(json, Opciones);
        }
        catch (JsonException error)
        {
            // Fallar aquí es mejor que seguir como si el outbox estuviera vacío:
            // un archivo corrupto significa Notifications perdidas en silencio.
            throw new InvalidOperationException(
                $"El outbox «{_ruta}» está dañado y no se puede leer. No se descarta: corrígelo o renómbralo.",
                error);
        }

        foreach (RegistroOutbox registro in registros ?? [])
        {
            if (!_registros.ContainsKey(registro.Id))
            {
                _orden.Add(registro.Id);
            }

            _registros[registro.Id] = registro;
        }
    }

    /// <summary>Escribe el outbox en el disco, con reemplazo atómico.</summary>
    private void Volcar()
    {
        string? carpeta = Path.GetDirectoryName(Path.GetFullPath(_ruta));
        if (!string.IsNullOrEmpty(carpeta))
        {
            Directory.CreateDirectory(carpeta);
        }

        List<RegistroOutbox> registros = _orden.Select(id => _registros[id]).ToList();
        string json = JsonSerializer.Serialize(registros, Opciones);

        string temporal = _ruta + ".tmp";
        File.WriteAllText(temporal, json);
        File.Move(temporal, _ruta, overwrite: true);
    }
}
