using Bolsa.Notificaciones.Entrega;

namespace Bolsa.Notificaciones.Tests;

/// <summary>Canal de prueba: registra las notificaciones en lugar de imprimirlas.</summary>
internal sealed class CanalDePrueba : ICanalNotificacion
{
    public List<Notificacion> Enviadas { get; } = [];

    public Task EnviarAsync(Notificacion notificacion)
    {
        Enviadas.Add(notificacion);
        return Task.CompletedTask;
    }
}
