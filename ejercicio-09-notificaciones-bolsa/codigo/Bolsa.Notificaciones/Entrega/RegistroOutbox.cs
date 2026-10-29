namespace Bolsa.Notificaciones.Entrega;

/// <summary>
/// La fila del outbox: la notificación tal y como se puede persistir (el canal
/// no se serializa, solo su tipo) más el estado del envío y el último error.
/// Es el formato que se guarda en el archivo y el que se lee al reiniciar.
/// </summary>
public sealed record RegistroOutbox(
    Guid Id,
    string ClienteId,
    string Contacto,
    string TipoCanal,
    string Mensaje,
    int Intentos,
    EstadoNotificacion Estado,
    string? UltimoError);
