namespace Bolsa.Notificaciones.Entrega;

/// <summary>
/// Lo único que la alerta necesita del despachador: dejarle la notificación y
/// seguir. Tener la interfaz permite cambiar la forma de entrega sin tocar el
/// observador (principio Abierto/Cerrado):
/// <list type="bullet">
///   <item><see cref="DespachadorNotificaciones"/> (inciso b) entrega en el
///   mismo hilo que cambió el precio;</item>
///   <item><see cref="DespachadorAsincrono"/> (inciso c) solo encola y entrega
///   en consumidores en segundo plano.</item>
/// </list>
/// </summary>
public interface IDespachadorNotificaciones
{
    /// <summary>Deja la notificación en la cola del despachador.</summary>
    void Encolar(Notificacion notificacion);
}
