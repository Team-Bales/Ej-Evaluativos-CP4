using Banco.Procesamiento.Visitantes;

namespace Banco.Procesamiento.Cuentas;

/// <summary>
/// ConcreteElement (GoF): cuenta de una empresa. Cada débito paga una comisión
/// y los débitos de monto alto necesitan una autorización.
/// </summary>
public sealed class CuentaEmpresarial : Cuenta
{
    public CuentaEmpresarial(string numero, string titular, decimal saldoInicial,
                             decimal tasaComision = 0.005m, decimal umbralAutorizacion = 10_000.00m)
        : base(numero, titular, saldoInicial)
    {
        TasaComision = tasaComision;
        UmbralAutorizacion = umbralAutorizacion;
    }

    /// <summary>Comisión sobre el monto de cada débito (0.005 = 0,5 %).</summary>
    public decimal TasaComision { get; }

    /// <summary>Los débitos por encima de este monto necesitan autorización.</summary>
    public decimal UmbralAutorizacion { get; }

    /// <summary>Total de comisiones cobradas.</summary>
    public decimal ComisionesCobradas { get; internal set; }

    /// <summary>Débitos de monto alto aplicados con autorización.</summary>
    public int OperacionesAutorizadas { get; internal set; }

    /// <inheritdoc />
    public override void Aceptar(IVisitanteCuenta visitante) => visitante.Visitar(this);
}
