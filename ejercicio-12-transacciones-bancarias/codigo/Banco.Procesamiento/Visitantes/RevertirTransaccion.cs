using Banco.Procesamiento.Cuentas;
using Banco.Procesamiento.Transacciones;

namespace Banco.Procesamiento.Visitantes;

/// <summary>
/// ConcreteVisitor (GoF): deshace en cada cuenta el efecto exacto que dejó
/// EjecutarTransaccion, leyendo su Asiento: resta del saldo ese efecto (en un
/// débito devuelve el monto y el cargo; en un crédito retira el monto) y
/// descuenta los contadores propios de cada tipo de cuenta. No restaura una
/// foto del estado anterior, así no borra lo que otras transacciones hicieron
/// después en la misma cuenta.
/// Es una compensación obligatoria: no aplica las reglas de negocio (un
/// depósito ya gastado se revierte igual) y AuditarTransaccion lo señala.
/// </summary>
public sealed class RevertirTransaccion : IVisitanteCuenta
{
    public RevertirTransaccion(Transaccion transaccion)
    {
        Transaccion = transaccion;
    }

    /// <summary>La transacción que se revierte.</summary>
    public Transaccion Transaccion { get; }

    /// <summary>
    /// Ahorro: si era un débito del mes en curso, devuelve el retiro que
    /// consumió. Si era de un mes anterior, ese mes ya no cuenta.
    /// </summary>
    public void Visitar(CuentaAhorro cuenta)
    {
        Asiento asiento = Deshacer(cuenta);
        bool debitoDelMesEnCurso = asiento.Naturaleza == Naturaleza.Debito
                                   && CuentaAhorro.MesDe(asiento.Transaccion.Fecha) == cuenta.MesDeRetiros;
        if (debitoDelMesEnCurso)
        {
            cuenta.RetirosDelMes--;
        }
    }

    /// <summary>Corriente: devuelve el cargo por sobregiro, si lo hubo, y descuenta ese sobregiro.</summary>
    public void Visitar(CuentaCorriente cuenta)
    {
        Asiento asiento = Deshacer(cuenta);
        if (asiento.Naturaleza == Naturaleza.Debito && asiento.SaldoResultante < 0m)
        {
            cuenta.VecesEnSobregiro--;
        }
    }

    /// <summary>Inversión: devuelve la penalización cobrada y la descuenta del total.</summary>
    public void Visitar(CuentaInversion cuenta)
    {
        Asiento asiento = Deshacer(cuenta);
        cuenta.PenalizacionesCobradas -= asiento.Cargo;
    }

    /// <summary>
    /// Empresarial: devuelve la comisión y, si era un débito de monto alto,
    /// descuenta la operación autorizada.
    /// </summary>
    public void Visitar(CuentaEmpresarial cuenta)
    {
        Asiento asiento = Deshacer(cuenta);
        cuenta.ComisionesCobradas -= asiento.Cargo;
        if (asiento.Naturaleza == Naturaleza.Debito && asiento.Monto > cuenta.UmbralAutorizacion)
        {
            cuenta.OperacionesAutorizadas--;
        }
    }

    /// <summary>
    /// Parte común: busca el asiento de la transacción, resta su efecto del
    /// saldo y lo marca como revertido. Falla, sin tocar la cuenta, si la
    /// transacción no se aplicó en ella o ya se revirtió.
    /// </summary>
    private Asiento Deshacer(Cuenta cuenta)
    {
        Asiento asiento = cuenta.AsientoDe(Transaccion.Id)
            ?? throw new InvalidOperationException($"La transacción no se aplicó en {cuenta.Numero}.");
        if (asiento.Revertido)
        {
            throw new InvalidOperationException($"La transacción ya se revirtió en {cuenta.Numero}.");
        }

        cuenta.AjustarSaldo(-asiento.Efecto);
        asiento.MarcarRevertido(cuenta.Saldo);
        return asiento;
    }
}
