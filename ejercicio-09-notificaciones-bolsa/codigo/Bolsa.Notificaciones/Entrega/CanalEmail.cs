namespace Bolsa.Notificaciones.Entrega;

/// <summary>Entrega por correo electrónico. El envío es simulado: se escribe en la consola.</summary>
public sealed class CanalEmail : ICanalNotificacion
{
    public Task EnviarAsync(Notificacion notificacion)
    {
        Console.WriteLine($"[EMAIL → {notificacion.Cliente.Contacto}] {notificacion.Mensaje}");
        return Task.CompletedTask;
    }
}
