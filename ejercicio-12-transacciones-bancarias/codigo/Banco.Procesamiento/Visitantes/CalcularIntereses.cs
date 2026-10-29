using Banco.Procesamiento.Cuentas;

namespace Banco.Procesamiento.Visitantes;

/// <summary>
/// ConcreteVisitor agregado para demostrar el requisito de extensibilidad:
/// «calcular intereses» es una operación nueva y para agregarla solo hizo
/// falta esta clase. Ninguna cuenta ni transacción se modificó.
/// Calcula el interés de un mes con la regla de cada tipo de cuenta y lo
/// acumula; no cambia saldos (abonarlo sería un Deposito más).
/// </summary>
public sealed class CalcularIntereses : IVisitanteCuenta
{
    private const decimal TasaAhorro = 0.03m;        // anual, sobre el saldo
    private const decimal TasaSobregiro = 0.18m;     // anual, se cobra sobre el sobregiro
    private const decimal TasaInversion = 0.06m;     // anual, sobre el saldo
    private const decimal TasaEmpresarial = 0.01m;   // anual, solo sobre lo que pase del exento
    private const decimal ExentoEmpresarial = 20_000.00m;

    private readonly Dictionary<string, decimal> _intereses = [];

    /// <summary>Interés del mes por número de cuenta; negativo si se cobra.</summary>
    public IReadOnlyDictionary<string, decimal> Intereses => _intereses;

    /// <summary>Suma de los intereses calculados.</summary>
    public decimal Total => _intereses.Values.Sum();

    /// <summary>Ahorro: 3 % anual sobre el saldo; un saldo negativo (por una reversión) no genera interés.</summary>
    public void Visitar(CuentaAhorro cuenta) => Anotar(cuenta, Mensual(Math.Max(cuenta.Saldo, 0m), TasaAhorro));

    /// <summary>Corriente: no paga intereses; si está en sobregiro, cobra el 18 % anual sobre lo que debe.</summary>
    public void Visitar(CuentaCorriente cuenta) =>
        Anotar(cuenta, cuenta.Saldo < 0m ? -Mensual(-cuenta.Saldo, TasaSobregiro) : 0m);

    /// <summary>Inversión: 6 % anual sobre el saldo; un saldo negativo (por una reversión) no genera interés.</summary>
    public void Visitar(CuentaInversion cuenta) => Anotar(cuenta, Mensual(Math.Max(cuenta.Saldo, 0m), TasaInversion));

    /// <summary>Empresarial: 1 % anual, solo sobre el saldo que pase del exento.</summary>
    public void Visitar(CuentaEmpresarial cuenta) =>
        Anotar(cuenta, Mensual(Math.Max(cuenta.Saldo - ExentoEmpresarial, 0m), TasaEmpresarial));

    private static decimal Mensual(decimal saldoBase, decimal tasaAnual) => Dinero.Redondear(saldoBase * tasaAnual / 12m);

    private void Anotar(Cuenta cuenta, decimal interes) => _intereses[cuenta.Numero] = interes;
}
