namespace Banco.Procesamiento.Visitantes;

/// <summary>
/// Qué le pasó a una transacción en una cuenta concreta, según su historial.
/// Es un estado por cuenta, no por transacción: una transferencia rechazada
/// puede quedar como Revertida en la cuenta que alcanzó a aplicar su débito y
/// como NoAplicada en la otra.
/// </summary>
public enum EstadoTransaccion
{
    /// <summary>La cuenta tiene el asiento y sigue vigente.</summary>
    Aplicada,

    /// <summary>
    /// La cuenta tiene el asiento, pero se deshizo: porque se pidió revertir la
    /// transacción o porque otra cuenta la rechazó y el procesador deshizo lo ya
    /// aplicado («todo o nada»). En los dos casos queda el rastro de la
    /// compensación, como en un asiento contable de anulación.
    /// </summary>
    Revertida,

    /// <summary>
    /// La cuenta no tiene asiento: la transacción nunca se aplicó en ella. La
    /// rechazó esta cuenta, la rechazó otra antes de llegar a esta, o no se
    /// ejecutó. El motivo del rechazo está en ResultadoTransaccion.
    /// </summary>
    NoAplicada,
}
