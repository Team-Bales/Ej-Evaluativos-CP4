using Banco.Procesamiento.Visitantes;

namespace Banco.Procesamiento.Cuentas;

/// <summary>
/// ConcreteElement (GoF): cuenta corriente. Puede quedar en negativo hasta un
/// tope de sobregiro; cada débito que la deja en negativo cobra un cargo fijo.
/// </summary>
public sealed class CuentaCorriente : Cuenta
{
    public CuentaCorriente(string numero, string titular, decimal saldoInicial,
                           decimal limiteSobregiro = 500.00m, decimal cargoPorSobregiro = 5.00m)
        : base(numero, titular, saldoInicial)
    {
        LimiteSobregiro = limiteSobregiro;
        CargoPorSobregiro = cargoPorSobregiro;
    }

    /// <summary>Hasta dónde puede bajar el saldo por debajo de cero.</summary>
    public decimal LimiteSobregiro { get; }

    /// <summary>Cargo fijo por cada débito que deja el saldo en negativo.</summary>
    public decimal CargoPorSobregiro { get; }

    /// <summary>Débitos que dejaron la cuenta en negativo (los que cobraron el cargo).</summary>
    public int VecesEnSobregiro { get; internal set; }

    /// <inheritdoc />
    public override void Aceptar(IVisitanteCuenta visitante) => visitante.Visitar(this);
}
