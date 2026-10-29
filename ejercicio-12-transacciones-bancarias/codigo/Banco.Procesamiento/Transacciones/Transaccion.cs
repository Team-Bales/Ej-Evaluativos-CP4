using Banco.Procesamiento.Cuentas;

namespace Banco.Procesamiento.Transacciones;

/// <summary>
/// Una petición del cliente encapsulada como objeto, al estilo Command (GoF:
/// «una transacción encapsula un conjunto de cambios sobre los datos»). Se
/// puede guardar, encolar, auditar y revertir porque es un objeto con su
/// identidad, quién la pidió, cuándo y qué movimientos implica.
/// A diferencia de un Command completo, no tiene Ejecutar ni Deshacer: cómo se
/// aplica depende del tipo de cuenta, y eso lo deciden los visitantes. Así una
/// operación nueva no obliga a modificar las transacciones.
/// </summary>
public abstract class Transaccion
{
    protected Transaccion(string usuario, DateTime fecha, string? autorizadaPor, IReadOnlyList<Movimiento> movimientos)
    {
        if (movimientos.Count == 0 || movimientos.Select(m => m.Cuenta).Distinct().Count() != movimientos.Count)
        {
            throw new ArgumentException("Una transacción tiene al menos un movimiento y como mucho uno por cuenta.",
                                        nameof(movimientos));
        }

        Usuario = usuario;
        Fecha = fecha;
        AutorizadaPor = autorizadaPor;
        Movimientos = movimientos;
    }

    /// <summary>Identifica la transacción en los asientos de las cuentas.</summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>Quién la pidió (cajero, app, banca web…).</summary>
    public string Usuario { get; }

    /// <summary>Cuándo se pidió.</summary>
    public DateTime Fecha { get; }

    /// <summary>Quién autorizó la operación, si hizo falta (débitos altos de una cuenta empresarial).</summary>
    public string? AutorizadaPor { get; }

    /// <summary>Débitos y créditos que implica, uno por cuenta.</summary>
    public IReadOnlyList<Movimiento> Movimientos { get; }

    /// <summary>Cuentas afectadas, en el orden en que se aplican.</summary>
    public IEnumerable<Cuenta> Cuentas => Movimientos.Select(m => m.Cuenta);

    /// <summary>Qué se pidió, en palabras (p. ej. «Retiro de 100.00 de AHO-001»).</summary>
    public abstract string Descripcion { get; }

    /// <summary>El movimiento de esta transacción sobre la cuenta indicada.</summary>
    public Movimiento MovimientoDe(Cuenta cuenta) =>
        Movimientos.FirstOrDefault(m => m.Cuenta == cuenta)
        ?? throw new InvalidOperationException($"La transacción no tiene movimientos en la cuenta {cuenta.Numero}.");
}
