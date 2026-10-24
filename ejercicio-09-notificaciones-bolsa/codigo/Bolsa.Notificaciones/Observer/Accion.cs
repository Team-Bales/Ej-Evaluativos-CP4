using System.Collections.Immutable;

namespace Bolsa.Notificaciones.Observer;

/// <summary>
/// ConcreteSubject (GoF): una cotización de bolsa. Cada acción es un Subject
/// independiente, con su símbolo, su precio y de cero a muchos observadores
/// (las alertas suscritas). No conoce a los clientes ni los canales.
/// </summary>
public sealed class Accion : ISujetoPrecio
{
    private ImmutableList<IObservadorPrecio> _observadores = ImmutableList<IObservadorPrecio>.Empty;

    public Accion(string simbolo, decimal precioInicial)
    {
        Simbolo = simbolo;
        Precio = precioInicial;
    }

    /// <summary>Símbolo bursátil de la acción (p. ej. AAPL).</summary>
    public string Simbolo { get; }

    /// <summary>Último precio negociado.</summary>
    public decimal Precio { get; private set; }

    /// <summary>
    /// Cambia el precio y avisa a todos los observadores suscritos en el mismo
    /// instante. Si el precio no cambió, no hay evento que notificar.
    /// </summary>
    public void ActualizarPrecio(decimal precioNuevo)
    {
        decimal precioAnterior = Precio;
        if (precioNuevo == precioAnterior)
        {
            return;
        }

        Precio = precioNuevo;
        Notificar(new CambioPrecio(Simbolo, precioAnterior, precioNuevo, DateTimeOffset.Now));
    }

    /// <inheritdoc />
    public void Notificar(CambioPrecio cambio)
    {
        foreach (IObservadorPrecio observador in _observadores)
        {
            observador.Actualizar(cambio);
        }
    }

    /// <inheritdoc />
    public void Suscribir(IObservadorPrecio observador)
    {
        _observadores = _observadores.Add(observador);
    }

    /// <inheritdoc />
    public void Desuscribir(IObservadorPrecio observador)
    {
        _observadores = _observadores.Remove(observador);
    }
}
