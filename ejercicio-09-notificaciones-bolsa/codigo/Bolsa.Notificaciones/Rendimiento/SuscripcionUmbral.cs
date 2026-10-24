using Bolsa.Notificaciones.Entrega;
using Bolsa.Notificaciones.Observer;

namespace Bolsa.Notificaciones.Rendimiento;

/// <summary>
/// Una suscripción vista como dato, no como objeto observador: cliente, umbral
/// y dirección. Es lo que el <see cref="GestorUmbralesIndexado"/> indexa, y la
/// diferencia de fondo con el inciso b): allí cada suscripción era un objeto
/// <see cref="AlertaUmbral"/> en la lista del sujeto; aquí son filas de un
/// índice ordenado.
/// </summary>
public sealed record SuscripcionUmbral(Cliente Cliente, decimal Umbral, Direccion Direccion);
