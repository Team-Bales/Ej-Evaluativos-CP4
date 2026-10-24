namespace Bolsa.Notificaciones.Entrega;

/// <summary>
/// Strategy (GoF): la forma de entrega es intercambiable. Cada cliente elige
/// el suyo y la acción nunca sabe cómo se entrega el aviso: el observador
/// decide SI hay que avisar y el canal decide CÓMO se entrega.
/// Añadir un canal nuevo (WhatsApp, Telegram, webhook…) no modifica nada del
/// resto del sistema (principio Abierto/Cerrado).
/// </summary>
public interface ICanalNotificacion
{
    /// <summary>Entrega la notificación por este canal.</summary>
    Task EnviarAsync(Notificacion notificacion);
}
