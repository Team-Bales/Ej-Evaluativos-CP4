namespace Bolsa.Notificaciones.Entrega;

/// <summary>
/// Decorador que hace fallar a propósito los primeros <c>fallosIniciales</c>
/// envíos y después entrega con normalidad. Con un número pequeño simula un
/// proveedor que se cae un momento y se recupera (la notificación llega en un
/// reintento); con <see cref="int.MaxValue"/> simula un cliente que no se puede
/// notificar nunca: un correo que ya no existe o un teléfono dado de baja.
/// </summary>
public sealed class CanalQueFalla : ICanalNotificacion
{
    private readonly object _bloqueo = new();
    private readonly ICanalNotificacion _interno;
    private int _fallosRestantes;
    private int _intentos;

    public CanalQueFalla(ICanalNotificacion interno, int fallosIniciales)
    {
        ArgumentNullException.ThrowIfNull(interno);
        ArgumentOutOfRangeException.ThrowIfNegative(fallosIniciales);

        _interno = interno;
        _fallosRestantes = fallosIniciales;
    }

    /// <summary>Veces que se intentó enviar por este canal, con éxito o sin él.</summary>
    public int Intentos => Volatile.Read(ref _intentos);

    public Task EnviarAsync(Notificacion notificacion)
    {
        ArgumentNullException.ThrowIfNull(notificacion);
        Interlocked.Increment(ref _intentos);

        lock (_bloqueo)
        {
            if (_fallosRestantes > 0)
            {
                _fallosRestantes--;
                throw new InvalidOperationException(
                    $"El proveedor rechazó el aviso de {notificacion.Cliente.Contacto}");
            }
        }

        return _interno.EnviarAsync(notificacion);
    }
}
