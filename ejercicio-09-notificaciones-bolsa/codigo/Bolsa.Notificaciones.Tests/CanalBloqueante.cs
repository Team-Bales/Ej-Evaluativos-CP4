using Bolsa.Notificaciones.Entrega;

namespace Bolsa.Notificaciones.Tests;

/// <summary>
/// Canal de prueba que se queda bloqueado dentro del envío hasta que se le
/// suelta. Sirve para comprobar en qué momento exacto la notificación está
/// persistida pero todavía no entregada.
/// </summary>
internal sealed class CanalBloqueante : ICanalNotificacion
{
    private readonly TaskCompletionSource _liberado = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _intentos;

    /// <summary>Veces que se intentó enviar por este canal.</summary>
    public int Intentos => Volatile.Read(ref _intentos);

    public Task EnviarAsync(Notificacion notificacion)
    {
        Interlocked.Increment(ref _intentos);
        return _liberado.Task;
    }

    /// <summary>Deja terminar el envío pendiente.</summary>
    public void Liberar() => _liberado.TrySetResult();
}
