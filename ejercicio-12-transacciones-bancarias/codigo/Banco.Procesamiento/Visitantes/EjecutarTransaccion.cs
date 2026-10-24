using Banco.Procesamiento.Cuentas;
using Banco.Procesamiento.Transacciones;

namespace Banco.Procesamiento.Visitantes;

/// <summary>
/// ConcreteVisitor (GoF): aplica a cada cuenta su movimiento de la transacción
/// con las reglas de negocio de su tipo, y deja en la cuenta un Asiento con el
/// efecto exacto (monto y cargo) para revertirla y auditarla después. Si una
/// regla lo impide, lanza OperacionRechazadaException sin tocar la cuenta.
/// Solo guarda la transacción, que no cambia: no tiene estado mutable propio.
/// </summary>
public sealed class EjecutarTransaccion : IVisitanteCuenta
{
    public EjecutarTransaccion(Transaccion transaccion)
    {
        Transaccion = transaccion;
    }

    /// <summary>La transacción que se aplica.</summary>
    public Transaccion Transaccion { get; }

    /// <summary>
    /// Ahorro: todo débito cuenta como retiro del mes de la transacción y se
    /// rechaza si ese mes ya alcanzó el límite; un mes nuevo empieza de cero.
    /// No hay sobregiro: el débito se rechaza si dejaría el saldo en negativo.
    /// </summary>
    public void Visitar(CuentaAhorro cuenta)
    {
        Movimiento movimiento = MovimientoPendiente(cuenta);
        if (movimiento.Naturaleza == Naturaleza.Credito)
        {
            Aplicar(cuenta, movimiento, cargo: 0m);
            return;
        }

        DateOnly mes = CuentaAhorro.MesDe(Transaccion.Fecha);
        int retirosDelMes = cuenta.MesDeRetiros == mes ? cuenta.RetirosDelMes : 0;
        if (retirosDelMes >= cuenta.LimiteRetirosMensuales)
        {
            throw new OperacionRechazadaException(cuenta,
                $"alcanzó el límite de {cuenta.LimiteRetirosMensuales} retiros del mes");
        }

        ExigirSaldo(cuenta, movimiento.Monto, saldoMinimo: 0m, "saldo insuficiente");
        Aplicar(cuenta, movimiento, cargo: 0m);
        cuenta.MesDeRetiros = mes;
        cuenta.RetirosDelMes = retirosDelMes + 1;
    }

    /// <summary>
    /// Corriente: permite quedar en negativo hasta el tope de sobregiro. Si el
    /// débito deja el saldo en negativo, cobra el cargo por sobregiro, y el
    /// tope se comprueba con el cargo incluido.
    /// </summary>
    public void Visitar(CuentaCorriente cuenta)
    {
        Movimiento movimiento = MovimientoPendiente(cuenta);
        if (movimiento.Naturaleza == Naturaleza.Credito)
        {
            Aplicar(cuenta, movimiento, cargo: 0m);
            return;
        }

        bool quedaEnNegativo = cuenta.Saldo - movimiento.Monto < 0m;
        decimal cargo = quedaEnNegativo ? cuenta.CargoPorSobregiro : 0m;
        ExigirSaldo(cuenta, movimiento.Monto + cargo, saldoMinimo: -cuenta.LimiteSobregiro,
                    $"supera el sobregiro permitido de {Dinero.Formato(cuenta.LimiteSobregiro)}");
        Aplicar(cuenta, movimiento, cargo);
        if (quedaEnNegativo)
        {
            cuenta.VecesEnSobregiro++;
        }
    }

    /// <summary>
    /// Inversión: los créditos deben llegar al depósito mínimo. Un débito antes
    /// de la fecha de vencimiento paga la penalización por retiro anticipado.
    /// </summary>
    public void Visitar(CuentaInversion cuenta)
    {
        Movimiento movimiento = MovimientoPendiente(cuenta);
        if (movimiento.Naturaleza == Naturaleza.Credito)
        {
            if (movimiento.Monto < cuenta.DepositoMinimo)
            {
                throw new OperacionRechazadaException(cuenta,
                    $"el depósito mínimo es {Dinero.Formato(cuenta.DepositoMinimo)}");
            }

            Aplicar(cuenta, movimiento, cargo: 0m);
            return;
        }

        decimal penalizacion = Transaccion.Fecha < cuenta.FechaVencimiento
            ? Dinero.Redondear(movimiento.Monto * cuenta.TasaPenalizacion)
            : 0m;
        ExigirSaldo(cuenta, movimiento.Monto + penalizacion, saldoMinimo: 0m, "saldo insuficiente");
        Aplicar(cuenta, movimiento, penalizacion);
        cuenta.PenalizacionesCobradas += penalizacion;
    }

    /// <summary>
    /// Empresarial: los débitos por encima del umbral necesitan que la
    /// transacción venga autorizada por alguien distinto de quien la pide.
    /// Todo débito paga la comisión.
    /// </summary>
    public void Visitar(CuentaEmpresarial cuenta)
    {
        Movimiento movimiento = MovimientoPendiente(cuenta);
        if (movimiento.Naturaleza == Naturaleza.Credito)
        {
            Aplicar(cuenta, movimiento, cargo: 0m);
            return;
        }

        bool requiereAutorizacion = movimiento.Monto > cuenta.UmbralAutorizacion;
        if (requiereAutorizacion && !TieneAutorizacionValida(Transaccion))
        {
            throw new OperacionRechazadaException(cuenta,
                $"los débitos de más de {Dinero.Formato(cuenta.UmbralAutorizacion)} necesitan la autorización de otra persona");
        }

        decimal comision = Dinero.Redondear(movimiento.Monto * cuenta.TasaComision);
        ExigirSaldo(cuenta, movimiento.Monto + comision, saldoMinimo: 0m, "saldo insuficiente");
        Aplicar(cuenta, movimiento, comision);
        cuenta.ComisionesCobradas += comision;
        if (requiereAutorizacion)
        {
            cuenta.OperacionesAutorizadas++;
        }
    }

    /// <summary>La autorización la da alguien distinto de quien pide la transacción.</summary>
    internal static bool TieneAutorizacionValida(Transaccion transaccion) =>
        !string.IsNullOrWhiteSpace(transaccion.AutorizadaPor) && transaccion.AutorizadaPor != transaccion.Usuario;

    /// <summary>El movimiento de la transacción sobre la cuenta; falla si ya se ejecutó en ella.</summary>
    private Movimiento MovimientoPendiente(Cuenta cuenta)
    {
        Asiento? asiento = cuenta.AsientoDe(Transaccion.Id);
        if (asiento != null)
        {
            throw new InvalidOperationException(asiento.Revertido
                ? $"La transacción ya se ejecutó y se revirtió en {cuenta.Numero}; para repetirla hay que crear una nueva."
                : $"La transacción ya se aplicó en {cuenta.Numero}.");
        }

        return Transaccion.MovimientoDe(cuenta);
    }

    /// <summary>Cambia el saldo y deja el asiento con el efecto exacto (monto y cargo).</summary>
    private void Aplicar(Cuenta cuenta, Movimiento movimiento, decimal cargo)
    {
        Asiento asiento = new(Transaccion, movimiento.Naturaleza, movimiento.Monto, cargo, saldoAnterior: cuenta.Saldo);
        cuenta.AjustarSaldo(asiento.Efecto);
        cuenta.Registrar(asiento);
    }

    private static void ExigirSaldo(Cuenta cuenta, decimal debito, decimal saldoMinimo, string motivo)
    {
        if (cuenta.Saldo - debito < saldoMinimo)
        {
            throw new OperacionRechazadaException(cuenta,
                $"{motivo} (saldo {Dinero.Formato(cuenta.Saldo)}, a debitar {Dinero.Formato(debito)})");
        }
    }
}
