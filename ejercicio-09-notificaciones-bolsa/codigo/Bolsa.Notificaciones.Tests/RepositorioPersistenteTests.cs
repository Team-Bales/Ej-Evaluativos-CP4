using System.Text;
using Bolsa.Notificaciones.Entrega;

namespace Bolsa.Notificaciones.Tests;

/// <summary>
/// Restricción E9-R: la entrega es at-least-once y las notificaciones fallidas
/// quedan persistidas. Estas pruebas comprueban el outbox real (archivo en
/// disco), no una simulación en memoria.
/// </summary>
public sealed class RepositorioPersistenteTests : IDisposable
{
    private static readonly TimeSpan EsperaCorta = TimeSpan.FromMilliseconds(10);

    private readonly string _carpeta;

    public RepositorioPersistenteTests()
    {
        _carpeta = Path.Combine(Path.GetTempPath(), $"bolsa-e9r-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_carpeta);
    }

    public void Dispose()
    {
        if (Directory.Exists(_carpeta))
        {
            Directory.Delete(_carpeta, recursive: true);
        }
    }

    private string Ruta => Path.Combine(_carpeta, "notificaciones.json");

    [Fact]
    public async Task LaNotificacionQuedaPersistidaAntesDeEnviar()
    {
        RepositorioPersistente repositorio = new(Ruta);
        var canal = new CanalBloqueante();

        await using var despachador = new DespachadorAsincrono(
            repositorio, new RegistroDePrueba(), consumidores: 1, maxIntentos: 3, esperaBase: EsperaCorta);

        Notificacion aviso = new(
            Guid.NewGuid(), new Cliente("C1", "cliente0001@correo.cu", canal), "aviso pendiente");
        despachador.Encolar(aviso);

        // El consumidor ya cogió el aviso y está dentro del envío: el canal
        // todavía no ha confirmado, así que en el outbox tiene que estar pendiente.
        await EsperarQue(() => canal.Intentos > 0, "el consumidor empieza el envío");

        RegistroOutbox pendiente = Assert.Single(repositorio.Todos());
        Assert.Equal(EstadoNotificacion.Pendiente, pendiente.Estado);
        Assert.Equal(0, pendiente.Intentos);

        // El canal confirma: ahora sí se marca como entregada.
        canal.Liberar();
        await despachador.EsperarVaciadoAsync();

        RegistroOutbox entregada = Assert.Single(repositorio.Todos());
        Assert.Equal(EstadoNotificacion.Entregada, entregada.Estado);
    }

    [Fact]
    public void ElPendienteSobreviveAlReinicio()
    {
        RepositorioPersistente antes = new(Ruta);
        Notificacion aviso = new(
            Guid.NewGuid(), new Cliente("C2", "cliente0002@correo.cu", new CanalEmail()), "aviso tras la caída");
        antes.RegistrarPendiente(aviso, intentos: 0);

        // Otro proceso abre el mismo archivo: el aviso sigue ahí.
        RepositorioPersistente despues = new(Ruta);
        EnvioPendiente pendiente = Assert.Single(despues.PendientesParaReintentar());

        Assert.Equal(aviso.Id, pendiente.Notificacion.Id);
        Assert.Equal(aviso.Mensaje, pendiente.Notificacion.Mensaje);
        Assert.Equal(aviso.Cliente.Contacto, pendiente.Notificacion.Cliente.Contacto);
        Assert.Equal(0, pendiente.Intentos);
    }

    [Fact]
    public async Task LaNotificacionRecuperadaSeEntrega()
    {
        var fabrica = new FabricaCanalesDePrueba();

        // El repositorio se reconstruye con una fábrica que sabe crear el canal.
        RepositorioPersistente conFabrica = new(Ruta, fabrica);
        Notificacion aviso = new(
            Guid.NewGuid(), new Cliente("C3", "cliente0003@correo.cu", new CanalEmail()), "aviso recuperado");
        conFabrica.RegistrarPendiente(aviso, intentos: 0);

        await using var despachador = new DespachadorAsincrono(
            conFabrica, new RegistroDePrueba(), consumidores: 1, maxIntentos: 3, esperaBase: EsperaCorta);

        int recuperadas = despachador.RecuperarPendientes();
        await despachador.EsperarVaciadoAsync();

        Assert.Equal(1, recuperadas);
        Assert.Equal(1, despachador.Entregadas);
        Assert.Equal(
            aviso.Id,
            Assert.Single(fabrica.Creados).Enviadas.Single().Id);
        Assert.Equal(
            EstadoNotificacion.Entregada,
            Assert.Single(conFabrica.Todos()).Estado);
    }

    [Fact]
    public async Task ElFalloDefinitivoQuedaPersistido()
    {
        RepositorioPersistente repositorio = new(Ruta);
        var canal = new CanalQueFalla(new CanalDePrueba(), int.MaxValue);

        await using var despachador = new DespachadorAsincrono(
            repositorio, new RegistroDePrueba(), consumidores: 1, maxIntentos: 2, esperaBase: EsperaCorta);

        despachador.Encolar(new Notificacion(
            Guid.NewGuid(), new Cliente("C4", "+53 50000004", canal), "aviso imposible"));
        await despachador.EsperarVaciadoAsync();

        RegistroOutbox fallida = Assert.Single(repositorio.Todos());
        Assert.Equal(EstadoNotificacion.FalloDefinitivo, fallida.Estado);
        Assert.Equal(2, fallida.Intentos);
        Assert.Contains("El proveedor rechazó", fallida.UltimoError);
        Assert.Single(despachador.FallosDefinitivos);
    }

    [Fact]
    public async Task ElContadorDeIntentosSeConservaEnElDisco()
    {
        RepositorioPersistente repositorio = new(Ruta);
        var canal = new CanalQueFalla(new CanalDePrueba(), fallosIniciales: 1);

        await using var despachador = new DespachadorAsincrono(
            repositorio, new RegistroDePrueba(), consumidores: 1, maxIntentos: 3, esperaBase: EsperaCorta);

        despachador.Encolar(new Notificacion(
            Guid.NewGuid(), new Cliente("C5", "cliente0005@correo.cu", canal), "aviso intermitente"));
        await despachador.EsperarVaciadoAsync();

        RegistroOutbox entregada = Assert.Single(repositorio.Todos());
        Assert.Equal(EstadoNotificacion.Entregada, entregada.Estado);
        Assert.Equal(1, entregada.Intentos); // falló una vez y se recuperó
    }

    [Fact]
    public void ElReenvioNoDuplicaLaFilaDelOutbox()
    {
        RepositorioPersistente repositorio = new(Ruta);
        Notificacion aviso = new(
            Guid.NewGuid(), new Cliente("C6", "cliente0006@correo.cu", new CanalEmail()), "aviso repetido");

        repositorio.RegistrarPendiente(aviso, intentos: 0);
        repositorio.RegistrarPendiente(aviso, intentos: 0);
        Assert.Single(repositorio.Todos());

        repositorio.Guardar(aviso);
        repositorio.Guardar(aviso);
        Assert.Single(repositorio.Todos());
    }

    [Fact]
    public void UnOutboxDanadoNoSeDescartaEnSilencio()
    {
        File.WriteAllText(Ruta, "esto no es un json", Encoding.UTF8);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => new RepositorioPersistente(Ruta));

        Assert.Contains("dañado", error.Message);
    }

    private static async Task EsperarQue(Func<bool> condicion, string descripcion)
    {
        var tope = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);
        while (DateTimeOffset.UtcNow < tope)
        {
            if (condicion())
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(1));
        }

        throw new InvalidOperationException($"Tiempo de espera agotado: {descripcion}.");
    }
}
