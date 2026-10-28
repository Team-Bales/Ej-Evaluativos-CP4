using Banco.Procesamiento.Transacciones;
using Banco.Procesamiento.Visitantes;

namespace Banco.Procesamiento.Cuentas;

/// <summary>
/// Base común de las cuatro cuentas concretas: número, titular, saldo y el
/// historial de asientos que dejan las transacciones. No tiene reglas de
/// negocio; esas están en los visitantes, que tratan a cada tipo de cuenta a
/// su manera.
/// </summary>
public abstract class Cuenta : ICuenta
{
    private readonly List<Asiento> _asientos = [];

    protected Cuenta(string numero, string titular, decimal saldoInicial)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(saldoInicial);
        Numero = numero;
        Titular = titular;
        Saldo = saldoInicial;
    }

    /// <summary>Número de cuenta (p. ej. AHO-001); la identifica.</summary>
    public string Numero { get; }

    /// <summary>Nombre del titular.</summary>
    public string Titular { get; }

    /// <summary>Saldo actual. Solo lo cambian los visitantes, a través de AjustarSaldo.</summary>
    public decimal Saldo { get; private set; }

    /// <inheritdoc />
    public abstract void Aceptar(IVisitanteCuenta visitante);

    /// <summary>
    /// Iterator (GoF): recorre las transacciones de la cuenta (sus asientos,
    /// también los revertidos) en el orden en que se aplicaron, sin exponer la
    /// lista interna: quien lo usa no puede agregar, quitar ni reordenar asientos.
    /// Si la cuenta cambia mientras se recorre, el recorrido falla en vez de
    /// devolver datos a medias.
    /// </summary>
    public IEnumerable<Asiento> Historial()
    {
        foreach (Asiento asiento in _asientos)
        {
            yield return asiento;
        }
    }

    /// <summary>Iterator filtrado: los asientos de las transacciones con fecha en [desde, hasta).</summary>
    public IEnumerable<Asiento> Historial(DateTime desde, DateTime hasta) =>
        Historial().Where(a => a.Transaccion.Fecha >= desde && a.Transaccion.Fecha < hasta);

    /// <summary>Iterator filtrado: los asientos de un tipo de transacción (p. ej. solo las transferencias).</summary>
    public IEnumerable<Asiento> Historial<TTransaccion>() where TTransaccion : Transaccion =>
        Historial().Where(a => a.Transaccion is TTransaccion);

    // Operaciones que usan los visitantes para cambiar el estado. Son internal:
    // fuera del ensamblado del banco solo se puede leer la cuenta. Es la forma
    // de acotar la desventaja de Visitor de exponer el estado de los elementos
    // (GoF, «Breaking encapsulation»).

    /// <summary>Suma (o resta, si es negativo) un importe al saldo.</summary>
    internal void AjustarSaldo(decimal efecto) => Saldo += efecto;

    /// <summary>Guarda en el historial lo que una transacción le hizo a la cuenta.</summary>
    internal void Registrar(Asiento asiento) => _asientos.Add(asiento);

    /// <summary>El asiento que dejó una transacción en esta cuenta, o null si no la aplicó.</summary>
    internal Asiento? AsientoDe(Guid transaccionId) => _asientos.Find(a => a.Transaccion.Id == transaccionId);
}
