using Banco.Procesamiento.Cuentas;
using Banco.Procesamiento.Transacciones;
using Banco.Procesamiento.Visitantes;

namespace Banco.Procesamiento.Tests;

/// <summary>
/// La restricción: las transacciones corren en paralelo, las que comparten una
/// cuenta no se mezclan, y las transferencias cruzadas no se interbloquean.
/// Cada prueba lanza cientos de transacciones a la vez para provocar choques.
/// </summary>
public sealed class ConcurrenciaTests
{
    private const int Operaciones = 1_000;

    private readonly ProcesadorTransacciones _procesador = new();

    [Fact]
    public void DepositosYRetirosEnParaleloSobreLaMismaCuenta_DanElSaldoEsperado()
    {
        CuentaCorriente cuenta = Escenario.Corriente(10_000.00m);
        List<Transaccion> transacciones = Enumerable.Range(0, Operaciones)
            .Select(i => i % 2 == 0
                ? (Transaccion)Escenario.NuevoDeposito(cuenta, 10.00m)
                : Escenario.NuevoRetiro(cuenta, 10.00m))
            .ToList();

        IReadOnlyList<ResultadoTransaccion> resultados = _procesador.EjecutarEnParalelo(transacciones);

        Assert.All(resultados, r => Assert.True(r.FueAplicada));
        Assert.Equal((10_000.00m, 0), Escenario.Estado(cuenta)); // 500 depósitos y 500 retiros de 10
        Assert.Equal(Operaciones, cuenta.Historial().Count());   // ningún asiento perdido
    }

    [Fact]
    public async Task TransferenciasCruzadasEnParalelo_TerminanSinInterbloqueoYConservanElDinero()
    {
        CuentaCorriente a = new("COR-101", "Ana", 10_000.00m);
        CuentaCorriente b = new("COR-102", "Beto", 10_000.00m);
        List<Transaccion> transferencias = Enumerable.Range(0, Operaciones)
            .Select(i => (Transaccion)(i % 2 == 0
                ? new Transferencia(a, b, 7.00m, Escenario.Usuario, Escenario.Hoy)
                : new Transferencia(b, a, 7.00m, Escenario.Usuario, Escenario.Hoy)))
            .ToList();

        // Un interbloqueo no terminaría nunca: WaitAsync falla con TimeoutException a los 30 s.
        IReadOnlyList<ResultadoTransaccion> resultados = await Task.Run(() => _procesador.EjecutarEnParalelo(transferencias))
            .WaitAsync(TimeSpan.FromSeconds(30));

        Assert.All(resultados, r => Assert.True(r.FueAplicada));
        Assert.Equal(20_000.00m, a.Saldo + b.Saldo);                 // el dinero se conserva
        Assert.Equal((10_000.00m, 0), Escenario.Estado(a));          // 500 envíos y 500 recepciones de 7
    }

    [Fact]
    public void TransferenciasRechazadasEnParalelo_NoDejanRastroEnLaCuentaDeOrigen()
    {
        CuentaCorriente origen = Escenario.Corriente(1_000.00m);
        CuentaInversion destino = Escenario.Inversion(20_000.00m);
        // El débito se aplica en el origen y el crédito se rechaza (menos del depósito mínimo):
        // cada transferencia se deshace mientras sigue teniendo los candados de las dos cuentas.
        List<Transaccion> transacciones = Enumerable.Range(0, Operaciones)
            .Select(i => i % 2 == 0
                ? (Transaccion)new Transferencia(origen, destino, 50.00m, Escenario.Usuario, Escenario.Hoy)
                : Escenario.NuevoDeposito(origen, 1.00m))
            .ToList();

        IReadOnlyList<ResultadoTransaccion> resultados = _procesador.EjecutarEnParalelo(transacciones);

        Assert.Equal(Operaciones / 2, resultados.Count(r => !r.FueAplicada));
        Assert.Equal((1_500.00m, 0), Escenario.Estado(origen));      // solo cuentan los 500 depósitos de 1
        Assert.Equal((20_000.00m, 0m), Escenario.Estado(destino));
    }

    [Fact]
    public void ReversionesEnParaleloSobreLaMismaCuenta_DejanElEstadoOriginal()
    {
        CuentaEmpresarial cuenta = Escenario.Empresarial(50_000.00m);
        var estadoOriginal = Escenario.Estado(cuenta);
        List<Transaccion> retiros = Enumerable.Range(0, Operaciones)
            .Select(_ => (Transaccion)Escenario.NuevoRetiro(cuenta, 20.00m))
            .ToList();
        _procesador.EjecutarEnParalelo(retiros);

        Parallel.ForEach(retiros, retiro => _procesador.Revertir(retiro));

        Assert.Equal(estadoOriginal, Escenario.Estado(cuenta));      // saldo, comisiones y autorizaciones
    }

    [Fact]
    public void AuditoriasEnParalelo_ConUnVisitantePorTarea_NoPierdenRegistros()
    {
        CuentaAhorro ahorro = Escenario.Ahorro(1_000.00m);
        CuentaCorriente corriente = Escenario.Corriente(1_000.00m);
        List<Transaccion> transacciones = Enumerable.Range(0, Operaciones)
            .Select(i => (Transaccion)Escenario.NuevoDeposito(i % 2 == 0 ? ahorro : corriente, 1.00m))
            .ToList();
        _procesador.EjecutarEnParalelo(transacciones);

        IReadOnlyList<RegistroAuditoria> registros = _procesador.AuditarEnParalelo(transacciones);

        Assert.Equal(Operaciones, registros.Count);
        Assert.All(registros, r => Assert.Equal(EstadoTransaccion.Aplicada, r.Estado));
        Assert.Equal(transacciones.Select(t => t.Id), registros.Select(r => r.TransaccionId)); // en orden
    }
}
