using Bolsa.Notificaciones.Entrega;

namespace Bolsa.Notificaciones.Rendimiento;

/// <summary>
/// Repositorio que solo lleva la cuenta de lo entregado. Con un millón de
/// clientes, guardar cada notificación en una lista mediría la memoria de la
/// lista en lugar de la de las suscripciones, que es lo que interesa en el
/// inciso d). Por eso <see cref="Pendientes"/> devuelve una lista vacía: esta
/// implementación es solo para medir.
/// </summary>
public sealed class RepositorioContador : IRepositorioNotificaciones
{
    private int _guardadas;

    /// <summary>Notificaciones registradas como entregadas.</summary>
    public int Guardadas => Volatile.Read(ref _guardadas);

    public void Guardar(Notificacion notificacion) => Interlocked.Increment(ref _guardadas);

    public IReadOnlyList<Notificacion> Pendientes() => [];
}
