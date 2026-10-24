using System.Collections.Immutable;
using Bolsa.Notificaciones.Observer;

namespace Bolsa.Notificaciones.Demos;

/// <summary>
/// El Subject tal como aparece en el Observer clásico de GoF: el bucle de
/// <c>Notificar</c> llama a <c>Actualizar</c> sin ninguna protección.
///
/// Existe solo para demostrar el problema del inciso c): si un observador
/// lanza una excepción, el bucle se corta ahí y los suscriptores que vienen
/// detrás no reciben nada. El sistema usa <see cref="Accion"/>, que aísla a
/// cada observador. Las dos clases son idénticas salvo en ese try/catch, para
/// que la comparación sea justa.
/// </summary>
public sealed class AccionSinAislamiento : ISujetoPrecio
{
    private ImmutableList<IObservadorPrecio> _observadores = ImmutableList<IObservadorPrecio>.Empty;

    public AccionSinAislamiento(string simbolo, decimal precioInicial)
    {
        Simbolo = simbolo;
        Precio = precioInicial;
    }

    public string Simbolo { get; }

    public decimal Precio { get; private set; }

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
    /// El bucle sin red: la excepción del observador que falla sube por aquí,
    /// sale de <c>ActualizarPrecio</c> y deja sin avisar a los demás.
    /// </summary>
    public void Notificar(CambioPrecio cambio)
    {
        foreach (IObservadorPrecio observador in _observadores)
        {
            observador.Actualizar(cambio);
        }
    }

    public void Suscribir(IObservadorPrecio observador)
    {
        _observadores = _observadores.Add(observador);
    }

    public void Desuscribir(IObservadorPrecio observador)
    {
        _observadores = _observadores.Remove(observador);
    }
}
