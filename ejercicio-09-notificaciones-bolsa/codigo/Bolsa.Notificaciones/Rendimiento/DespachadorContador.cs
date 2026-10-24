using Bolsa.Notificaciones.Entrega;

namespace Bolsa.Notificaciones.Rendimiento;

/// <summary>
/// Despachador que solo cuenta. Sirve para medir por separado las dos fases del
/// inciso d): lo que cuesta <b>detectar</b> qué clientes hay que avisar (que es
/// donde se ve el orden de complejidad) y lo que cuesta <b>entregar</b> los
/// avisos (que es trabajo proporcional a los notificados, se haga como se haga).
/// </summary>
public sealed class DespachadorContador : IDespachadorNotificaciones
{
    private int _encoladas;

    /// <summary>Notificaciones que se habrían enviado.</summary>
    public int Encoladas => Volatile.Read(ref _encoladas);

    public void Encolar(Notificacion notificacion) => Interlocked.Increment(ref _encoladas);
}
