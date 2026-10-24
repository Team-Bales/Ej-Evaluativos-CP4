using Bolsa.Notificaciones.Diagnostico;

namespace Bolsa.Notificaciones.Tests;

/// <summary>Registro de prueba: guarda los fallos en lugar de imprimirlos.</summary>
internal sealed class RegistroDePrueba : IRegistroFallos
{
    private readonly object _bloqueo = new();
    private readonly List<string> _fallos = [];

    public IReadOnlyList<string> Fallos
    {
        get
        {
            lock (_bloqueo)
            {
                return _fallos.ToArray();
            }
        }
    }

    public void Registrar(string origen, string detalle, Exception? error = null)
    {
        lock (_bloqueo)
        {
            _fallos.Add($"{origen}: {detalle}");
        }
    }
}
