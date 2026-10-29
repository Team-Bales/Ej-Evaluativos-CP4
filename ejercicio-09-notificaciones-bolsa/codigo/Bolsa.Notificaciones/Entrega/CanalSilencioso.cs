namespace Bolsa.Notificaciones.Entrega;

/// <summary>
/// Canal que no escribe nada: solo cuenta las entregas. Lo usan las mediciones
/// de los incisos c) y d), porque escribir en la consola cuesta más que el
/// propio envío simulado y falsearía los tiempos. Envuelto en
/// <see cref="CanalLento"/> sirve además para simular un proveedor lento sin
/// llenar la pantalla.
/// </summary>
public sealed class CanalSilencioso : ICanalNotificacion
{
    private int _entregas;

    /// <summary>Notificaciones entregadas por este canal.</summary>
    public int Entregas => Volatile.Read(ref _entregas);

    public Task EnviarAsync(Notificacion notificacion)
    {
        Interlocked.Increment(ref _entregas);
        return Task.CompletedTask;
    }
}
