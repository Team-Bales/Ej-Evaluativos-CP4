using System.Globalization;

namespace Banco.Procesamiento;

/// <summary>Redondeo y formato de importes, iguales en todo el banco.</summary>
internal static class Dinero
{
    /// <summary>Redondea a centavos; los medios centavos se redondean hacia arriba.</summary>
    internal static decimal Redondear(decimal monto) => Math.Round(monto, 2, MidpointRounding.AwayFromZero);

    /// <summary>Formato fijo, independiente de la cultura del equipo (p. ej. 1250.00).</summary>
    internal static string Formato(decimal monto) => monto.ToString("0.00", CultureInfo.InvariantCulture);
}
