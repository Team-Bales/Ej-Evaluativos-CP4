namespace Bolsa.Notificaciones.Observer;

/// <summary>
/// Observer (GoF): quien recibe los cambios de precio de una acción. La acción
/// solo conoce esta interfaz; no sabe si detrás hay una alerta de un cliente ni
/// por qué canal se le avisará.
/// </summary>
public interface IObservadorPrecio
{
    /// <summary>Se invoca cada vez que la acción cambia de precio.</summary>
    void Actualizar(CambioPrecio cambio);
}
