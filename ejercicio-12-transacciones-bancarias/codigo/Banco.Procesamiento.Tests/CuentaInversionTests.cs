using Banco.Procesamiento.Cuentas;
using Banco.Procesamiento.Transacciones;
using Banco.Procesamiento.Visitantes;

namespace Banco.Procesamiento.Tests;

/// <summary>
/// Las tres operaciones sobre CuentaInversion: penalización por retiro
/// anticipado y depósito mínimo.
/// </summary>
public sealed class CuentaInversionTests
{
    [Fact]
    public void Ejecutar_RetiroAntesDelVencimientoPagaLaPenalizacion()
    {
        CuentaInversion cuenta = Escenario.Inversion(20_000.00m);

        cuenta.Aceptar(new EjecutarTransaccion(Escenario.NuevoRetiro(cuenta, 1_000.00m)));

        Assert.Equal((18_980.00m, 20.00m), Escenario.Estado(cuenta)); // 2 % de 1000
    }

    [Fact]
    public void Ejecutar_RetiroDesdeLaFechaDeVencimientoNoPagaPenalizacion()
    {
        CuentaInversion cuenta = Escenario.Inversion(20_000.00m);

        cuenta.Aceptar(new EjecutarTransaccion(Escenario.NuevoRetiro(cuenta, 1_000.00m, Escenario.Vencimiento)));

        Assert.Equal((19_000.00m, 0m), Escenario.Estado(cuenta));
    }

    [Fact]
    public void Ejecutar_RechazaElDepositoMenorQueElMinimo()
    {
        CuentaInversion cuenta = Escenario.Inversion(20_000.00m);

        EjecutarTransaccion ejecutar = new(Escenario.NuevoDeposito(cuenta, 50.00m));
        OperacionRechazadaException rechazo =
            Assert.Throws<OperacionRechazadaException>(() => cuenta.Aceptar(ejecutar));

        Assert.Equal("el depósito mínimo es 100.00", rechazo.Motivo);
        Assert.Equal((20_000.00m, 0m), Escenario.Estado(cuenta));
    }

    [Fact]
    public void Ejecutar_RechazaElRetiroQueConLaPenalizacionPasaDelSaldo()
    {
        CuentaInversion cuenta = Escenario.Inversion(1_000.00m);

        // 990 + 19.80 de penalización = 1009.80 > 1000
        EjecutarTransaccion ejecutar = new(Escenario.NuevoRetiro(cuenta, 990.00m));

        Assert.Throws<OperacionRechazadaException>(() => cuenta.Aceptar(ejecutar));
        Assert.Equal((1_000.00m, 0m), Escenario.Estado(cuenta));
    }

    [Fact]
    public void EjecutarYRevertir_DevuelveElMontoYLaPenalizacion()
    {
        CuentaInversion cuenta = Escenario.Inversion(20_000.00m);
        var estadoOriginal = Escenario.Estado(cuenta);
        Retiro retiro = Escenario.NuevoRetiro(cuenta, 1_000.00m);

        cuenta.Aceptar(new EjecutarTransaccion(retiro));
        cuenta.Aceptar(new RevertirTransaccion(retiro));

        Assert.Equal(estadoOriginal, Escenario.Estado(cuenta));
    }

    [Fact]
    public void Auditar_SenalaElRetiroAnticipadoYSuPenalizacionSinModificarLaCuenta()
    {
        CuentaInversion cuenta = Escenario.Inversion(20_000.00m);
        Retiro retiro = Escenario.NuevoRetiro(cuenta, 1_000.00m);
        cuenta.Aceptar(new EjecutarTransaccion(retiro));
        var estadoAntes = Escenario.Estado(cuenta);

        AuditarTransaccion auditar = new(retiro);
        cuenta.Aceptar(auditar);

        RegistroAuditoria registro = Assert.Single(auditar.Registros);
        Assert.Equal(EstadoTransaccion.Aplicada, registro.Estado);
        Assert.True(registro.Conforme);
        Assert.Equal("retiro anticipado (vence el 2027-03-31): penalización 20.00; saldo tras la transacción 18980.00",
                     registro.Observacion);
        Assert.Equal(estadoAntes, Escenario.Estado(cuenta));
    }

    [Fact]
    public void Auditar_MarcaNoConformeLaReversionQueDejaLaCuentaEnNegativo()
    {
        CuentaInversion cuenta = Escenario.Inversion(0m);
        Deposito deposito = Escenario.NuevoDeposito(cuenta, 1_000.00m);
        cuenta.Aceptar(new EjecutarTransaccion(deposito));
        cuenta.Aceptar(new EjecutarTransaccion(Escenario.NuevoRetiro(cuenta, 1_000.00m, Escenario.Vencimiento)));

        cuenta.Aceptar(new RevertirTransaccion(deposito)); // compensación obligatoria: queda en −1000
        AuditarTransaccion auditar = new(deposito);
        cuenta.Aceptar(auditar);

        RegistroAuditoria registro = Assert.Single(auditar.Registros);
        Assert.Equal(EstadoTransaccion.Revertida, registro.Estado);
        Assert.False(registro.Conforme);
    }
}
