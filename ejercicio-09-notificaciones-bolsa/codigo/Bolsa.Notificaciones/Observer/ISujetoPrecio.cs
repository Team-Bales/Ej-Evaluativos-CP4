namespace Bolsa.Notificaciones.Observer;

/// <summary>
/// Subject (GoF): lo que implementa cada acción de bolsa. Permite suscribir y
/// desuscribir observadores en tiempo de ejecución, y notificarles un cambio
/// de precio.
/// </summary>
public interface ISujetoPrecio
{
    /// <summary>Agrega un observador a la lista de suscritos.</summary>
    void Suscribir(IObservadorPrecio observador);

    /// <summary>Quita un observador de la lista; deja de recibir avisos.</summary>
    void Desuscribir(IObservadorPrecio observador);

    /// <summary>Entrega el cambio de precio a todos los observadores suscritos (modelo push).</summary>
    void Notificar(CambioPrecio cambio);
}
