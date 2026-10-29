using System.Threading.Channels;

namespace Bolsa.Notificaciones.Entrega;

/// <summary>
/// Separa la detección del evento (la alerta decide si hay que avisar) del
/// envío real (entregar por el canal y registrar). Las alertas solo encolan.
///
/// En este inciso b) la entrega es inmediata y síncrona: Encolar procesa la
/// cola al momento, para que la simulación sea determinista y las pruebas no
/// dependan de hilos en segundo plano. La Channel queda ya como la cola del
/// despachador; el inciso c) convierte el envío en asíncrono con
/// <see cref="DespachadorAsincrono"/> y la restricción (E9-R) usará el
/// repositorio para la entrega at-least-once con reintentos.
/// </summary>
public sealed class DespachadorNotificaciones : IDespachadorNotificaciones
{
    private readonly Channel<Notificacion> _cola =
        Channel.CreateUnbounded<Notificacion>(new UnboundedChannelOptions { SingleReader = true });

    private readonly IRepositorioNotificaciones _repositorio;

    public DespachadorNotificaciones(IRepositorioNotificaciones repositorio)
    {
        _repositorio = repositorio;
    }

    /// <summary>
    /// Encola una notificación y la entrega: primero por el canal del cliente,
    /// como muestra el diagrama, y después la guarda en el repositorio.
    /// </summary>
    public void Encolar(Notificacion notificacion)
    {
        _cola.Writer.TryWrite(notificacion);

        while (_cola.Reader.TryRead(out Notificacion? pendiente))
        {
            pendiente.Cliente.Canal.EnviarAsync(pendiente).GetAwaiter().GetResult();
            _repositorio.Guardar(pendiente);
        }
    }
}
