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
    private readonly DespachadorNotificaciones _despachador;

    public AlertaUmbral(Cliente cliente, decimal umbral, Direccion direccion, DespachadorNotificaciones despachador)
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

        string verbo = Direccion == Direccion.Sube ? "subió por encima de" : "bajó por debajo de";
        string mensaje = $"{cambio.Simbolo} {verbo} su umbral {Formato(Umbral)}: " +
                         $"{Formato(cambio.PrecioAnterior)} → {Formato(cambio.PrecioNuevo)}";

        _despachador.Encolar(new Notificacion(Guid.NewGuid(), Cliente, mensaje));
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
