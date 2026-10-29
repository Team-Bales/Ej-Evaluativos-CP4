namespace Bolsa.Notificaciones.Entrega;

/// <summary>
/// Decorador que añade un retardo a otro canal, para simular un proveedor
/// lento (una pasarela de SMS que tarda 2 s en responder). Sirve para mostrar
/// el problema del inciso c): con entrega síncrona este retardo lo paga el hilo
/// que cambió el precio y todos los suscriptores que vienen detrás.
/// </summary>
public sealed class CanalLento : ICanalNotificacion
{
    private readonly ICanalNotificacion _interno;
    private readonly TimeSpan _retardo;

    public CanalLento(ICanalNotificacion interno, TimeSpan retardo)
    {
        ArgumentNullException.ThrowIfNull(interno);
        ArgumentOutOfRangeException.ThrowIfLessThan(retardo, TimeSpan.Zero);

        _interno = interno;
        _retardo = retardo;
    }

    public async Task EnviarAsync(Notificacion notificacion)
    {
        await Task.Delay(_retardo);
        await _interno.EnviarAsync(notificacion);
    }
}
