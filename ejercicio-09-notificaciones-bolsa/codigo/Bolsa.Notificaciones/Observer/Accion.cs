using System.Collections.Immutable;
using Bolsa.Notificaciones.Diagnostico;

namespace Bolsa.Notificaciones.Observer;

/// <summary>
/// ConcreteSubject (GoF): una cotización de bolsa. Cada acción es un Subject
/// independiente, con su símbolo, su precio y de cero a muchos observadores
/// (las alertas suscritas). No conoce a los clientes ni los canales.
/// </summary>
public sealed class Accion : ISujetoPrecio
{
    private ImmutableList<IObservadorPrecio> _observadores = ImmutableList<IObservadorPrecio>.Empty;
    private readonly IRegistroFallos _registro;

    /// <param name="simbolo">Símbolo bursátil.</param>
    /// <param name="precioInicial">Precio con el que empieza a cotizar.</param>
    /// <param name="registro">
    /// Donde se anotan los observadores que fallan al actualizarse (inciso c).
    /// Si no se indica, los fallos se escriben en la consola: nunca se callan.
    /// </param>
    public Accion(string simbolo, decimal precioInicial, IRegistroFallos? registro = null)
    {
        Simbolo = simbolo;
        Precio = precioInicial;
        _registro = registro ?? new RegistroFallos();
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

    /// <summary>
    /// Entrega el cambio a todos los observadores, aislando a cada uno
    /// (inciso c). En el Observer clásico de GoF el bucle llama a
    /// <c>Actualizar</c> sin protección: la primera excepción lo corta y los
    /// suscriptores que vienen detrás se quedan sin su aviso. Aquí el fallo de
    /// una alerta se registra y el recorrido continúa, porque un cliente que no
    /// se puede notificar no debe silenciar a los otros 999.
    /// </summary>
    public void Notificar(CambioPrecio cambio)
    {
        foreach (IObservadorPrecio observador in _observadores)
        {
            try
            {
                observador.Actualizar(cambio);
            }
            catch (Exception error)
            {
                _registro.Registrar(
                    nameof(Accion),
                    $"{Simbolo}: un observador falló al actualizarse y se omitió",
                    error);
            }
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
