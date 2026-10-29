using Banco.Procesamiento.Cuentas;
using Banco.Procesamiento.Transacciones;
using Banco.Procesamiento.Visitantes;

namespace Banco.Procesamiento;

/// <summary>
/// Client (GoF): crea el visitante de cada operación y lo pasa por las cuentas
/// de la transacción, llamando a Aceptar en cada una. Para pasar el visitante
/// solo usa ICuenta: no sabe qué tipos de cuenta hay ni cuáles son sus reglas.
/// Garantiza «todo o nada»: si una cuenta rechaza su movimiento, revierte con
/// RevertirTransaccion las que ya lo habían aplicado (en una transferencia, el
/// débito ya hecho cuando falla el crédito).
/// </summary>
public sealed class ProcesadorTransacciones
{
    /// <summary>
    /// Aplica la transacción en todas sus cuentas, o en ninguna. Un rechazo por
    /// una regla de negocio se devuelve como resultado; cualquier otro error
    /// también deshace lo aplicado y después se propaga.
    /// </summary>
    public ResultadoTransaccion Ejecutar(Transaccion transaccion)
    {
        EjecutarTransaccion ejecutar = new(transaccion);
        List<ICuenta> aplicadas = [];
        try
        {
            foreach (ICuenta cuenta in transaccion.Cuentas)
            {
                cuenta.Aceptar(ejecutar);
                aplicadas.Add(cuenta);
            }

            return ResultadoTransaccion.Aplicada(transaccion);
        }
        catch (OperacionRechazadaException rechazo)
        {
            Deshacer(transaccion, aplicadas);
            return ResultadoTransaccion.Rechazada(transaccion, rechazo.Message);
        }
        catch
        {
            Deshacer(transaccion, aplicadas);
            throw;
        }
    }

    /// <summary>
    /// Aplica una operación a una cuenta o a todas las cuentas de un grupo (el
    /// cliente no distingue): crea una transacción por cuenta, en el orden del
    /// recorrido del árbol, y ejecuta cada una con «todo o nada». La política
    /// del grupo es de mejor esfuerzo: si una cuenta rechaza su transacción, las
    /// demás siguen, y el reporte dice qué pasó con cada una.
    /// </summary>
    public IReadOnlyList<ResultadoTransaccion> EjecutarEnCadaCuenta(ICuenta destino, Func<Cuenta, Transaccion> crear) =>
        RecolectarCuentas.De(destino).Select(cuenta => Ejecutar(crear(cuenta))).ToList();

    /// <summary>Revierte una transacción aplicada en todas sus cuentas (rollback).</summary>
    public void Revertir(Transaccion transaccion) => Deshacer(transaccion, [.. transaccion.Cuentas]);

    /// <summary>Audita la transacción en cada una de sus cuentas, sin modificarlas.</summary>
    public IReadOnlyList<RegistroAuditoria> Auditar(Transaccion transaccion)
    {
        AuditarTransaccion auditar = new(transaccion);
        foreach (ICuenta cuenta in transaccion.Cuentas)
        {
            cuenta.Aceptar(auditar);
        }

        return auditar.Registros;
    }

    /// <summary>Revierte en orden inverso al de aplicación.</summary>
    private static void Deshacer(Transaccion transaccion, List<ICuenta> cuentas)
    {
        RevertirTransaccion revertir = new(transaccion);
        for (int i = cuentas.Count - 1; i >= 0; i--)
        {
            cuentas[i].Aceptar(revertir);
        }
    }
}
