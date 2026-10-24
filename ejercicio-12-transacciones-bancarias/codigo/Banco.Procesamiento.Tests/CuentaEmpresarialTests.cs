using Banco.Procesamiento.Cuentas;
using Banco.Procesamiento.Transacciones;
using Banco.Procesamiento.Visitantes;

namespace Banco.Procesamiento.Tests;

/// <summary>
/// Las tres operaciones sobre CuentaEmpresarial: comisión por débito y
/// autorización para montos altos.
/// </summary>
public sealed class CuentaEmpresarialTests
{
    [Fact]
    public void Ejecutar_DebitoPagaLaComision()
    {
        CuentaEmpresarial cuenta = Escenario.Empresarial(50_000.00m);

        cuenta.Aceptar(new EjecutarTransaccion(Escenario.NuevoRetiro(cuenta, 1_000.00m)));

        Assert.Equal((48_995.00m, 5.00m, 0), Escenario.Estado(cuenta)); // 0,5 % de 1000
    }

    [Fact]
    public void Ejecutar_CreditoNoPagaComision()
    {
        CuentaEmpresarial cuenta = Escenario.Empresarial(50_000.00m);

        cuenta.Aceptar(new EjecutarTransaccion(Escenario.NuevoDeposito(cuenta, 1_000.00m)));

        Assert.Equal((51_000.00m, 0m, 0), Escenario.Estado(cuenta));
    }

    [Fact]
    public void Ejecutar_RechazaElMontoAltoSinAutorizacion()
    {
        CuentaEmpresarial cuenta = Escenario.Empresarial(50_000.00m);

        EjecutarTransaccion ejecutar = new(Escenario.NuevoRetiro(cuenta, 15_000.00m));
        OperacionRechazadaException rechazo =
            Assert.Throws<OperacionRechazadaException>(() => cuenta.Aceptar(ejecutar));

        Assert.Equal("los débitos de más de 10000.00 necesitan la autorización de otra persona", rechazo.Motivo);
        Assert.Equal((50_000.00m, 0m, 0), Escenario.Estado(cuenta));
    }

    [Fact]
    public void Ejecutar_RechazaQueQuienPideLaTransaccionSeAutoriceASiMismo()
    {
        CuentaEmpresarial cuenta = Escenario.Empresarial(50_000.00m);

        EjecutarTransaccion ejecutar = new(Escenario.NuevoRetiro(cuenta, 15_000.00m, autorizadaPor: Escenario.Usuario));

        Assert.Throws<OperacionRechazadaException>(() => cuenta.Aceptar(ejecutar));
        Assert.Equal((50_000.00m, 0m, 0), Escenario.Estado(cuenta));
    }

    [Fact]
    public void Ejecutar_UnMontoIgualAlUmbralNoNecesitaAutorizacion()
    {
        CuentaEmpresarial cuenta = Escenario.Empresarial(50_000.00m);

        cuenta.Aceptar(new EjecutarTransaccion(Escenario.NuevoRetiro(cuenta, 10_000.00m)));

        Assert.Equal((39_950.00m, 50.00m, 0), Escenario.Estado(cuenta));
    }

    [Fact]
    public void Ejecutar_AceptaElMontoAltoAutorizadoYLoCuenta()
    {
        CuentaEmpresarial cuenta = Escenario.Empresarial(50_000.00m);

        cuenta.Aceptar(new EjecutarTransaccion(
            Escenario.NuevoRetiro(cuenta, 15_000.00m, autorizadaPor: "gerente.finanzas")));

        Assert.Equal((34_925.00m, 75.00m, 1), Escenario.Estado(cuenta));
    }

    [Fact]
    public void EjecutarYRevertir_DevuelveElMontoYLaComisionYDescuentaLaAutorizacion()
    {
        CuentaEmpresarial cuenta = Escenario.Empresarial(50_000.00m);
        var estadoOriginal = Escenario.Estado(cuenta);
        Retiro retiro = Escenario.NuevoRetiro(cuenta, 15_000.00m, autorizadaPor: "gerente.finanzas");

        cuenta.Aceptar(new EjecutarTransaccion(retiro));
        cuenta.Aceptar(new RevertirTransaccion(retiro));

        Assert.Equal(estadoOriginal, Escenario.Estado(cuenta));
    }

    [Fact]
    public void Auditar_RegistraLaComisionYQuienAutorizoSinModificarLaCuenta()
    {
        CuentaEmpresarial cuenta = Escenario.Empresarial(50_000.00m);
        Retiro retiro = Escenario.NuevoRetiro(cuenta, 15_000.00m, autorizadaPor: "gerente.finanzas");
        cuenta.Aceptar(new EjecutarTransaccion(retiro));
        var estadoAntes = Escenario.Estado(cuenta);

        AuditarTransaccion auditar = new(retiro);
        cuenta.Aceptar(auditar);

        RegistroAuditoria registro = Assert.Single(auditar.Registros);
        Assert.Equal(EstadoTransaccion.Aplicada, registro.Estado);
        Assert.True(registro.Conforme);
        Assert.Equal("comisión 75.00; monto alto autorizado por gerente.finanzas; saldo tras la transacción 34925.00",
                     registro.Observacion);
        Assert.Equal(estadoAntes, Escenario.Estado(cuenta));
    }
}
