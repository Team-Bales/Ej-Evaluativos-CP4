using Banco.Procesamiento.Cuentas;
using Banco.Procesamiento.Transacciones;

namespace Banco.Procesamiento.Tests;

/// <summary>
/// Las transacciones solo admiten lo que los visitantes saben aplicar: un
/// movimiento por cuenta y montos en centavos.
/// </summary>
public sealed class TransaccionTests
{
    private sealed class TransaccionConDosMovimientos(Cuenta cuenta)
        : Transaccion(Escenario.Usuario, Escenario.Hoy, autorizadaPor: null,
                      [new Movimiento(cuenta, Naturaleza.Debito, 10.00m), new Movimiento(cuenta, Naturaleza.Credito, 10.00m)])
    {
        public override string Descripcion => "dos movimientos sobre la misma cuenta";
    }

    [Fact]
    public void NoAdmiteDosMovimientosSobreLaMismaCuenta()
    {
        CuentaAhorro cuenta = Escenario.Ahorro();

        ArgumentException error = Assert.Throws<ArgumentException>(() => new TransaccionConDosMovimientos(cuenta));

        Assert.Equal("movimientos", error.ParamName);
    }

    [Fact]
    public void NoAdmiteUnaTransferenciaALaMismaCuenta()
    {
        CuentaAhorro cuenta = Escenario.Ahorro();

        ArgumentException error = Assert.Throws<ArgumentException>(
            () => new Transferencia(cuenta, cuenta, 10.00m, Escenario.Usuario, Escenario.Hoy));

        Assert.Equal("movimientos", error.ParamName); // la rechaza la regla general: un movimiento por cuenta
    }

    [Fact]
    public void NoAdmiteMontosConFraccionesDeCentavo()
    {
        CuentaAhorro cuenta = Escenario.Ahorro();

        ArgumentException error = Assert.Throws<ArgumentException>(() => Escenario.NuevoDeposito(cuenta, 0.005m));

        Assert.Equal("monto", error.ParamName);
    }
}
