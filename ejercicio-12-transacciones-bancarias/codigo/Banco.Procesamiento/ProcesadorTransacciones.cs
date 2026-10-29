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
/// Concurrencia (restricción): antes de visitar, toma el candado de cada cuenta
/// de la transacción, siempre en el mismo orden (por número de cuenta), y no lo
/// suelta hasta terminar. Las transacciones sobre cuentas distintas corren en
/// paralelo; las que comparten una cuenta se ejecutan de una en una.
/// </summary>
public sealed class ProcesadorTransacciones
{
    /// <summary>
    /// Aplica la transacción en todas sus cuentas, o en ninguna. Un rechazo por
    /// una regla de negocio se devuelve como resultado; cualquier otro error
    /// también deshace lo aplicado y después se propaga.
    /// </summary>
    public ResultadoTransaccion Ejecutar(Transaccion transaccion) =>
        ConCandados(transaccion, () => EjecutarConCandados(transaccion));

    /// <summary>
    /// Ejecuta varias transacciones en paralelo (Parallel.For). Cada una toma
    /// los candados de sus cuentas, así que solo esperan las que comparten
    /// alguna cuenta. Los resultados vuelven en el mismo orden de la lista.
    /// </summary>
    public IReadOnlyList<ResultadoTransaccion> EjecutarEnParalelo(IReadOnlyList<Transaccion> transacciones)
    {
        ResultadoTransaccion[] resultados = new ResultadoTransaccion[transacciones.Count];
        Parallel.For(0, transacciones.Count, i => resultados[i] = Ejecutar(transacciones[i]));
        return resultados;
    }

    /// <summary>
    /// Aplica una operación a una cuenta o a todas las cuentas de un grupo (el
    /// cliente no distingue): crea una transacción por cuenta, en el orden del
    /// recorrido del árbol, y las ejecuta en paralelo, cada una con «todo o
    /// nada». La política del grupo es de mejor esfuerzo: si una cuenta rechaza
    /// su transacción, las demás siguen, y el reporte dice qué pasó con cada una.
    /// </summary>
    public IReadOnlyList<ResultadoTransaccion> EjecutarEnCadaCuenta(ICuenta destino, Func<Cuenta, Transaccion> crear) =>
        EjecutarEnParalelo(RecolectarCuentas.De(destino).Select(crear).ToList());

    /// <summary>Revierte una transacción aplicada en todas sus cuentas (rollback).</summary>
    public void Revertir(Transaccion transaccion) =>
        ConCandados(transaccion, () => Deshacer(transaccion, [.. transaccion.Cuentas]));

    /// <summary>Audita la transacción en cada una de sus cuentas, sin modificarlas.</summary>
    public IReadOnlyList<RegistroAuditoria> Auditar(Transaccion transaccion) =>
        ConCandados(transaccion, () =>
        {
            // Un AuditarTransaccion nuevo en cada llamada: su lista de registros
            // nunca se comparte entre hilos.
            AuditarTransaccion auditar = new(transaccion);
            foreach (ICuenta cuenta in transaccion.Cuentas)
            {
                cuenta.Aceptar(auditar);
            }

            return auditar.Registros;
        });

    /// <summary>
    /// Audita varias transacciones en paralelo: cada tarea usa su propio
    /// AuditarTransaccion y los registros se combinan al final, en orden.
    /// </summary>
    public IReadOnlyList<RegistroAuditoria> AuditarEnParalelo(IReadOnlyList<Transaccion> transacciones)
    {
        IReadOnlyList<RegistroAuditoria>[] porTarea = new IReadOnlyList<RegistroAuditoria>[transacciones.Count];
        Parallel.For(0, transacciones.Count, i => porTarea[i] = Auditar(transacciones[i]));
        return porTarea.SelectMany(registros => registros).ToList();
    }

    /// <summary>El «todo o nada» del inciso b; se llama con los candados ya tomados.</summary>
    private static ResultadoTransaccion EjecutarConCandados(Transaccion transaccion)
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
            DeshacerSinTaparLaCausa(transaccion, aplicadas, rechazo);
            return ResultadoTransaccion.Rechazada(transaccion, rechazo.Message);
        }
        catch (Exception error)
        {
            DeshacerSinTaparLaCausa(transaccion, aplicadas, error);
            throw;
        }
    }

    /// <summary>
    /// Deshace lo aplicado. Si deshacer también falla, no tapa el error que lo
    /// provocó: lanza una AggregateException con los dos.
    /// </summary>
    private static void DeshacerSinTaparLaCausa(Transaccion transaccion, List<ICuenta> aplicadas, Exception causa)
    {
        try
        {
            Deshacer(transaccion, aplicadas);
        }
        catch (Exception errorAlDeshacer)
        {
            throw new AggregateException(
                $"No se pudo deshacer «{transaccion.Descripcion}» después de un fallo.", causa, errorAlDeshacer);
        }
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

    /// <summary>
    /// Toma el candado de cada cuenta de la transacción, ordenadas por número,
    /// ejecuta la acción y suelta los candados en orden inverso. El orden fijo
    /// evita el interbloqueo: dos transferencias cruzadas (A→B y B→A) piden los
    /// dos candados en el mismo orden, así que una espera a la otra en vez de
    /// quedarse cada una con un candado esperando el de la otra.
    /// </summary>
    private static T ConCandados<T>(Transaccion transaccion, Func<T> accion)
    {
        Cuenta[] cuentas = [.. transaccion.Cuentas.OrderBy(c => c.Numero, StringComparer.Ordinal)];
        int tomados = 0;
        try
        {
            foreach (Cuenta cuenta in cuentas)
            {
                bool tomado = false;
                Monitor.Enter(cuenta.Candado, ref tomado);
                if (tomado)
                {
                    tomados++;
                }
            }

            return accion();
        }
        finally
        {
            for (int i = tomados - 1; i >= 0; i--)
            {
                Monitor.Exit(cuentas[i].Candado);
            }
        }
    }

    private static void ConCandados(Transaccion transaccion, Action accion) =>
        ConCandados(transaccion, () =>
        {
            accion();
            return true;
        });
}
