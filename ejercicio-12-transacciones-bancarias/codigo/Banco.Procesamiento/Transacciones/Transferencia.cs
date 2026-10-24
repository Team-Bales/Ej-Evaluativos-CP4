using Banco.Procesamiento.Cuentas;

namespace Banco.Procesamiento.Transacciones;

/// <summary>
/// Transferencia: un débito en la cuenta de origen y un crédito en la de
/// destino. Es la transacción que afecta a dos cuentas, por eso es la que
/// necesita «todo o nada» (ProcesadorTransacciones). Una transferencia a la
/// misma cuenta la rechaza el constructor de Transaccion: como mucho un
/// movimiento por cuenta.
/// </summary>
public sealed class Transferencia : Transaccion
{
    public Transferencia(Cuenta origen, Cuenta destino, decimal monto, string usuario, DateTime fecha,
                         string? autorizadaPor = null)
        : base(usuario, fecha, autorizadaPor,
               [new Movimiento(origen, Naturaleza.Debito, monto), new Movimiento(destino, Naturaleza.Credito, monto)])
    {
        Origen = origen;
        Destino = destino;
        Monto = monto;
    }

    public Cuenta Origen { get; }

    public Cuenta Destino { get; }

    public decimal Monto { get; }

    /// <inheritdoc />
    public override string Descripcion =>
        $"Transferencia de {Dinero.Formato(Monto)} de {Origen.Numero} a {Destino.Numero}";
}
