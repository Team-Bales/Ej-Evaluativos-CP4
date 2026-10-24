namespace Bolsa.Notificaciones.Entrega;

/// <summary>Entrega por notificación push a la app. El envío es simulado: se escribe en la consola.</summary>
public sealed class CanalPush : ICanalNotificacion
{
    public Task EnviarAsync(Notificacion notificacion)
    {
        Console.WriteLine($"[PUSH → {notificacion.Cliente.Contacto}] {notificacion.Mensaje}");
        return Task.CompletedTask;
    }
}
