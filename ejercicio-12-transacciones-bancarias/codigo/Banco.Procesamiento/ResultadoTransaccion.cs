using Banco.Procesamiento.Transacciones;

namespace Banco.Procesamiento;

/// <summary>Resultado de ejecutar una transacción: aplicada en todas sus cuentas, o rechazada en todas.</summary>
/// <param name="Transaccion">La transacción ejecutada.</param>
/// <param name="FueAplicada">True si se aplicó; false si una cuenta la rechazó y se deshizo todo.</param>
/// <param name="MotivoRechazo">Cuenta y regla que la rechazaron; null si se aplicó.</param>
public sealed record ResultadoTransaccion(Transaccion Transaccion, bool FueAplicada, string? MotivoRechazo)
{
    public static ResultadoTransaccion Aplicada(Transaccion transaccion) => new(transaccion, true, null);

    public static ResultadoTransaccion Rechazada(Transaccion transaccion, string motivo) => new(transaccion, false, motivo);
}
