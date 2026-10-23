using Bolsa.Notificaciones.Entrega;

namespace Bolsa.Notificaciones.Tests;

/// <summary>
/// Pruebas del despachador: entrega cada notificación por el canal del cliente
/// que le corresponde y la registra en el repositorio.
/// </summary>
public sealed class DespachadorNotificacionesTests
{
    [Fact]
    public void EntregaCadaNotificacionPorElCanalDeSuCliente()
    {
        CanalDePrueba canalEmail = new();
        CanalDePrueba canalSms = new();
        CanalDePrueba canalPush = new();
        DespachadorNotificaciones despachador = new(new RepositorioMemoria());

        despachador.Encolar(new Notificacion(Guid.NewGuid(), new Cliente("C1", "c1@correo.cu", canalEmail), "aviso email"));
        despachador.Encolar(new Notificacion(Guid.NewGuid(), new Cliente("C2", "+53 50000002", canalSms), "aviso SMS"));
        despachador.Encolar(new Notificacion(Guid.NewGuid(), new Cliente("C3", "dispositivo-0003", canalPush), "aviso push"));

        Assert.Equal("aviso email", Assert.Single(canalEmail.Enviadas).Mensaje);
        Assert.Equal("aviso SMS", Assert.Single(canalSms.Enviadas).Mensaje);
        Assert.Equal("aviso push", Assert.Single(canalPush.Enviadas).Mensaje);
    }

    [Fact]
    public void GuardaEnElRepositorioCadaNotificacionEntregada()
    {
        RepositorioMemoria repositorio = new();
        DespachadorNotificaciones despachador = new(repositorio);
        CanalDePrueba canal = new();

        despachador.Encolar(new Notificacion(Guid.NewGuid(), new Cliente("C1", "c1@correo.cu", canal), "aviso 1"));
        despachador.Encolar(new Notificacion(Guid.NewGuid(), new Cliente("C2", "c2@correo.cu", canal), "aviso 2"));

        Assert.Equal(2, repositorio.Pendientes().Count);
        Assert.Equal(2, canal.Enviadas.Count);
        Assert.All(repositorio.Pendientes(), n => Assert.Contains(n, canal.Enviadas));
    }
}
