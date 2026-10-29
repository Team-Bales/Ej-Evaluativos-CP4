namespace Bolsa.Notificaciones.Diagnostico;

/// <summary>
/// Donde se anota un fallo que se aisló en lugar de dejarlo propagar. Existe
/// para que ningún try/catch quede silencioso: la acción lo usa cuando un
/// observador lanza al actualizarse y el despachador cuando un canal rechaza
/// una notificación.
/// </summary>
public interface IRegistroFallos
{
    /// <summary>
    /// Registra un fallo. <paramref name="origen"/> es la clase que lo aisló y
    /// <paramref name="detalle"/> describe qué se estaba haciendo. El error
    /// puede faltar cuando el fallo no viene de una excepción (por ejemplo,
    /// una notificación rechazada porque la cola está llena).
    /// </summary>
    void Registrar(string origen, string detalle, Exception? error = null);
}
