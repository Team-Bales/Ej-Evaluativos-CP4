using Banco.Procesamiento.Cuentas;
using Banco.Procesamiento.Transacciones;
using Banco.Procesamiento.Visitantes;

namespace Banco.Procesamiento.Tests;

/// <summary>
/// El procesador como cliente del patrón: transacciones sobre varias cuentas
/// («todo o nada») y reversiones que deshacen solo su propio efecto.
/// </summary>
public sealed class ProcesadorTransaccionesTests
{
    private readonly ProcesadorTransacciones _procesador = new();

    [Fact]
    public void Transferencia_AplicaElDebitoEnElOrigenYElCreditoEnElDestino()
    {
        CuentaAhorro origen = Escenario.Ahorro(1_000.00m);
        CuentaCorriente destino = Escenario.Corriente(300.00m);

        ResultadoTransaccion resultado =
            _procesador.Ejecutar(new Transferencia(origen, destino, 200.00m, Escenario.Usuario, Escenario.Hoy));

        Assert.True(resultado.FueAplicada);
        Assert.Equal((800.00m, 1), Escenario.Estado(origen));
        Assert.Equal((500.00m, 0), Escenario.Estado(destino));
    }

    [Fact]
    public void Transferencia_SiElCreditoSeRechazaRevierteElDebitoYaAplicado()
    {
        CuentaCorriente origen = Escenario.Corriente(300.00m);
        CuentaInversion destino = Escenario.Inversion(20_000.00m);
        Transferencia transferencia = new(origen, destino, 50.00m, Escenario.Usuario, Escenario.Hoy); // < depósito mínimo

        ResultadoTransaccion resultado = _procesador.Ejecutar(transferencia);

        Assert.False(resultado.FueAplicada);
        Assert.Equal("INV-001: el depósito mínimo es 100.00", resultado.MotivoRechazo);
        Assert.Equal((300.00m, 0), Escenario.Estado(origen));      // el débito se deshizo
        Assert.Equal((20_000.00m, 0m), Escenario.Estado(destino)); // el crédito nunca se aplicó
        IReadOnlyList<RegistroAuditoria> auditoria = _procesador.Auditar(transferencia);
        Assert.Equal([EstadoTransaccion.Revertida, EstadoTransaccion.NoAplicada], auditoria.Select(r => r.Estado));
    }

    [Fact]
    public void Ejecutar_SiFallaPorOtraCausaDeshaceLoAplicadoYPropagaElError()
    {
        CuentaAhorro origen = Escenario.Ahorro(1_000.00m);
        CuentaCorriente destino = Escenario.Corriente(300.00m);
        Transferencia transferencia = new(origen, destino, 200.00m, Escenario.Usuario, Escenario.Hoy);
        destino.Aceptar(new EjecutarTransaccion(transferencia)); // el crédito ya estaba aplicado

        // El débito se aplica en el origen y el destino falla con un error que no es un rechazo.
        Assert.Throws<InvalidOperationException>(() => _procesador.Ejecutar(transferencia));

        Assert.Equal((1_000.00m, 0), Escenario.Estado(origen)); // el débito se deshizo
    }

    [Fact]
    public void Revertir_NoBorraLoQueOtraTransaccionHizoDespuesEnLaMismaCuenta()
    {
        CuentaAhorro cuenta = Escenario.Ahorro(1_000.00m);
        Retiro retiro = Escenario.NuevoRetiro(cuenta, 100.00m);
        _procesador.Ejecutar(retiro);                                   // 900, 1 retiro
        _procesador.Ejecutar(Escenario.NuevoDeposito(cuenta, 50.00m));  // 950

        _procesador.Revertir(retiro);

        // Se deshace solo el retiro; el depósito posterior se conserva.
        // Restaurar una foto del estado anterior al retiro dejaría 1000 y perdería el depósito.
        Assert.Equal((1_050.00m, 0), Escenario.Estado(cuenta));
    }

    [Fact]
    public void Revertir_LaMismaTransaccionDosVecesFallaSinTocarLaCuenta()
    {
        CuentaAhorro cuenta = Escenario.Ahorro(1_000.00m);
        Retiro retiro = Escenario.NuevoRetiro(cuenta, 100.00m);
        _procesador.Ejecutar(retiro);
        _procesador.Revertir(retiro);

        Assert.Throws<InvalidOperationException>(() => _procesador.Revertir(retiro));
        Assert.Equal((1_000.00m, 0), Escenario.Estado(cuenta));
    }

    [Fact]
    public void Ejecutar_LaMismaTransaccionDosVecesFallaSinTocarLaCuenta()
    {
        CuentaAhorro cuenta = Escenario.Ahorro(1_000.00m);
        Retiro retiro = Escenario.NuevoRetiro(cuenta, 100.00m);
        _procesador.Ejecutar(retiro);

        Assert.Throws<InvalidOperationException>(() => _procesador.Ejecutar(retiro));
        Assert.Equal((900.00m, 1), Escenario.Estado(cuenta));
    }

    [Fact]
    public void Revertir_UnDepositoYaGastadoSeCompensaIgualYLaAuditoriaLoMarca()
    {
        CuentaAhorro cuenta = Escenario.Ahorro(100.00m);
        Deposito deposito = Escenario.NuevoDeposito(cuenta, 500.00m);
        _procesador.Ejecutar(deposito);                                   // 600
        _procesador.Ejecutar(Escenario.NuevoRetiro(cuenta, 550.00m));     // 50

        _procesador.Revertir(deposito);                                   // compensación obligatoria

        Assert.Equal(-450.00m, cuenta.Saldo);
        RegistroAuditoria registro = Assert.Single(_procesador.Auditar(deposito));
        Assert.Equal(EstadoTransaccion.Revertida, registro.Estado);
        Assert.False(registro.Conforme); // una cuenta de ahorro no puede quedar en negativo
    }
}
