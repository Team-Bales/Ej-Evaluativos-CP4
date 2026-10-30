namespace Bolsa.Notificaciones.Entrega;

/// <summary>
/// Estado durable de una notificación en el outbox (restricción E9-R). Es lo
/// que permite distinguir un aviso que todavía no se ha enviado de uno que ya
/// confirmó el canal, y de uno que se dio por perdido tras agotar los intentos.
/// </summary>
public enum EstadoNotificacion
{
    /// <summary>Persistida y a la espera de que un canal la confirme.</summary>
    Pendiente,

    /// <summary>Un canal confirmó el envío: se puede borrar del outbox.</summary>
    Entregada,

    /// <summary>
    /// Se agotaron los intentos: quedó en <i>dead letter</i>. No se borra,
    /// para que la notificación no se pierda y un operador pueda reintentarla.
    /// </summary>
    FalloDefinitivo,
}
