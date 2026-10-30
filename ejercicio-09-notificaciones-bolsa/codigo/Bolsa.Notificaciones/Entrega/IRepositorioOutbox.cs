namespace Bolsa.Notificaciones.Entrega;

/// <summary>
/// Capacidad opcional del repositorio que necesita la restricción E9-R: guardar
/// la notificación <em>antes</em> de enviarla (patrón <i>outbox</i>) y seguir
/// el estado del envío.
///
/// <para>
/// El despachador de los incisos b y c sigue hablando solo con
/// <see cref="IRepositorioNotificaciones"/>: si el repositorio que recibe no
/// implementa esta interfaz, el comportamiento es el de siempre. Por eso el
/// inciso c no cambia al pasar a la versión persistente.
/// </para>
/// </summary>
public interface IRepositorioOutbox
{
    /// <summary>
    /// Guarda la notificación como pendiente. Se llama <em>antes</em> de encolar
    /// el envío: si el proceso se cae después, el aviso sigue en el disco.
    /// Repetirla con el mismo <see cref="Notificacion.Id"/> no duplica la fila.
    /// </summary>
    void RegistrarPendiente(Notificacion notificacion, int intentos);

    /// <summary>Marca la notificación como entregada.</summary>
    void MarcarEntregada(Guid id);

    /// <summary>Anota un intento fallido y conserva el contador.</summary>
    void MarcarReintento(Guid id, int intentos, string error);

    /// <summary>Anota que se agotaron los intentos (dead letter).</summary>
    void MarcarFalloDefinitivo(Guid id, int intentos, string error);

    /// <summary>
    /// Los avisos pendientes de envío, con el canal reconstruido y el contador
    /// de intentos que tenían guardado. Es lo que se reencola al reiniciar.
    /// </summary>
    IReadOnlyList<EnvioPendiente> PendientesParaReintentar();

    /// <summary>Todos los registros del outbox, en cualquier estado.</summary>
    IReadOnlyList<RegistroOutbox> Todos();
}
