namespace Bolsa.Notificaciones.Entrega;

/// <summary>
/// Lo que viaja por la cola del <see cref="DespachadorAsincrono"/>: la
/// notificación y cuántas veces se intentó enviar ya. El contador va en la cola
/// y no en la notificación porque la notificación es el aviso del cliente, no
/// el estado del envío.
/// </summary>
public sealed record EnvioPendiente(Notificacion Notificacion, int Intentos);
