using Banco.Procesamiento.Cuentas;

namespace Banco.Procesamiento.Transacciones;

/// <summary>Depósito: un crédito en la cuenta.</summary>
public sealed class Deposito : Transaccion
{
    public Deposito(Cuenta cuenta, decimal monto, string usuario, DateTime fecha)
        : base(usuario, fecha, autorizadaPor: null, [new Movimiento(cuenta, Naturaleza.Credito, monto)])
    {
        Cuenta = cuenta;
        Monto = monto;
    }

    public Cuenta Cuenta { get; }

    public decimal Monto { get; }

    /// <inheritdoc />
    public override string Descripcion => $"Depósito de {Dinero.Formato(Monto)} en {Cuenta.Numero}";
}
