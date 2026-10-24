namespace Bolsa.Notificaciones.Observer;

/// <summary>
/// Dirección del cruce que dispara una alerta: cuando el precio sube por
/// encima del umbral (Sube) o cuando baja por debajo (Baja).
/// </summary>
public enum Direccion
{
    Sube,
    Baja,
}
