using Banco.Procesamiento.Cuentas;

namespace Banco.Procesamiento.Visitantes;

/// <summary>
/// Una regla de negocio de la cuenta impide aplicar su movimiento. La lanza
/// EjecutarTransaccion antes de tocar la cuenta; ProcesadorTransacciones la
/// captura y revierte las cuentas que ya aplicaron la transacción.
/// </summary>
public sealed class OperacionRechazadaException(Cuenta cuenta, string motivo)
    : Exception($"{cuenta.Numero}: {motivo}")
{
    /// <summary>Cuenta que rechazó el movimiento.</summary>
    public Cuenta Cuenta { get; } = cuenta;

    /// <summary>Regla que lo impidió, en palabras.</summary>
    public string Motivo { get; } = motivo;
}
