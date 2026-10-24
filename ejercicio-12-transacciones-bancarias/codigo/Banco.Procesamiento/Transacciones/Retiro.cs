using Banco.Procesamiento.Cuentas;

namespace Banco.Procesamiento.Transacciones;

/// <summary>Retiro: un débito en la cuenta.</summary>
public sealed class Retiro : Transaccion
{
    public Retiro(Cuenta cuenta, decimal monto, string usuario, DateTime fecha, string? autorizadaPor = null)
        : base(usuario, fecha, autorizadaPor, [new Movimiento(cuenta, Naturaleza.Debito, monto)])
    {
        Cuenta = cuenta;
        Monto = monto;
    }

    public Cuenta Cuenta { get; }

    public decimal Monto { get; }

    /// <inheritdoc />
    public override string Descripcion => $"Retiro de {Dinero.Formato(Monto)} de {Cuenta.Numero}";
}
