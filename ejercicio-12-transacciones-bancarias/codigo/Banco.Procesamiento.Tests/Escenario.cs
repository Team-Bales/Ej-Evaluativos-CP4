using Banco.Procesamiento.Cuentas;
using Banco.Procesamiento.Transacciones;

namespace Banco.Procesamiento.Tests;

/// <summary>
/// Cuentas, fechas y transacciones de las pruebas, con los mismos parámetros
/// que la simulación (Program.cs).
/// </summary>
internal static class Escenario
{
    public static readonly DateTime Hoy = new(2026, 10, 15);
    public static readonly DateTime Vencimiento = new(2027, 3, 31);
    public const string Usuario = "cajero.pruebas";

    public static CuentaAhorro Ahorro(decimal saldo = 1_000.00m) => new("AHO-001", "Ana Pérez", saldo);

    public static CuentaCorriente Corriente(decimal saldo = 300.00m) => new("COR-001", "Luis Gómez", saldo);

    public static CuentaInversion Inversion(decimal saldo = 20_000.00m) => new("INV-001", "María Díaz", saldo, Vencimiento);

    public static CuentaEmpresarial Empresarial(decimal saldo = 50_000.00m) => new("EMP-001", "Bales S.A.", saldo);

    public static Deposito NuevoDeposito(Cuenta cuenta, decimal monto) => new(cuenta, monto, Usuario, Hoy);

    public static Retiro NuevoRetiro(Cuenta cuenta, decimal monto, DateTime? fecha = null, string? autorizadaPor = null) =>
        new(cuenta, monto, Usuario, fecha ?? Hoy, autorizadaPor);

    // Estado observable de cada tipo de cuenta: el saldo y sus contadores.
    // Ejecutar y después revertir debe dejarlo exactamente igual.

    public static (decimal Saldo, int RetirosDelMes) Estado(CuentaAhorro cuenta) =>
        (cuenta.Saldo, cuenta.RetirosDelMes);

    public static (decimal Saldo, int VecesEnSobregiro) Estado(CuentaCorriente cuenta) =>
        (cuenta.Saldo, cuenta.VecesEnSobregiro);

    public static (decimal Saldo, decimal PenalizacionesCobradas) Estado(CuentaInversion cuenta) =>
        (cuenta.Saldo, cuenta.PenalizacionesCobradas);

    public static (decimal Saldo, decimal ComisionesCobradas, int OperacionesAutorizadas) Estado(CuentaEmpresarial cuenta) =>
        (cuenta.Saldo, cuenta.ComisionesCobradas, cuenta.OperacionesAutorizadas);
}
