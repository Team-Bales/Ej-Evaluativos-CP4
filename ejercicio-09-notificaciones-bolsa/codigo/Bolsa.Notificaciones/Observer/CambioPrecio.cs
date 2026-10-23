namespace Bolsa.Notificaciones.Observer;

/// <summary>
/// El dato que viaja del sujeto al observador (modelo push): la acción lo crea
/// en cada cambio y el observador recibe todo lo que necesita para decidir si
/// se cruzó su umbral, sin tener que volver a consultar a la acción.
/// </summary>
public sealed record CambioPrecio(
    string Simbolo,
    decimal PrecioAnterior,
    decimal PrecioNuevo,
    DateTimeOffset Instante);
