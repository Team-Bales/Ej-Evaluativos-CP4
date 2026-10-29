using Banco.Procesamiento.Visitantes;

namespace Banco.Procesamiento.Cuentas;

/// <summary>
/// ConcreteElement (GoF): cuenta de ahorro. Admite un número limitado de
/// débitos al mes y no admite sobregiro: ejecutar una transacción nunca la deja
/// en negativo.
/// </summary>
public sealed class CuentaAhorro : Cuenta
{
    public CuentaAhorro(string numero, string titular, decimal saldoInicial, int limiteRetirosMensuales = 3)
        : base(numero, titular, saldoInicial)
    {
        LimiteRetirosMensuales = limiteRetirosMensuales;
    }

    /// <summary>Débitos permitidos en un mes (retiros, transferencias salientes y pagos).</summary>
    public int LimiteRetirosMensuales { get; }

    /// <summary>
    /// Débitos aplicados en MesDeRetiros. Un débito de un mes nuevo empieza la
    /// cuenta de cero. Simplificación: solo se lleva el conteo del mes más
    /// reciente, así que se supone que las transacciones llegan en orden de fecha.
    /// </summary>
    public int RetirosDelMes { get; internal set; }

    /// <summary>Mes al que corresponde RetirosDelMes (su primer día).</summary>
    public DateOnly MesDeRetiros { get; internal set; }

    /// <inheritdoc />
    public override void Aceptar(IVisitanteCuenta visitante) => visitante.Visitar(this);

    /// <summary>El mes de una fecha, representado por su primer día.</summary>
    internal static DateOnly MesDe(DateTime fecha) => new(fecha.Year, fecha.Month, 1);
}
