namespace Bolsa.Notificaciones.Entrega;

/// <summary>
/// Reconstruye el canal de un aviso cuando se recupera del outbox: el archivo
/// guarda el <em>tipo</em> del canal (por ejemplo <c>CanalEmail</c>), no el
/// objeto, porque un canal no se serializa.
/// </summary>
public interface IFabricaCanales
{
    /// <summary>
    /// Crea el canal indicado. Si el tipo no se conoce, devuelve <c>null</c>: el
    /// aviso no se pierde, queda en el outbox a la espera de que un operador lo
    /// resuelva.
    /// </summary>
    ICanalNotificacion? Crear(string tipoCanal);
}

/// <summary>
/// Fábrica de los canales reales del sistema: correo, SMS y push. Es la que
/// usan las demos y la que reconstruye los avisos al reiniciar.
/// </summary>
public sealed class FabricaCanales : IFabricaCanales
{
    /// <inheritdoc />
    public ICanalNotificacion? Crear(string tipoCanal) => tipoCanal switch
    {
        nameof(CanalEmail) => new CanalEmail(),
        nameof(CanalSms) => new CanalSms(),
        nameof(CanalPush) => new CanalPush(),
        _ => null,
    };
}
