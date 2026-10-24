namespace Banco.Procesamiento.Transacciones;

/// <summary>Sentido de un movimiento sobre una cuenta.</summary>
public enum Naturaleza
{
    /// <summary>Sale dinero de la cuenta (retiro, transferencia saliente, pago).</summary>
    Debito,

    /// <summary>Entra dinero en la cuenta (depósito, transferencia entrante).</summary>
    Credito,
}
