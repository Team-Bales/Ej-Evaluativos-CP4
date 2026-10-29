using Banco.Procesamiento.Cuentas;
using Banco.Procesamiento.Transacciones;
using Banco.Procesamiento.Visitantes;

namespace Banco.Procesamiento.Tests;

/// <summary>
/// El Iterator del inciso c: las transacciones de una cuenta se recorren sin
/// exponer su lista interna, completas o filtradas.
/// </summary>
public sealed class HistorialTests
{
    private readonly ProcesadorTransacciones _procesador = new();

    [Fact]
    public void Historial_RecorreLosAsientosEnOrdenIncluidosLosRevertidos()
    {
        CuentaCorriente cuenta = Escenario.Corriente(300.00m);
        Retiro retiro = Escenario.NuevoRetiro(cuenta, 100.00m);
        Deposito deposito = Escenario.NuevoDeposito(cuenta, 50.00m);
        _procesador.Ejecutar(retiro);
        _procesador.Ejecutar(deposito);
        _procesador.Revertir(retiro);

        List<Asiento> historial = cuenta.Historial().ToList();

        Assert.Equal([retiro, deposito], historial.Select(a => (Transaccion)a.Transaccion));
        Assert.Equal([true, false], historial.Select(a => a.Revertido));
    }

    [Fact]
    public void Historial_NoExponeLaListaInterna()
    {
        CuentaAhorro cuenta = Escenario.Ahorro();
        _procesador.Ejecutar(Escenario.NuevoDeposito(cuenta, 50.00m));

        IEnumerable<Asiento> historial = cuenta.Historial();

        Assert.Null(historial as ICollection<Asiento>); // no se puede convertir para agregar o quitar asientos
        Assert.Single(historial);
    }

    [Fact]
    public void Historial_FiltraPorFecha()
    {
        CuentaAhorro cuenta = Escenario.Ahorro(1_000.00m);
        _procesador.Ejecutar(Escenario.NuevoRetiro(cuenta, 10.00m, Escenario.Hoy));
        _procesador.Ejecutar(Escenario.NuevoRetiro(cuenta, 20.00m, Escenario.Hoy.AddMonths(1)));

        Asiento deNoviembre = Assert.Single(cuenta.Historial(new DateTime(2026, 11, 1), new DateTime(2026, 12, 1)));

        Assert.Equal(20.00m, deNoviembre.Monto);
    }

    [Fact]
    public void Historial_FiltraPorTipoDeTransaccion()
    {
        CuentaCorriente origen = Escenario.Corriente(300.00m);
        CuentaAhorro destino = Escenario.Ahorro(0m);
        _procesador.Ejecutar(Escenario.NuevoRetiro(origen, 10.00m));
        Transferencia transferencia = new(origen, destino, 20.00m, Escenario.Usuario, Escenario.Hoy);
        _procesador.Ejecutar(transferencia);

        Asiento soloLaTransferencia = Assert.Single(origen.Historial<Transferencia>());

        Assert.Same(transferencia, soloLaTransferencia.Transaccion);
    }
}
