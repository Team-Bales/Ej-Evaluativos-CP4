using Bolsa.Notificaciones.Entrega;
using Bolsa.Notificaciones.Observer;
using Bolsa.Notificaciones.Rendimiento;

namespace Bolsa.Notificaciones.Tests;

/// <summary>
/// Inciso d): el índice ordenado tiene que ser una optimización, no un cambio
/// de comportamiento. Lo esencial de estas pruebas es la primera: ante los
/// mismos precios, el índice notifica exactamente a los mismos clientes y con
/// el mismo mensaje que las alertas una por una del inciso b).
/// </summary>
public sealed class GestorUmbralesIndexadoTests
{
    private const decimal PrecioInicial = 150.00m;

    [Fact]
    public void NotificaExactamenteALosMismosClientesQueLasAlertasIndividuales()
    {
        var aleatorio = new Random(20261024); // semilla fija: la prueba es reproducible
        SuscripcionUmbral[] suscripciones = Enumerable.Range(1, 500)
            .Select(i => new SuscripcionUmbral(
                ClienteDe(i),
                Math.Round(PrecioInicial * (1 + (decimal)(aleatorio.NextDouble() - 0.5) * 0.2m), 2),
                i % 2 == 0 ? Direccion.Sube : Direccion.Baja))
            .ToArray();

        decimal[] precios = Enumerable.Range(1, 40)
            .Select(_ => Math.Round(PrecioInicial * (1 + (decimal)(aleatorio.NextDouble() - 0.5) * 0.25m), 2))
            .ToArray();

        DespachadorDePrueba conAlertas = EjecutarConAlertas(suscripciones, precios);
        DespachadorDePrueba conIndice = EjecutarConIndice(suscripciones, precios, enParalelo: false);

        Assert.NotEmpty(conAlertas.Encoladas); // si no notificara nada, la prueba no probaría nada
        Assert.Equal(conAlertas.Resumen(), conIndice.Resumen());
    }

    [Fact]
    public void ElIndiceEnParaleloNotificaLoMismoQueElSecuencial()
    {
        SuscripcionUmbral[] suscripciones = Enumerable.Range(1, 200)
            .Select(i => new SuscripcionUmbral(ClienteDe(i), PrecioInicial + 0.10m * i, Direccion.Sube))
            .ToArray();
        decimal[] precios = [155.00m, 160.00m, 152.00m, 168.00m];

        DespachadorDePrueba secuencial = EjecutarConIndice(suscripciones, precios, enParalelo: false);
        DespachadorDePrueba paralelo = EjecutarConIndice(suscripciones, precios, enParalelo: true);

        Assert.NotEmpty(secuencial.Encoladas);
        Assert.Equal(secuencial.Resumen(), paralelo.Resumen());
    }

    [Fact]
    public void NotificaSoloALosDelTramoCruzadoAlAlza()
    {
        SuscripcionUmbral[] suscripciones =
        [
            new(ClienteDe(1), 149.00m, Direccion.Sube), // ya estaba cruzado antes del cambio
            new(ClienteDe(2), 151.00m, Direccion.Sube), // dentro del tramo
            new(ClienteDe(3), 152.00m, Direccion.Sube), // dentro del tramo
            new(ClienteDe(4), 160.00m, Direccion.Sube), // por encima del precio nuevo
            new(ClienteDe(5), 151.00m, Direccion.Baja), // dirección contraria
        ];

        DespachadorDePrueba despachador = EjecutarConIndice(suscripciones, [153.00m], enParalelo: false);

        Assert.Equal(["C2", "C3"], despachador.Encoladas.Select(n => n.Cliente.Id).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void NotificaSoloALosDelTramoCruzadoALaBaja()
    {
        SuscripcionUmbral[] suscripciones =
        [
            new(ClienteDe(1), 151.00m, Direccion.Baja), // por encima del precio inicial: no se cruza
            new(ClienteDe(2), 149.00m, Direccion.Baja), // dentro del tramo
            new(ClienteDe(3), 148.00m, Direccion.Baja), // dentro del tramo
            new(ClienteDe(4), 140.00m, Direccion.Baja), // por debajo del precio nuevo
            new(ClienteDe(5), 149.00m, Direccion.Sube), // dirección contraria
        ];

        DespachadorDePrueba despachador = EjecutarConIndice(suscripciones, [147.00m], enParalelo: false);

        Assert.Equal(["C2", "C3"], despachador.Encoladas.Select(n => n.Cliente.Id).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void RespetaLosExtremosDelTramo()
    {
        // La regla es: Sube notifica si precio anterior <= umbral < precio nuevo.
        SuscripcionUmbral[] suscripciones =
        [
            new(ClienteDe(1), 150.00m, Direccion.Sube), // igual al precio anterior: sí notifica
            new(ClienteDe(2), 155.00m, Direccion.Sube), // igual al precio nuevo: no notifica
        ];

        DespachadorDePrueba despachador = EjecutarConIndice(suscripciones, [155.00m], enParalelo: false);

        Assert.Equal("C1", Assert.Single(despachador.Encoladas).Cliente.Id);
    }

    [Fact]
    public void NoNotificaCuandoNingunUmbralQuedaEnElTramo()
    {
        SuscripcionUmbral[] suscripciones =
        [
            new(ClienteDe(1), 160.00m, Direccion.Sube),
            new(ClienteDe(2), 140.00m, Direccion.Baja),
        ];

        DespachadorDePrueba despachador = EjecutarConIndice(suscripciones, [151.00m, 149.00m], enParalelo: false);

        Assert.Empty(despachador.Encoladas);
    }

    [Fact]
    public void ElSujetoSoloVeUnObservador()
    {
        // El índice es un ChangeManager: la acción sigue sin conocer a los
        // clientes, solo ve un IObservadorPrecio.
        var despachador = new DespachadorDePrueba();
        SuscripcionUmbral[] suscripciones = Enumerable.Range(1, 1000)
            .Select(i => new SuscripcionUmbral(ClienteDe(i), PrecioInicial + 0.01m * i, Direccion.Sube))
            .ToArray();

        GestorUmbralesIndexado gestor = GestorUmbralesIndexado.Construir(suscripciones, despachador);

        Assert.Equal(1000, gestor.Suscripciones);
        Assert.IsAssignableFrom<IObservadorPrecio>(gestor);
    }

    private static DespachadorDePrueba EjecutarConAlertas(SuscripcionUmbral[] suscripciones, decimal[] precios)
    {
        DespachadorDePrueba despachador = new();
        var accion = new Accion("AAPL", PrecioInicial, new RegistroDePrueba());

        foreach (SuscripcionUmbral s in suscripciones)
        {
            accion.Suscribir(new AlertaUmbral(s.Cliente, s.Umbral, s.Direccion, despachador));
        }

        foreach (decimal precio in precios)
        {
            accion.ActualizarPrecio(precio);
        }

        return despachador;
    }

    private static DespachadorDePrueba EjecutarConIndice(
        SuscripcionUmbral[] suscripciones, decimal[] precios, bool enParalelo)
    {
        DespachadorDePrueba despachador = new();
        var accion = new Accion("AAPL", PrecioInicial, new RegistroDePrueba());
        accion.Suscribir(GestorUmbralesIndexado.Construir(suscripciones, despachador, enParalelo));

        foreach (decimal precio in precios)
        {
            accion.ActualizarPrecio(precio);
        }

        return despachador;
    }

    private static Cliente ClienteDe(int i) =>
        new($"C{i}", $"cliente{i}@correo.cu", new CanalDePrueba());
}
