using Bolsa.Notificaciones.Demos;
using Bolsa.Notificaciones.Entrega;
using Bolsa.Notificaciones.Observer;

namespace Bolsa.Notificaciones.Tests;

/// <summary>
/// Inciso c), primera pregunta: qué pasa si un cliente no puede ser notificado.
/// Las dos primeras pruebas son la misma escena —tres alertas y la del medio
/// con un canal que siempre falla— sobre el Subject clásico y sobre el que
/// aísla a cada observador.
/// </summary>
public sealed class AislamientoDeFallosTests
{
    private const decimal PrecioInicial = 150.00m;
    private const decimal PrecioTrasElSalto = 155.00m;
    private const decimal Umbral = 151.00m;

    [Fact]
    public void ElBucleClasicoSeCorta_CuandoUnObservadorFalla()
    {
        var despachador = new DespachadorNotificaciones(new RepositorioMemoria());
        var accion = new AccionSinAislamiento("AAPL", PrecioInicial);
        (CanalDePrueba primero, CanalDePrueba ultimo) = SuscribirTresAlertas(accion, despachador);

        // La excepción del cliente del medio sale del propio ActualizarPrecio.
        Assert.Throws<InvalidOperationException>(() => accion.ActualizarPrecio(PrecioTrasElSalto));

        Assert.Single(primero.Enviadas); // el anterior al fallo sí recibió
        Assert.Empty(ultimo.Enviadas);   // el posterior se quedó sin aviso
    }

    [Fact]
    public void UnClienteNoNotificableNoImpideQueLosDemasReciban()
    {
        RegistroDePrueba registro = new();
        var despachador = new DespachadorNotificaciones(new RepositorioMemoria());
        var accion = new Accion("AAPL", PrecioInicial, registro);
        (CanalDePrueba primero, CanalDePrueba ultimo) = SuscribirTresAlertas(accion, despachador);

        accion.ActualizarPrecio(PrecioTrasElSalto); // no lanza: el fallo está aislado

        Assert.Single(primero.Enviadas);
        Assert.Single(ultimo.Enviadas);
        Assert.Single(registro.Fallos); // y el fallo no se silenció
    }

    [Fact]
    public void ElPrecioSeActualizaAunqueUnObservadorFalle()
    {
        var despachador = new DespachadorNotificaciones(new RepositorioMemoria());
        var accion = new Accion("AAPL", PrecioInicial, new RegistroDePrueba());
        SuscribirTresAlertas(accion, despachador);

        accion.ActualizarPrecio(PrecioTrasElSalto);

        Assert.Equal(PrecioTrasElSalto, accion.Precio);
    }

    /// <summary>
    /// Suscribe tres alertas con el mismo umbral; la segunda pertenece a un
    /// cliente cuyo canal siempre falla (un teléfono dado de baja). Devuelve
    /// los canales de la primera y la tercera.
    /// </summary>
    private static (CanalDePrueba Primero, CanalDePrueba Ultimo) SuscribirTresAlertas(
        ISujetoPrecio accion, IDespachadorNotificaciones despachador)
    {
        CanalDePrueba primero = new();
        CanalDePrueba ultimo = new();

        accion.Suscribir(new AlertaUmbral(
            new Cliente("C1", "c1@correo.cu", primero), Umbral, Direccion.Sube, despachador));
        accion.Suscribir(new AlertaUmbral(
            new Cliente("C2", "+53 50000002", new CanalQueFalla(new CanalDePrueba(), int.MaxValue)),
            Umbral, Direccion.Sube, despachador));
        accion.Suscribir(new AlertaUmbral(
            new Cliente("C3", "dispositivo-0003", ultimo), Umbral, Direccion.Sube, despachador));

        return (primero, ultimo);
    }
}
