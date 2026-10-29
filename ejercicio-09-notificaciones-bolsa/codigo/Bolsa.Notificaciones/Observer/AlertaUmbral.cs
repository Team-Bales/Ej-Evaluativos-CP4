using System.Globalization;
using Bolsa.Notificaciones.Entrega;

namespace Bolsa.Notificaciones.Observer;

/// <summary>
/// ConcreteObserver (GoF): una suscripción de un cliente a una acción. Cada
/// objeto es una alerta con una sola responsabilidad: decidir si el cambio de
/// precio cruza SU umbral en SU dirección y, si lo cruza, crear la
/// Notificacion y encolarla en el despachador. Por eso el umbral vive aquí y
/// no en la acción, que no necesita saber qué le interesa a cada cliente.
/// </summary>
public sealed class AlertaUmbral : IObservadorPrecio
{
    private readonly IDespachadorNotificaciones _despachador;

    /// <param name="despachador">
    /// La alerta solo encola, así que depende de la interfaz y no de una
    /// implementación: el inciso b) le pasa el despachador síncrono y el
    /// inciso c) el asíncrono, sin cambiar nada de esta clase.
    /// </param>
    public AlertaUmbral(Cliente cliente, decimal umbral, Direccion direccion, IDespachadorNotificaciones despachador)
    {
        Cliente = cliente;
        Umbral = umbral;
        Direccion = direccion;
        _despachador = despachador;
    }

    /// <summary>Cliente dueño de la alerta; la notificación se entrega por su canal.</summary>
    public Cliente Cliente { get; }

    /// <summary>Precio que dispara la alerta.</summary>
    public decimal Umbral { get; }

    /// <summary>Dirección del cruce: al alza (Sube) o a la baja (Baja).</summary>
    public Direccion Direccion { get; }

    /// <summary>
    /// Regla de disparo: solo hay aviso cuando el precio CRUZA el umbral, no en
    /// cada tick mientras siga del lado contrario. Sube: el precio pasa de
    /// menor o igual que el umbral a mayor. Baja: pasa de mayor o igual a menor.
    /// </summary>
    public void Actualizar(CambioPrecio cambio)
    {
        if (!CruzaElUmbral(cambio))
        {
            return;
        }

        _despachador.Encolar(new Notificacion(Guid.NewGuid(), Cliente, Mensaje(cambio, Umbral, Direccion)));
    }

    /// <summary>
    /// Texto del aviso. Está aquí, y no dentro de <see cref="Actualizar"/>,
    /// porque el índice ordenado del inciso d) genera exactamente el mismo
    /// mensaje sin pasar por el observador: así las dos versiones son
    /// comparables.
    /// </summary>
    internal static string Mensaje(CambioPrecio cambio, decimal umbral, Direccion direccion)
    {
        string verbo = direccion == Direccion.Sube ? "subió por encima de" : "bajó por debajo de";

        return $"{cambio.Simbolo} {verbo} su umbral {Formato(umbral)}: " +
               $"{Formato(cambio.PrecioAnterior)} → {Formato(cambio.PrecioNuevo)}";
    }

    private bool CruzaElUmbral(CambioPrecio cambio) => Direccion switch
    {
        Direccion.Sube => cambio.PrecioAnterior <= Umbral && cambio.PrecioNuevo > Umbral,
        Direccion.Baja => cambio.PrecioAnterior >= Umbral && cambio.PrecioNuevo < Umbral,
        _ => false,
    };

    /// <summary>Formato fijo de precios e umbrales, independiente de la cultura del equipo.</summary>
    internal static string Formato(decimal precio) => precio.ToString("0.00", CultureInfo.InvariantCulture);
}
