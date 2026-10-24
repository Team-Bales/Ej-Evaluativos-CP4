namespace Bolsa.Notificaciones.Entrega;

/// <summary>
/// Un cliente de la plataforma: su identificador, su contacto y el canal por
/// el que quiere recibir los avisos (email, SMS o push). Un cliente puede
/// tener muchas alertas, sobre una o varias acciones.
/// </summary>
public sealed record Cliente(string Id, string Contacto, ICanalNotificacion Canal);
