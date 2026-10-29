using System.Diagnostics;
using Bolsa.Notificaciones.Entrega;
using Bolsa.Notificaciones.Observer;

namespace Bolsa.Notificaciones.Tests;

/// <summary>
/// Inciso c), segunda y tercera preguntas: la notificación fallida se reintenta
/// y no se pierde, y el hilo que cambia el precio no espera a ningún canal.
/// Los márgenes de tiempo son holgados a propósito, para que las pruebas no
/// dependan de lo cargada que esté la máquina.
/// </summary>
public sealed class DespachadorAsincronoTests
{
    private const decimal PrecioInicial = 150.00m;
    private const decimal PrecioTrasElSalto = 155.00m;
    private const decimal Umbral = 151.00m;

    private static readonly TimeSpan EsperaCorta = TimeSpan.FromMilliseconds(10);

    [Fact]
    public async Task EntregaEnSegundoPlanoTodasLasNotificacionesEncoladas()
    {
        CanalDePrueba canal = new();
        RepositorioMemoria repositorio = new();
        await using DespachadorAsincrono despachador = Crear(repositorio);

        for (int i = 1; i <= 50; i++)
        {
            despachador.Encolar(new Notificacion(
                Guid.NewGuid(), new Cliente($"C{i}", $"c{i}@correo.cu", canal), $"aviso {i}"));
        }

        await despachador.EsperarVaciadoAsync();

        Assert.Equal(50, despachador.Entregadas);
        Assert.Equal(50, canal.Enviadas.Count);
        Assert.Equal(50, repositorio.Pendientes().Count);
    }

    [Fact]
    public async Task ReintentaYEntrega_CuandoElCanalSeRecupera()
    {
        CanalDePrueba destino = new();
        CanalQueFalla canal = new(destino, fallosIniciales: 2);
        await using DespachadorAsincrono despachador = Crear(new RepositorioMemoria());

        despachador.Encolar(new Notificacion(
            Guid.NewGuid(), new Cliente("C1", "c1@correo.cu", canal), "aviso con reintentos"));
        await despachador.EsperarVaciadoAsync();

        Assert.Equal(1, despachador.Entregadas);
        Assert.Equal(2, despachador.Reintentos);
        Assert.Equal(3, canal.Intentos); // dos fallos y la entrega
        Assert.Single(destino.Enviadas);
        Assert.Empty(despachador.FallosDefinitivos);
    }

    [Fact]
    public async Task DaPorPerdidaLaNotificacion_TrasAgotarLosIntentos()
    {
        CanalQueFalla canal = new(new CanalDePrueba(), int.MaxValue);
        RepositorioMemoria repositorio = new();
        await using DespachadorAsincrono despachador = Crear(repositorio);

        Notificacion notificacion = new(
            Guid.NewGuid(), new Cliente("C1", "+53 50000001", canal), "aviso imposible");
        despachador.Encolar(notificacion);
        await despachador.EsperarVaciadoAsync();

        Assert.Equal(0, despachador.Entregadas);
        Assert.Equal(despachador.MaxIntentos, canal.Intentos);
        Assert.Equal(notificacion, Assert.Single(despachador.FallosDefinitivos));
        Assert.Empty(repositorio.Pendientes()); // no se guarda como entregada
    }

    [Fact]
    public async Task UnCanalCaidoNoImpideLaEntregaDeLosDemasClientes()
    {
        CanalDePrueba sano = new();
        await using DespachadorAsincrono despachador = Crear(new RepositorioMemoria());
        var accion = new Accion("AAPL", PrecioInicial, new RegistroDePrueba());

        accion.Suscribir(new AlertaUmbral(
            new Cliente("C1", "c1@correo.cu", new CanalQueFalla(new CanalDePrueba(), int.MaxValue)),
            Umbral, Direccion.Sube, despachador));
        accion.Suscribir(new AlertaUmbral(
            new Cliente("C2", "c2@correo.cu", sano), Umbral, Direccion.Sube, despachador));

        accion.ActualizarPrecio(PrecioTrasElSalto);
        await despachador.EsperarVaciadoAsync();

        Assert.Single(sano.Enviadas);
        Assert.Single(despachador.FallosDefinitivos);
    }

    [Fact]
    public async Task ElCambioDePrecioNoEsperaAlCanalLento()
    {
        CanalDePrueba destino = new();
        var canalLento = new CanalLento(destino, TimeSpan.FromSeconds(1));
        await using DespachadorAsincrono despachador = Crear(new RepositorioMemoria());
        var accion = new Accion("AAPL", PrecioInicial);
        accion.Suscribir(new AlertaUmbral(
            new Cliente("C1", "+53 50000001", canalLento), Umbral, Direccion.Sube, despachador));

        var reloj = Stopwatch.StartNew();
        accion.ActualizarPrecio(PrecioTrasElSalto);
        reloj.Stop();

        // El sujeto solo encoló: regresa muy por debajo del segundo que tarda el canal.
        Assert.True(
            reloj.Elapsed < TimeSpan.FromMilliseconds(500),
            $"ActualizarPrecio tardó {reloj.ElapsedMilliseconds} ms; debería regresar enseguida.");
        Assert.Empty(destino.Enviadas); // todavía no se ha entregado

        await despachador.EsperarVaciadoAsync();
        Assert.Single(destino.Enviadas); // la entrega ocurrió en segundo plano
    }

    [Fact]
    public async Task RechazaSinBloquear_CuandoLaColaEstaLlena()
    {
        // Capacidad 1 y un único consumidor ocupado con un canal lento: la cola
        // se llena enseguida y el resto se rechaza (backpressure, inciso d).
        await using var despachador = new DespachadorAsincrono(
            new RepositorioMemoria(),
            new RegistroDePrueba(),
            consumidores: 1,
            capacidad: 1,
            maxIntentos: 1,
            esperaBase: EsperaCorta);

        var canal = new CanalLento(new CanalDePrueba(), TimeSpan.FromMilliseconds(500));
        var reloj = Stopwatch.StartNew();

        for (int i = 1; i <= 100; i++)
        {
            despachador.Encolar(new Notificacion(
                Guid.NewGuid(), new Cliente($"C{i}", $"c{i}@correo.cu", canal), $"aviso {i}"));
        }

        reloj.Stop();

        Assert.True(
            reloj.Elapsed < TimeSpan.FromMilliseconds(500),
            $"Encolar 100 avisos tardó {reloj.ElapsedMilliseconds} ms; no debería esperar nunca.");
        Assert.True(despachador.Rechazadas > 0, "con la cola llena tiene que haber rechazos.");

        // Ya en reposo, cada aviso tuvo exactamente un desenlace: entregado o rechazado.
        await despachador.EsperarVaciadoAsync();
        Assert.Equal(0, despachador.EnProceso);
        Assert.Equal(100, despachador.Entregadas + despachador.Rechazadas);
    }

    [Fact]
    public async Task ElReintentoQueNoPuedeReencolarseSeRegistraYPasaAFalloDefinitivo()
    {
        // Cuando el reintento encuentra la cola cerrada (despachador
        // disponible), su TryWrite falla: se registra con _registro.Registrar
        // antes de pasar el aviso a FallosDefinitivos, nunca en silencio.
        RegistroDePrueba registro = new();
        DespachadorAsincrono despachador = new(
            new RepositorioMemoria(), registro,
            consumidores: 1, capacidad: 10, maxIntentos: 3, esperaBase: TimeSpan.FromMilliseconds(50));

        CanalQueFalla canal = new(new CanalDePrueba(), int.MaxValue);
        Notificacion aviso = new(
            Guid.NewGuid(), new Cliente("C1", "c1@correo.cu", canal), "reintento truncado");
        despachador.Encolar(aviso);

        // El consumidor falla el primer envío y programa el reintento (25 ms).
        await EsperarQue(() => despachador.Reintentos > 0, "el primer reintento se programa");

        // Cerrar el despachador hace que el temporizador pendiente falle su
        // TryWrite: se registra antes del dead letter, nunca en silencio.
        await despachador.DisposeAsync();

        Assert.Equal(1, canal.Intentos);
        Assert.Equal(0, despachador.Entregadas);
        Assert.Single(despachador.FallosDefinitivos);
        Assert.Equal(aviso, Assert.Single(despachador.FallosDefinitivos));
        Assert.Contains(
            registro.Fallos,
            fallo => fallo.Contains("no se pudo reencolar"));
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

    private static DespachadorAsincrono Crear(IRepositorioNotificaciones repositorio) =>
        new(repositorio, new RegistroDePrueba(), consumidores: 4, maxIntentos: 3, esperaBase: EsperaCorta);
}
