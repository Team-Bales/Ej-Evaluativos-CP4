namespace Bolsa.Notificaciones.Entrega;

/// <summary>
/// Implementación en memoria de <see cref="IRepositorioNotificaciones"/>: la que
/// usa el inciso b). La restricción (E9-R) añadirá una persistente, sin cambiar
/// el despachador, que solo conoce la interfaz.
/// </summary>
public sealed class RepositorioMemoria : IRepositorioNotificaciones
{
    private readonly object _bloqueo = new();
    private readonly List<Notificacion> _notificaciones = [];

    public void Guardar(Notificacion notificacion)
    {
        lock (_bloqueo)
        {
            _notificaciones.Add(notificacion);
        }
    }

    public IReadOnlyList<Notificacion> Pendientes()
    {
        lock (_bloqueo)
        {
            return _notificaciones.ToArray();
        }
    }
}
