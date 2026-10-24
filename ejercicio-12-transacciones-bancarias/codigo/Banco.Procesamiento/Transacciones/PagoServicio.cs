using Banco.Procesamiento.Cuentas;

namespace Banco.Procesamiento.Transacciones;

/// <summary>Pago de un servicio (electricidad, agua, telefonía…): un débito con su concepto.</summary>
public sealed class PagoServicio : Transaccion
{
    public PagoServicio(Cuenta cuenta, string servicio, decimal monto, string usuario, DateTime fecha,
                        string? autorizadaPor = null)
        : base(usuario, fecha, autorizadaPor, [new Movimiento(cuenta, Naturaleza.Debito, monto)])
    {
        Cuenta = cuenta;
        Servicio = servicio;
        Monto = monto;
    }

    public Cuenta Cuenta { get; }

    /// <summary>Servicio que se paga.</summary>
    public string Servicio { get; }

    public decimal Monto { get; }

    /// <inheritdoc />
    public override string Descripcion =>
        $"Pago de {Servicio} por {Dinero.Formato(Monto)} desde {Cuenta.Numero}";
}
