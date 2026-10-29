using Banco.Procesamiento.Cuentas;
using Banco.Procesamiento.Transacciones;
using Banco.Procesamiento.Visitantes;

namespace Banco.Procesamiento.Tests;

/// <summary>
/// Las tres operaciones sobre CuentaCorriente: sobregiro hasta un tope y un
/// cargo fijo por cada débito que deja la cuenta en negativo.
/// </summary>
public sealed class CuentaCorrienteTests
{
    [Fact]
    public void Ejecutar_DebitoQueNoDejaLaCuentaEnNegativoNoCobraCargo()
    {
        CuentaCorriente cuenta = Escenario.Corriente(300.00m);

        cuenta.Aceptar(new EjecutarTransaccion(Escenario.NuevoRetiro(cuenta, 100.00m)));

        Assert.Equal((200.00m, 0), Escenario.Estado(cuenta));
    }

    [Fact]
    public void Ejecutar_DebitoQueDejaLaCuentaEnNegativoCobraElCargoPorSobregiro()
    {
        CuentaCorriente cuenta = Escenario.Corriente(300.00m);

        cuenta.Aceptar(new EjecutarTransaccion(Escenario.NuevoRetiro(cuenta, 600.00m)));

        Assert.Equal((-305.00m, 1), Escenario.Estado(cuenta)); // 300 − 600 − 5 de cargo
    }

    [Fact]
    public void Ejecutar_PermiteLlegarJustoAlTopeDeSobregiroConElCargoIncluido()
    {
        CuentaCorriente cuenta = Escenario.Corriente(300.00m);

        cuenta.Aceptar(new EjecutarTransaccion(Escenario.NuevoRetiro(cuenta, 795.00m)));

        Assert.Equal((-500.00m, 1), Escenario.Estado(cuenta));
    }

    [Fact]
    public void Ejecutar_RechazaElDebitoQuePasaDelTopeDeSobregiro()
    {
        CuentaCorriente cuenta = Escenario.Corriente(300.00m);

        EjecutarTransaccion ejecutar = new(Escenario.NuevoRetiro(cuenta, 796.00m));
        OperacionRechazadaException rechazo =
            Assert.Throws<OperacionRechazadaException>(() => cuenta.Aceptar(ejecutar));

        Assert.StartsWith("supera el sobregiro permitido de 500.00", rechazo.Motivo);
        Assert.Equal((300.00m, 0), Escenario.Estado(cuenta));
    }

    [Fact]
    public void EjecutarYRevertir_DevuelveElMontoYElCargoYDescuentaElSobregiro()
    {
        CuentaCorriente cuenta = Escenario.Corriente(300.00m);
        var estadoOriginal = Escenario.Estado(cuenta);
        Retiro retiro = Escenario.NuevoRetiro(cuenta, 600.00m);

        cuenta.Aceptar(new EjecutarTransaccion(retiro));
        cuenta.Aceptar(new RevertirTransaccion(retiro));

        Assert.Equal(estadoOriginal, Escenario.Estado(cuenta));
    }

    [Fact]
    public void Auditar_InformaElSobregiroYElCargoSinModificarLaCuenta()
    {
        CuentaCorriente cuenta = Escenario.Corriente(300.00m);
        Retiro retiro = Escenario.NuevoRetiro(cuenta, 600.00m);
        cuenta.Aceptar(new EjecutarTransaccion(retiro));
        var estadoAntes = Escenario.Estado(cuenta);

        AuditarTransaccion auditar = new(retiro);
        cuenta.Aceptar(auditar);

        RegistroAuditoria registro = Assert.Single(auditar.Registros);
        Assert.Equal(EstadoTransaccion.Aplicada, registro.Estado);
        Assert.True(registro.Conforme);
        Assert.Equal("débito que dejó la cuenta en sobregiro; cargo 5.00; saldo tras la transacción -305.00",
                     registro.Observacion);
        Assert.Equal(estadoAntes, Escenario.Estado(cuenta));
    }

    [Fact]
    public void Auditar_JuzgaLaTransaccionYNoElEstadoActualDeLaCuenta()
    {
        CuentaCorriente cuenta = Escenario.Corriente(300.00m);
        Retiro retiro = Escenario.NuevoRetiro(cuenta, 600.00m);
        cuenta.Aceptar(new EjecutarTransaccion(retiro));                                   // −305
        cuenta.Aceptar(new EjecutarTransaccion(Escenario.NuevoDeposito(cuenta, 1_000.00m))); // 695: ya sin sobregiro

        AuditarTransaccion auditar = new(retiro);
        cuenta.Aceptar(auditar);

        // El retiro se juzga por lo que hizo: dejó la cuenta en −305 y cobró el cargo que correspondía.
        RegistroAuditoria registro = Assert.Single(auditar.Registros);
        Assert.True(registro.Conforme);
        Assert.EndsWith("saldo tras la transacción -305.00", registro.Observacion);
    }
}
