using Banco.Procesamiento.Cuentas;

namespace Banco.Procesamiento.Transacciones;

/// <summary>
/// Un débito o un crédito sobre una cuenta. Toda transacción se describe con
/// sus movimientos; así los visitantes solo distinguen débito de crédito y no
/// dependen del tipo de transacción.
/// </summary>
public sealed record Movimiento
{
    public Movimiento(Cuenta cuenta, Naturaleza naturaleza, decimal monto)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(monto);
        if (monto != Dinero.Redondear(monto))
        {
            throw new ArgumentException("El monto no puede tener fracciones de centavo.", nameof(monto));
        }

        Cuenta = cuenta;
        Naturaleza = naturaleza;
        Monto = monto;
    }

    /// <summary>Cuenta afectada.</summary>
    public Cuenta Cuenta { get; }

    /// <summary>Débito o crédito.</summary>
    public Naturaleza Naturaleza { get; }

    /// <summary>Monto pedido, sin los cargos que pueda cobrar la cuenta.</summary>
    public decimal Monto { get; }
}
