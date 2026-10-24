using Banco.Procesamiento.Visitantes;

namespace Banco.Procesamiento.Cuentas;

/// <summary>
/// ConcreteElement (GoF): cuenta de inversión a plazo. Los débitos antes del
/// vencimiento pagan una penalización y los depósitos tienen un monto mínimo.
/// </summary>
public sealed class CuentaInversion : Cuenta
{
    public CuentaInversion(string numero, string titular, decimal saldoInicial, DateTime fechaVencimiento,
                           decimal tasaPenalizacion = 0.02m, decimal depositoMinimo = 100.00m)
        : base(numero, titular, saldoInicial)
    {
        FechaVencimiento = fechaVencimiento;
        TasaPenalizacion = tasaPenalizacion;
        DepositoMinimo = depositoMinimo;
    }

    /// <summary>Fecha a partir de la cual se puede retirar sin penalización.</summary>
    public DateTime FechaVencimiento { get; }

    /// <summary>Penalización sobre el monto de un débito anticipado (0.02 = 2 %).</summary>
    public decimal TasaPenalizacion { get; }

    /// <summary>Monto mínimo de cada crédito.</summary>
    public decimal DepositoMinimo { get; }

    /// <summary>Total de penalizaciones cobradas.</summary>
    public decimal PenalizacionesCobradas { get; internal set; }

    /// <inheritdoc />
    public override void Aceptar(IVisitanteCuenta visitante) => visitante.Visitar(this);
}
