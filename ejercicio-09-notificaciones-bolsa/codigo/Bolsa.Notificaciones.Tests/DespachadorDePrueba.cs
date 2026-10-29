using Bolsa.Notificaciones.Entrega;

namespace Bolsa.Notificaciones.Tests;

/// <summary>
/// Despachador de prueba: guarda lo encolado sin entregarlo, para poder
/// comparar qué notificaciones habría producido cada implementación.
/// </summary>
internal sealed class DespachadorDePrueba : IDespachadorNotificaciones
{
    private readonly object _bloqueo = new();
    private readonly List<Notificacion> _encoladas = [];

    public IReadOnlyList<Notificacion> Encoladas
    {
        get
        {
            lock (_bloqueo)
            {
                return _encoladas.ToArray();
            }
        }
    }

    /// <summary>
    /// Las notificaciones como texto comparable y en orden estable. No incluye
    /// el <c>Id</c>, que es un Guid nuevo en cada notificación.
    /// </summary>
    public IReadOnlyList<string> Resumen() =>
        Encoladas.Select(n => $"{n.Cliente.Id}|{n.Mensaje}").Order(StringComparer.Ordinal).ToArray();

    public void Encolar(Notificacion notificacion)
    {
        lock (_bloqueo)
        {
            _encoladas.Add(notificacion);
        }
    }
}
