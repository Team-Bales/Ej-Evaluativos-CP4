namespace Bolsa.Notificaciones.Entrega;

/// <summary>Entrega por mensaje de texto. El envío es simulado: se escribe en la consola.</summary>
public sealed class CanalSms : ICanalNotificacion
{
    public Task EnviarAsync(Notificacion notificacion)
    {
        Console.WriteLine($"[SMS → {notificacion.Cliente.Contacto}] {notificacion.Mensaje}");
        return Task.CompletedTask;
    }
}
