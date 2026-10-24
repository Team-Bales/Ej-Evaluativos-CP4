using Bolsa.Notificaciones.Entrega;

namespace Bolsa.Notificaciones.Tests;

/// <summary>
/// Canal de prueba: registra las notificaciones en lugar de imprimirlas. Lleva
/// un bloqueo porque desde el inciso c) los envíos pueden venir de varios
/// consumidores en segundo plano a la vez.
/// </summary>
internal sealed class CanalDePrueba : ICanalNotificacion
{
    private readonly object _bloqueo = new();
    private readonly List<Notificacion> _enviadas = [];

    public IReadOnlyList<Notificacion> Enviadas
    {
        get
        {
            lock (_bloqueo)
            {
                return _enviadas.ToArray();
            }
        }
    }

    public Task EnviarAsync(Notificacion notificacion)
    {
        lock (_bloqueo)
        {
            _enviadas.Add(notificacion);
        }

        return Task.CompletedTask;
    }
}
