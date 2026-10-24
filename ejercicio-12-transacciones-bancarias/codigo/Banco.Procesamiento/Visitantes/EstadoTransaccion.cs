namespace Banco.Procesamiento.Visitantes;

/// <summary>Qué le pasó a una transacción en una cuenta, según su historial.</summary>
public enum EstadoTransaccion
{
    /// <summary>La cuenta tiene el asiento y sigue vigente.</summary>
    Aplicada,

    /// <summary>La cuenta tiene el asiento, pero se deshizo.</summary>
    Revertida,

    /// <summary>
    /// La cuenta no tiene asiento: la transacción se rechazó (en esta cuenta o en
    /// otra) o no se ejecutó. El motivo del rechazo está en ResultadoTransaccion.
    /// </summary>
    NoAplicada,
}
