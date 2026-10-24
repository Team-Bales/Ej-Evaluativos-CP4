namespace Banco.Procesamiento.Visitantes;

/// <summary>
/// Lo que AuditarTransaccion anota por cada cuenta: quién pidió la
/// transacción, qué se pidió, cuándo, el resultado en esa cuenta y si lo que
/// le hizo cumple las reglas de su tipo.
/// </summary>
/// <param name="TransaccionId">Transacción auditada.</param>
/// <param name="Cuenta">Número de la cuenta auditada.</param>
/// <param name="Usuario">Quién pidió la transacción.</param>
/// <param name="Descripcion">Qué se pidió.</param>
/// <param name="Fecha">Cuándo se pidió.</param>
/// <param name="Estado">Resultado en esta cuenta: aplicada, revertida o no aplicada.</param>
/// <param name="Conforme">
/// True si lo que la transacción le hizo a la cuenta cumple las reglas de su tipo: el cargo
/// cobrado, la autorización y el saldo en que la dejó (o en que la dejó su reversión).
/// </param>
/// <param name="Observacion">Detalle de la regla revisada (cargos, autorización…) y el saldo en que quedó la cuenta.</param>
public sealed record RegistroAuditoria(
    Guid TransaccionId,
    string Cuenta,
    string Usuario,
    string Descripcion,
    DateTime Fecha,
    EstadoTransaccion Estado,
    bool Conforme,
    string Observacion);
