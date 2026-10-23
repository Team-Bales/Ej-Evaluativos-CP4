using Bolsa.Notificaciones.Entrega;
using Bolsa.Notificaciones.Observer;

namespace Bolsa.Notificaciones.Tests;

/// <summary>
/// Pruebas de la regla de disparo de AlertaUmbral: solo se notifica cuando el
/// precio cruza el umbral, en la dirección de la alerta, y solo hasta que el
/// cliente se desuscribe.
/// </summary>
public sealed class AlertaUmbralTests
{
    private static (Accion Accion, AlertaUmbral Alerta, CanalDePrueba Canal, RepositorioMemoria Repositorio)
        CrearEscenario(decimal precioInicial, decimal umbral, Direccion direccion)
    {
        CanalDePrueba canal = new();
        RepositorioMemoria repositorio = new();
        DespachadorNotificaciones despachador = new(repositorio);
        Cliente cliente = new("C0001", "cliente0001@correo.cu", canal);
        Accion accion = new("AAPL", precioInicial);
        AlertaUmbral alerta = new(cliente, umbral, direccion, despachador);
        accion.Suscribir(alerta);
        return (accion, alerta, canal, repositorio);
    }

    [Fact]
    public void Notifica_CuandoElPrecioCruzaElUmbralHaciaArriba()
    {
        (Accion accion, _, CanalDePrueba canal, RepositorioMemoria repositorio) =
            CrearEscenario(150.00m, 152.00m, Direccion.Sube);

        accion.ActualizarPrecio(151.90m); // se acerca al umbral, pero no lo cruza
        Assert.Empty(canal.Enviadas);

        accion.ActualizarPrecio(152.05m); // cruza el umbral al alza
        Notificacion notificacion = Assert.Single(canal.Enviadas);
        Assert.Equal(notificacion, Assert.Single(repositorio.Pendientes()));
        Assert.Contains("AAPL subió por encima de su umbral 152.00", notificacion.Mensaje);
        Assert.Contains("151.90 → 152.05", notificacion.Mensaje);
    }

    [Fact]
    public void Notifica_CuandoElPrecioCruzaElUmbralHaciaAbajo()
    {
        (Accion accion, _, CanalDePrueba canal, _) = CrearEscenario(150.00m, 148.00m, Direccion.Baja);

        accion.ActualizarPrecio(148.50m); // sigue por encima del umbral
        Assert.Empty(canal.Enviadas);

        accion.ActualizarPrecio(147.95m); // cruza el umbral a la baja
        Notificacion notificacion = Assert.Single(canal.Enviadas);
        Assert.Contains("AAPL bajó por debajo de su umbral 148.00", notificacion.Mensaje);
    }

    [Fact]
    public void NoNotifica_SiElPrecioQuedaPorDebajoDelUmbral()
    {
        (Accion accion, _, CanalDePrueba canal, RepositorioMemoria repositorio) =
            CrearEscenario(150.00m, 152.00m, Direccion.Sube);

        accion.ActualizarPrecio(151.99m); // sube, pero se queda por debajo del umbral
        accion.ActualizarPrecio(150.50m);

        Assert.Empty(canal.Enviadas);
        Assert.Empty(repositorio.Pendientes());
    }

    [Fact]
    public void NoNotifica_RepetidasVeces_MientrasElPrecioSigaPorEncimaDelUmbral()
    {
        (Accion accion, _, CanalDePrueba canal, _) = CrearEscenario(150.00m, 152.00m, Direccion.Sube);

        accion.ActualizarPrecio(153.00m); // primer cruce: un aviso
        accion.ActualizarPrecio(154.00m); // sigue por encima: sin aviso nuevo
        accion.ActualizarPrecio(155.00m); // sigue por encima: sin aviso nuevo

        Assert.Single(canal.Enviadas);
    }

    [Fact]
    public void NoNotifica_DespuesDeDesuscribirse()
    {
        (Accion accion, AlertaUmbral alerta, CanalDePrueba canal, _) =
            CrearEscenario(150.00m, 152.00m, Direccion.Sube);

        accion.ActualizarPrecio(153.00m);
        Assert.Single(canal.Enviadas);

        accion.Desuscribir(alerta);

        accion.ActualizarPrecio(151.00m); // baja y vuelve a cruzar el umbral al alza
        accion.ActualizarPrecio(152.50m);

        Assert.Single(canal.Enviadas); // solo el aviso anterior a la desuscripción
    }

    [Fact]
    public void NotificaATodosLosObservadoresSuscritos()
    {
        RepositorioMemoria repositorio = new();
        DespachadorNotificaciones despachador = new(repositorio);
        CanalDePrueba canalDeUn = new();
        CanalDePrueba canalDeOtro = new();
        Accion accion = new("AAPL", 150.00m);

        accion.Suscribir(new AlertaUmbral(new Cliente("C1", "c1@correo.cu", canalDeUn), 152.00m, Direccion.Sube, despachador));
        accion.Suscribir(new AlertaUmbral(new Cliente("C2", "c2@correo.cu", canalDeOtro), 151.00m, Direccion.Sube, despachador));

        accion.ActualizarPrecio(153.00m); // cruza ambos umbrales

        Assert.Single(canalDeUn.Enviadas);
        Assert.Single(canalDeOtro.Enviadas);
        Assert.Equal(2, repositorio.Pendientes().Count);
    }
}
