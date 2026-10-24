namespace Bolsa.Notificaciones.Entrega;

/// <summary>
/// Donde el despachador registra las notificaciones. En este inciso funciona
/// como el registro de las entregadas; la restricción del ejercicio (E9-R)
/// lo usará para persistir también las fallidas y reintentarlas, de modo que
/// la entrega sea at-least-once.
/// </summary>
public interface IRepositorioNotificaciones
{
    /// <summary>Registra una notificación.</summary>
    void Guardar(Notificacion notificacion);

    /// <summary>Notificaciones registradas (en E9-R: las pendientes de reintentar).</summary>
    IReadOnlyList<Notificacion> Pendientes();
}
