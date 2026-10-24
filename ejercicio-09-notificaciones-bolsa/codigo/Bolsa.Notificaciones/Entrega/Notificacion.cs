namespace Bolsa.Notificaciones.Entrega;

/// <summary>
/// El dato que crea la alerta cuando el precio cruza su umbral y que el
/// despachador entrega por el canal del cliente y guarda en el repositorio.
/// </summary>
public sealed record Notificacion(Guid Id, Cliente Cliente, string Mensaje);
