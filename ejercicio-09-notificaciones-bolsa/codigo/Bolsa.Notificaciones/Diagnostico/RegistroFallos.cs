namespace Bolsa.Notificaciones.Diagnostico;

/// <summary>
/// Registro de fallos que escribe en una salida de texto y lleva la cuenta de
/// cuántos hubo. Por omisión escribe en la consola, para que un fallo aislado
/// se vea siempre; el benchmark del inciso d) le pasa
/// <see cref="TextWriter.Null"/> porque allí el ruido de la consola falsearía
/// la medición.
/// </summary>
public sealed class RegistroFallos : IRegistroFallos
{
    private readonly TextWriter _salida;
    private int _total;

    public RegistroFallos(TextWriter? salida = null)
    {
        _salida = salida ?? Console.Out;
    }

    /// <summary>Cantidad de fallos registrados.</summary>
    public int Total => Volatile.Read(ref _total);

    /// <inheritdoc />
    public void Registrar(string origen, string detalle, Exception? error = null)
    {
        Interlocked.Increment(ref _total);

        string causa = error is null ? string.Empty : $" ({error.GetType().Name}: {error.Message})";
        _salida.WriteLine($"[FALLO {origen}] {detalle}{causa}");
    }
}
