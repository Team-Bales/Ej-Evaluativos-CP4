using Banco.Procesamiento.Cuentas;
using Banco.Procesamiento.Transacciones;
using Banco.Procesamiento.Visitantes;

namespace Banco.Procesamiento.Tests;

/// <summary>
/// El Composite del inciso c: un grupo se trata igual que una cuenta, los
/// grupos se anidan y las operaciones sobre un grupo llegan a todas sus hojas.
/// </summary>
public sealed class GrupoCuentasTests
{
    private sealed class VisitanteQueAnota : IVisitanteCuenta
    {
        public List<string> Recorrido { get; } = [];

        public void EntrarEnGrupo(GrupoCuentas grupo) => Recorrido.Add($"entra {grupo.Nombre}");

        public void SalirDeGrupo(GrupoCuentas grupo) => Recorrido.Add($"sale {grupo.Nombre}");

        public void Visitar(CuentaAhorro cuenta) => Recorrido.Add(cuenta.Numero);

        public void Visitar(CuentaCorriente cuenta) => Recorrido.Add(cuenta.Numero);

        public void Visitar(CuentaInversion cuenta) => Recorrido.Add(cuenta.Numero);

        public void Visitar(CuentaEmpresarial cuenta) => Recorrido.Add(cuenta.Numero);
    }

    // Banco > Región > (Centro: AHO-001, COR-001; Playa: INV-001) y Banco > Empresas: EMP-001
    private static (GrupoCuentas Banco, CuentaAhorro Ahorro, CuentaCorriente Corriente,
                    CuentaInversion Inversion, CuentaEmpresarial Empresarial) Arbol()
    {
        CuentaAhorro ahorro = Escenario.Ahorro(1_000.00m);
        CuentaCorriente corriente = Escenario.Corriente(300.00m);
        CuentaInversion inversion = Escenario.Inversion(20_000.00m);
        CuentaEmpresarial empresarial = Escenario.Empresarial(50_000.00m);
        GrupoCuentas region = new GrupoCuentas("Región")
            .Agregar(new GrupoCuentas("Centro").Agregar(ahorro, corriente), new GrupoCuentas("Playa").Agregar(inversion));
        GrupoCuentas banco = new GrupoCuentas("Banco").Agregar(region, new GrupoCuentas("Empresas").Agregar(empresarial));
        return (banco, ahorro, corriente, inversion, empresarial);
    }

    [Fact]
    public void Aceptar_RecorreLosGruposAnidadosEnProfundidadYEnOrden()
    {
        GrupoCuentas banco = Arbol().Banco;
        VisitanteQueAnota visitante = new();

        banco.Aceptar(visitante);

        Assert.Equal(
            new[]
            {
                "entra Banco", "entra Región", "entra Centro", "AHO-001", "COR-001", "sale Centro",
                "entra Playa", "INV-001", "sale Playa", "sale Región",
                "entra Empresas", "EMP-001", "sale Empresas", "sale Banco",
            },
            visitante.Recorrido);
    }

    [Fact]
    public void Agregar_RechazaLosCiclos()
    {
        GrupoCuentas banco = new("Banco");
        GrupoCuentas region = new("Región");
        banco.Agregar(region);

        Assert.Throws<ArgumentException>(() => banco.Agregar(banco));
        Assert.Throws<ArgumentException>(() => region.Agregar(banco)); // el banco ya contiene a la región
    }

    [Fact]
    public void Hijos_NoExponeLaListaInterna()
    {
        GrupoCuentas grupo = new GrupoCuentas("Centro").Agregar(Escenario.Ahorro());

        Assert.Null(grupo.Hijos as ICollection<ICuenta>); // no se puede convertir para modificarla
        Assert.Single(grupo.Hijos);
    }

    [Fact]
    public void ResumirPorGrupo_SumaCadaCuentaEnTodosLosGruposQueLaContienen()
    {
        (GrupoCuentas banco, _, CuentaCorriente corriente, CuentaInversion inversion, CuentaEmpresarial empresarial) = Arbol();
        corriente.Aceptar(new EjecutarTransaccion(Escenario.NuevoRetiro(corriente, 600.00m)));         // cargo 5
        inversion.Aceptar(new EjecutarTransaccion(Escenario.NuevoRetiro(inversion, 1_000.00m)));       // penalización 20
        empresarial.Aceptar(new EjecutarTransaccion(Escenario.NuevoRetiro(empresarial, 1_000.00m)));   // comisión 5
        ResumirPorGrupo resumir = new();

        banco.Aceptar(resumir);

        Assert.Equal(
            new[]
            {
                new ResumenGrupo("Banco", 0, 4, 1_000m - 305m + 18_980m + 48_995m, 30m),
                new ResumenGrupo("Región", 1, 3, 1_000m - 305m + 18_980m, 25m),
                new ResumenGrupo("Centro", 2, 2, 1_000m - 305m, 5m),
                new ResumenGrupo("Playa", 2, 1, 18_980m, 20m),
                new ResumenGrupo("Empresas", 1, 1, 48_995m, 5m),
            },
            resumir.Resumenes);
    }

    [Fact]
    public void UnVisitanteQueNoDistingueGrupos_FuncionaSobreUnGrupoSinCambios()
    {
        GrupoCuentas banco = Arbol().Banco;
        CalcularIntereses intereses = new();

        banco.Aceptar(intereses);

        Assert.Equal(4, intereses.Intereses.Count);
        Assert.Equal(2.50m + 0m + 100.00m + 25.00m, intereses.Total);
    }

    [Fact]
    public void EjecutarEnCadaCuenta_ConFalloParcialSigueConLasDemasYReportaCadaResultado()
    {
        (GrupoCuentas banco, CuentaAhorro ahorro, CuentaCorriente corriente, CuentaInversion inversion,
         CuentaEmpresarial empresarial) = Arbol();
        for (int i = 0; i < ahorro.LimiteRetirosMensuales; i++)
        {
            ahorro.Aceptar(new EjecutarTransaccion(Escenario.NuevoRetiro(ahorro, 10.00m))); // agota los débitos del mes
        }

        IReadOnlyList<ResultadoTransaccion> reporte = new ProcesadorTransacciones().EjecutarEnCadaCuenta(
            banco, cuenta => new PagoServicio(cuenta, "Mantenimiento", 5.00m, Escenario.Usuario, Escenario.Hoy));

        Assert.Equal([false, true, true, true], reporte.Select(r => r.FueAplicada));
        Assert.StartsWith("AHO-001: alcanzó el límite", reporte[0].MotivoRechazo);
        Assert.Equal((970.00m, 3), Escenario.Estado(ahorro));            // la que falló, intacta
        Assert.Equal((295.00m, 0), Escenario.Estado(corriente));         // las demás, cobradas
        Assert.Equal((19_994.90m, 0.10m), Escenario.Estado(inversion));
        Assert.Equal((49_994.97m, 0.03m, 0), Escenario.Estado(empresarial));
    }

    [Fact]
    public void EjecutarEnCadaCuenta_TrataIgualAUnaCuentaSola()
    {
        CuentaCorriente corriente = Escenario.Corriente(300.00m);

        IReadOnlyList<ResultadoTransaccion> reporte = new ProcesadorTransacciones().EjecutarEnCadaCuenta(
            corriente, cuenta => new PagoServicio(cuenta, "Mantenimiento", 5.00m, Escenario.Usuario, Escenario.Hoy));

        Assert.True(Assert.Single(reporte).FueAplicada);
        Assert.Equal(295.00m, corriente.Saldo);
    }
}
