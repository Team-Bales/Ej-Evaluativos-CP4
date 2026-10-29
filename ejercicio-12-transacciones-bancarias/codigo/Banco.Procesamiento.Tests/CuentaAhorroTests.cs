using Banco.Procesamiento.Cuentas;
using Banco.Procesamiento.Transacciones;
using Banco.Procesamiento.Visitantes;

namespace Banco.Procesamiento.Tests;

/// <summary>
/// Las tres operaciones sobre CuentaAhorro: un límite de débitos al mes y nunca
/// saldo negativo.
/// </summary>
public sealed class CuentaAhorroTests
{
    [Fact]
    public void Ejecutar_RetiroDescuentaElSaldoYCuentaComoRetiroDelMes()
    {
        CuentaAhorro cuenta = Escenario.Ahorro(1_000.00m);

        cuenta.Aceptar(new EjecutarTransaccion(Escenario.NuevoRetiro(cuenta, 100.00m)));

        Assert.Equal((900.00m, 1), Escenario.Estado(cuenta));
    }

    [Fact]
    public void Ejecutar_DepositoSumaAlSaldoYNoCuentaComoRetiro()
    {
        CuentaAhorro cuenta = Escenario.Ahorro(1_000.00m);

        cuenta.Aceptar(new EjecutarTransaccion(Escenario.NuevoDeposito(cuenta, 250.00m)));

        Assert.Equal((1_250.00m, 0), Escenario.Estado(cuenta));
    }

    [Fact]
    public void Ejecutar_RechazaElDebitoQuePasaDelLimiteMensual()
    {
        CuentaAhorro cuenta = Escenario.Ahorro(1_000.00m);
        for (int i = 0; i < cuenta.LimiteRetirosMensuales; i++)
        {
            cuenta.Aceptar(new EjecutarTransaccion(Escenario.NuevoRetiro(cuenta, 10.00m)));
        }

        EjecutarTransaccion cuartoRetiro = new(Escenario.NuevoRetiro(cuenta, 10.00m));
        OperacionRechazadaException rechazo =
            Assert.Throws<OperacionRechazadaException>(() => cuenta.Aceptar(cuartoRetiro));

        Assert.Equal("alcanzó el límite de 3 retiros del mes", rechazo.Motivo);
        Assert.Equal((970.00m, 3), Escenario.Estado(cuenta)); // el rechazo no tocó la cuenta
    }

    [Fact]
    public void Ejecutar_UnMesNuevoEmpiezaLaCuentaDeRetirosDeCero()
    {
        CuentaAhorro cuenta = Escenario.Ahorro(1_000.00m);
        for (int i = 0; i < cuenta.LimiteRetirosMensuales; i++)
        {
            cuenta.Aceptar(new EjecutarTransaccion(Escenario.NuevoRetiro(cuenta, 10.00m)));
        }

        DateTime mesSiguiente = Escenario.Hoy.AddMonths(1);
        cuenta.Aceptar(new EjecutarTransaccion(Escenario.NuevoRetiro(cuenta, 10.00m, mesSiguiente)));

        Assert.Equal((960.00m, 1), Escenario.Estado(cuenta));
        Assert.Equal(new DateOnly(2026, 11, 1), cuenta.MesDeRetiros);
    }

    [Fact]
    public void Ejecutar_RechazaElRetiroSinSaldoSuficiente()
    {
        CuentaAhorro cuenta = Escenario.Ahorro(50.00m);

        EjecutarTransaccion ejecutar = new(Escenario.NuevoRetiro(cuenta, 60.00m));

        Assert.Throws<OperacionRechazadaException>(() => cuenta.Aceptar(ejecutar));
        Assert.Equal((50.00m, 0), Escenario.Estado(cuenta));
    }

    [Fact]
    public void Ejecutar_PermiteDejarElSaldoJustoEnCero()
    {
        CuentaAhorro cuenta = Escenario.Ahorro(50.00m);

        cuenta.Aceptar(new EjecutarTransaccion(Escenario.NuevoRetiro(cuenta, 50.00m)));

        Assert.Equal((0m, 1), Escenario.Estado(cuenta));
    }

    [Fact]
    public void EjecutarYRevertir_UnRetiroDejaElSaldoYLosRetirosDelMesComoEstaban()
    {
        CuentaAhorro cuenta = Escenario.Ahorro(1_000.00m);
        var estadoOriginal = Escenario.Estado(cuenta);
        Retiro retiro = Escenario.NuevoRetiro(cuenta, 100.00m);

        cuenta.Aceptar(new EjecutarTransaccion(retiro));
        cuenta.Aceptar(new RevertirTransaccion(retiro));

        Assert.Equal(estadoOriginal, Escenario.Estado(cuenta));
    }

    [Fact]
    public void EjecutarYRevertir_UnDepositoDejaElSaldoComoEstaba()
    {
        CuentaAhorro cuenta = Escenario.Ahorro(1_000.00m);
        var estadoOriginal = Escenario.Estado(cuenta);
        Deposito deposito = Escenario.NuevoDeposito(cuenta, 250.00m);

        cuenta.Aceptar(new EjecutarTransaccion(deposito));
        cuenta.Aceptar(new RevertirTransaccion(deposito));

        Assert.Equal(estadoOriginal, Escenario.Estado(cuenta));
    }

    [Fact]
    public void Revertir_UnRetiroDeUnMesAnteriorNoCambiaLaCuentaDelMesEnCurso()
    {
        CuentaAhorro cuenta = Escenario.Ahorro(1_000.00m);
        Retiro retiroDeOctubre = Escenario.NuevoRetiro(cuenta, 100.00m);
        cuenta.Aceptar(new EjecutarTransaccion(retiroDeOctubre));
        cuenta.Aceptar(new EjecutarTransaccion(Escenario.NuevoRetiro(cuenta, 100.00m, Escenario.Hoy.AddMonths(1))));

        cuenta.Aceptar(new RevertirTransaccion(retiroDeOctubre));

        Assert.Equal((900.00m, 1), Escenario.Estado(cuenta)); // noviembre sigue con su retiro
    }

    [Fact]
    public void Auditar_RegistraQuienQueCuandoYResultadoSinModificarLaCuenta()
    {
        CuentaAhorro cuenta = Escenario.Ahorro(1_000.00m);
        Retiro retiro = Escenario.NuevoRetiro(cuenta, 100.00m);
        cuenta.Aceptar(new EjecutarTransaccion(retiro));
        var estadoAntes = Escenario.Estado(cuenta);

        AuditarTransaccion auditar = new(retiro);
        cuenta.Aceptar(auditar);

        RegistroAuditoria registro = Assert.Single(auditar.Registros);
        Assert.Equal(Escenario.Usuario, registro.Usuario);                    // quién
        Assert.Equal("Retiro de 100.00 de AHO-001", registro.Descripcion);    // qué
        Assert.Equal(Escenario.Hoy, registro.Fecha);                          // cuándo
        Assert.Equal(EstadoTransaccion.Aplicada, registro.Estado);            // resultado
        Assert.True(registro.Conforme);
        Assert.Equal("débito del mes 2026-10; saldo tras la transacción 900.00", registro.Observacion);
        Assert.Equal(estadoAntes, Escenario.Estado(cuenta));                  // no modificó la cuenta
    }
}
