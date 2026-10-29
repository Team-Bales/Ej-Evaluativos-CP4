using Banco.Procesamiento.Transacciones;

namespace Banco.Procesamiento.Cuentas;

/// <summary>
/// Lo que una transacción le hizo a una cuenta: el movimiento (débito o
/// crédito y su monto) y el cargo que cobró la regla de la cuenta (comisión,
/// penalización o cargo por sobregiro). Es lo que se guarda para poder
/// revertir: el efecto exacto que se aplicó, no una foto del estado anterior.
/// Así revertir una transacción no borra lo que otras hicieron después en la
/// misma cuenta. También guarda el saldo en que la transacción (y, si se
/// revirtió, su reversión) dejó la cuenta, para auditarla.
/// </summary>
public sealed class Asiento
{
    internal Asiento(Transaccion transaccion, Naturaleza naturaleza, decimal monto, decimal cargo, decimal saldoAnterior)
    {
        Transaccion = transaccion;
        Naturaleza = naturaleza;
        Monto = monto;
        Cargo = cargo;
        SaldoResultante = saldoAnterior + Efecto;
    }

    /// <summary>La transacción que dejó el asiento (quién, qué y cuándo).</summary>
    public Transaccion Transaccion { get; }

    /// <summary>Débito o crédito.</summary>
    public Naturaleza Naturaleza { get; }

    /// <summary>Monto del movimiento, sin el cargo.</summary>
    public decimal Monto { get; }

    /// <summary>Comisión, penalización o cargo por sobregiro cobrado; 0 si no hubo.</summary>
    public decimal Cargo { get; }

    /// <summary>Saldo de la cuenta justo después de aplicar el movimiento.</summary>
    public decimal SaldoResultante { get; }

    /// <summary>True cuando RevertirTransaccion ya deshizo este asiento.</summary>
    public bool Revertido { get; private set; }

    /// <summary>Saldo de la cuenta justo después de revertir; null si no se revirtió.</summary>
    public decimal? SaldoTrasRevertir { get; private set; }

    /// <summary>Cambio que el asiento produjo en el saldo: +monto en un crédito, −(monto + cargo) en un débito.</summary>
    public decimal Efecto => Naturaleza == Naturaleza.Credito ? Monto : -(Monto + Cargo);

    internal void MarcarRevertido(decimal saldoTrasRevertir)
    {
        Revertido = true;
        SaldoTrasRevertir = saldoTrasRevertir;
    }
}
