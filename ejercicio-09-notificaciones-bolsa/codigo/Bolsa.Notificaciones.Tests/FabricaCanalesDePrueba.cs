using Bolsa.Notificaciones.Entrega;

namespace Bolsa.Notificaciones.Tests;

/// <summary>
/// Fábrica de canales para las pruebas: en vez de imprimir el aviso, lo guarda
/// para poder comprobar que llegó. Es el mismo punto de extensión que usa la
/// aplicación real (<see cref="FabricaCanales"/>), pero sin ruido en la salida.
/// </summary>
internal sealed class FabricaCanalesDePrueba : IFabricaCanales
{
    private readonly List<CanalDePrueba> _creados = [];

    /// <summary>Los canales que se han reconstruido, en orden.</summary>
    public IReadOnlyList<CanalDePrueba> Creados => _creados;

    public ICanalNotificacion? Crear(string tipoCanal)
    {
        if (tipoCanal != nameof(CanalEmail))
        {
            return null;
        }

        var canal = new CanalDePrueba();
        _creados.Add(canal);
        return canal;
    }
}
