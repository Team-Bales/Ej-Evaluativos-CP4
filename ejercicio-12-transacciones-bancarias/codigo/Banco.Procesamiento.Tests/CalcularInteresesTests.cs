using Banco.Procesamiento.Cuentas;
using Banco.Procesamiento.Transacciones;
using Banco.Procesamiento.Visitantes;

namespace Banco.Procesamiento.Tests;

/// <summary>
/// La operación nueva del requisito de extensibilidad: se agregó con una sola
/// clase y se comporta distinto en cada tipo de cuenta.
/// </summary>
public sealed class CalcularInteresesTests
{
    [Fact]
    public void CalculaElInteresDelMesConLaReglaDeCadaTipoDeCuenta()
    {
        CuentaAhorro ahorro = Escenario.Ahorro(1_200.00m);
        CuentaCorriente corriente = Escenario.Corriente(300.00m);
        corriente.Aceptar(new EjecutarTransaccion(Escenario.NuevoRetiro(corriente, 695.00m))); // queda en −400
        CuentaInversion inversion = Escenario.Inversion(20_000.00m);
        CuentaEmpresarial empresarial = Escenario.Empresarial(50_000.00m);
        CalcularIntereses intereses = new();

        foreach (ICuenta cuenta in new ICuenta[] { ahorro, corriente, inversion, empresarial })
        {
            cuenta.Aceptar(intereses);
        }

        Assert.Equal(3.00m, intereses.Intereses["AHO-001"]);    // 3 % anual de 1200
        Assert.Equal(-6.00m, intereses.Intereses["COR-001"]);   // cobra 18 % anual sobre 400 de sobregiro
        Assert.Equal(100.00m, intereses.Intereses["INV-001"]);  // 6 % anual de 20 000
        Assert.Equal(25.00m, intereses.Intereses["EMP-001"]);   // 1 % anual sobre 30 000 (exento 20 000)
        Assert.Equal(122.00m, intereses.Total);
    }

    [Fact]
    public void UnSaldoNegativoPorUnaReversionNoGeneraInteres()
    {
        CuentaInversion cuenta = Escenario.Inversion(0m);
        Deposito deposito = Escenario.NuevoDeposito(cuenta, 1_000.00m);
        cuenta.Aceptar(new EjecutarTransaccion(deposito));
        cuenta.Aceptar(new EjecutarTransaccion(Escenario.NuevoRetiro(cuenta, 1_000.00m, Escenario.Vencimiento)));
        cuenta.Aceptar(new RevertirTransaccion(deposito)); // queda en −1000
        CalcularIntereses intereses = new();

        cuenta.Aceptar(intereses);

        Assert.Equal(0m, intereses.Intereses["INV-001"]);
    }

    [Fact]
    public void NoModificaLasCuentas()
    {
        CuentaInversion cuenta = Escenario.Inversion(20_000.00m);
        var estadoAntes = Escenario.Estado(cuenta);

        cuenta.Aceptar(new CalcularIntereses());

        Assert.Equal(estadoAntes, Escenario.Estado(cuenta));
    }
}
