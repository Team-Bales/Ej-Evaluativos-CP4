using System.Globalization;
using Banco.Procesamiento;
using Banco.Procesamiento.Cuentas;
using Banco.Procesamiento.Transacciones;
using Banco.Procesamiento.Visitantes;

// ===========================================================================
// Ejercicio 12, inciso b): 4 tipos de cuenta y 3 operaciones (visitantes).
// Cada transacción se ejecuta, se revierte o se audita pasando un visitante
// por sus cuentas; cada Visitar aplica la regla de su tipo de cuenta.
// Las fechas son fijas: la salida es la misma en cada ejecución.
// ===========================================================================

DateTime hoy = new(2026, 10, 15);

CuentaAhorro ahorro = new("AHO-001", "Ana Pérez", 1_000.00m);
CuentaCorriente corriente = new("COR-001", "Luis Gómez", 300.00m);
CuentaInversion inversion = new("INV-001", "María Díaz", 20_000.00m, fechaVencimiento: new DateTime(2027, 3, 31));
CuentaEmpresarial empresarial = new("EMP-001", "Bales S.A.", 50_000.00m);
Cuenta[] cuentas = [ahorro, corriente, inversion, empresarial];

ProcesadorTransacciones procesador = new();

Console.WriteLine("Reglas de cada cuenta:");
Console.WriteLine($"  {ahorro.Numero}  ahorro       hasta {ahorro.LimiteRetirosMensuales} débitos al mes, sin sobregiro");
Console.WriteLine($"  {corriente.Numero}  corriente    sobregiro hasta {F(corriente.LimiteSobregiro)}, cargo {F(corriente.CargoPorSobregiro)} por débito en negativo");
Console.WriteLine($"  {inversion.Numero}  inversión    penalización {P(inversion.TasaPenalizacion)} antes del {inversion.FechaVencimiento:yyyy-MM-dd}, depósito mínimo {F(inversion.DepositoMinimo)}");
Console.WriteLine($"  {empresarial.Numero}  empresarial  comisión {P(empresarial.TasaComision)} por débito, autorización por encima de {F(empresarial.UmbralAutorizacion)}");

// --------------------------------------------------------------------------
// 1. EjecutarTransaccion: las cuatro clases de transacción sobre las cuatro
// cuentas. Algunas se rechazan a propósito, una por cada regla.
// --------------------------------------------------------------------------
Retiro retiroEnSobregiro = new(corriente, 600.00m, "cajero.centro", hoy);
Transaccion[] transacciones =
[
    new Deposito(ahorro, 250.00m, "cajero.centro", hoy),
    new Retiro(ahorro, 100.00m, "cajero.centro", hoy),
    new Retiro(ahorro, 50.00m, "app.movil", hoy),
    new PagoServicio(ahorro, "Electricidad", 80.00m, "app.movil", hoy),
    new Retiro(ahorro, 20.00m, "cajero.centro", hoy),                           // 4.º débito del mes
    retiroEnSobregiro,                                                          // deja la cuenta en negativo
    new Retiro(corriente, 300.00m, "cajero.centro", hoy),                       // pasaría del tope de sobregiro
    new Retiro(inversion, 1_000.00m, "banca.web", hoy),                         // antes del vencimiento
    new Transferencia(corriente, inversion, 50.00m, "banca.web", hoy),          // el crédito no llega al mínimo
    new Transferencia(empresarial, inversion, 15_000.00m, "banca.web", hoy),    // monto alto sin autorización
    new Transferencia(empresarial, inversion, 15_000.00m, "banca.web", hoy, autorizadaPor: "gerente.finanzas"),
    new PagoServicio(empresarial, "Telefonía", 1_200.00m, "banca.web", hoy),
];

Titulo("1. EjecutarTransaccion");
foreach (Transaccion transaccion in transacciones)
{
    ResultadoTransaccion resultado = procesador.Ejecutar(transaccion);
    Console.WriteLine(resultado.FueAplicada
        ? $"  Aplicada   {transaccion.Descripcion}"
        : $"  Rechazada  {transaccion.Descripcion}\n             motivo: {resultado.MotivoRechazo}");
}

Console.WriteLine();
Console.WriteLine("  La transferencia de COR-001 a INV-001 llegó a aplicar el débito en COR-001; al rechazarse");
Console.WriteLine("  el crédito en INV-001, el procesador lo revirtió: la cuenta quedó como estaba (todo o nada).");
EstadoDeLasCuentas();

// --------------------------------------------------------------------------
// 2. RevertirTransaccion: deshace el retiro que dejó COR-001 en sobregiro.
// Devuelve el monto y el cargo, y descuenta esa vez en sobregiro.
// --------------------------------------------------------------------------
Titulo($"2. RevertirTransaccion: {retiroEnSobregiro.Descripcion}");
Console.WriteLine($"  antes:   saldo {F(corriente.Saldo)}, veces en sobregiro {corriente.VecesEnSobregiro}");
procesador.Revertir(retiroEnSobregiro);
Console.WriteLine($"  después: saldo {F(corriente.Saldo)}, veces en sobregiro {corriente.VecesEnSobregiro}");

// --------------------------------------------------------------------------
// 3. AuditarTransaccion: un registro por transacción y cuenta, sin modificar
// nada. Cada tipo de cuenta revisa su propia regla.
// --------------------------------------------------------------------------
Titulo("3. AuditarTransaccion");
Console.WriteLine($"  {"Cuenta",-8} {"Resultado",-10} {"Conforme",-8}  Qué / quién / observación");
foreach (Transaccion transaccion in transacciones)
{
    foreach (RegistroAuditoria registro in procesador.Auditar(transaccion))
    {
        Console.WriteLine($"  {registro.Cuenta,-8} {registro.Estado,-10} {(registro.Conforme ? "sí" : "NO"),-8}  " +
                          $"{registro.Descripcion} · {registro.Usuario} · {registro.Fecha:yyyy-MM-dd}");
        Console.WriteLine($"  {"",-29}{registro.Observacion}");
    }
}

// --------------------------------------------------------------------------
// 4. Extensibilidad: CalcularIntereses es una operación nueva. Solo se creó
// esa clase; ninguna cuenta ni transacción cambió.
// --------------------------------------------------------------------------
Titulo("4. Operación nueva: CalcularIntereses (una clase nueva, 0 cuentas modificadas)");
CalcularIntereses intereses = new();
foreach (Cuenta cuenta in cuentas)
{
    cuenta.Aceptar(intereses);
}

foreach ((string numero, decimal interes) in intereses.Intereses)
{
    Console.WriteLine($"  {numero}  {F(interes),9}");
}

Console.WriteLine($"  total     {F(intereses.Total),9}");
EstadoDeLasCuentas();

// --------------------------------------------------------------------------
// 5. Composite (inciso c): grupos anidados de cuentas. El banco tiene una
// región con dos sucursales y una sucursal de empresas. Un grupo recibe el
// mismo Aceptar que una cuenta.
// --------------------------------------------------------------------------
CuentaAhorro ahorroSinFondos = new("AHO-002", "Pedro Ruiz", 3.00m);
GrupoCuentas sucursalCentro = new GrupoCuentas("Sucursal Centro").Agregar(ahorro, corriente);
GrupoCuentas sucursalPlaya = new GrupoCuentas("Sucursal Playa").Agregar(inversion, ahorroSinFondos);
GrupoCuentas regionOccidente = new GrupoCuentas("Región Occidente").Agregar(sucursalCentro, sucursalPlaya);
GrupoCuentas sucursalEmpresas = new GrupoCuentas("Sucursal Empresas").Agregar(empresarial);
GrupoCuentas banco = new GrupoCuentas("Banco").Agregar(regionOccidente, sucursalEmpresas);

Titulo("5. Composite: totales por grupo en un solo recorrido (ResumirPorGrupo)");
ImprimirResumen(banco);

CalcularIntereses interesesDelBanco = new();
banco.Aceptar(interesesDelBanco); // el mismo visitante del punto 4, sin cambios
Console.WriteLine($"\n  CalcularIntereses sobre el banco entero (mismo visitante, sin cambios): total {F(interesesDelBanco.Total)}");

Console.WriteLine("\n  Cobro de mantenimiento de 5.00 a todas las cuentas del banco (mejor esfuerzo):");
IReadOnlyList<ResultadoTransaccion> reporte = procesador.EjecutarEnCadaCuenta(
    banco, cuenta => new PagoServicio(cuenta, "Mantenimiento", 5.00m, "sistema", hoy));
foreach (ResultadoTransaccion resultado in reporte)
{
    Console.WriteLine(resultado.FueAplicada
        ? $"  Aplicada   {resultado.Transaccion.Descripcion}"
        : $"  Rechazada  {resultado.Transaccion.Descripcion}\n             motivo: {resultado.MotivoRechazo}");
}

Console.WriteLine($"  → {reporte.Count(r => r.FueAplicada)} aplicadas y {reporte.Count(r => !r.FueAplicada)} rechazadas; " +
                  "las rechazadas no tocaron sus cuentas.");
ImprimirResumen(banco);

// --------------------------------------------------------------------------
// 6. Iterator (inciso c): el historial de una cuenta se recorre sin exponer
// su lista interna, completo o filtrado por tipo de transacción.
// --------------------------------------------------------------------------
Titulo($"6. Iterator: historial de {corriente.Numero}");
foreach (Asiento asiento in corriente.Historial())
{
    Console.WriteLine($"  {asiento.Transaccion.Descripcion,-46} {asiento.Naturaleza,-7} efecto {F(asiento.Efecto),8}" +
                      (asiento.Revertido ? "  (revertido)" : ""));
}

Console.WriteLine($"\n  Solo las transferencias de {corriente.Numero}:");
foreach (Asiento asiento in corriente.Historial<Transferencia>())
{
    Console.WriteLine($"  {asiento.Transaccion.Descripcion}");
}

void ImprimirResumen(GrupoCuentas raiz)
{
    ResumirPorGrupo resumir = new();
    raiz.Aceptar(resumir);
    foreach (ResumenGrupo grupo in resumir.Resumenes)
    {
        string nombre = new string(' ', 2 * grupo.Nivel) + grupo.Nombre;
        string cuentasDelGrupo = grupo.Cuentas == 1 ? "1 cuenta " : $"{grupo.Cuentas} cuentas";
        Console.WriteLine($"  {nombre,-22} {cuentasDelGrupo}  saldo {F(grupo.SaldoTotal),9}  cargos cobrados {F(grupo.CargosCobrados),6}");
    }
}

void EstadoDeLasCuentas()
{
    Console.WriteLine();
    Console.WriteLine("  Estado de las cuentas:");
    Console.WriteLine($"  {ahorro.Numero}  saldo {F(ahorro.Saldo),9}  retiros del mes {ahorro.RetirosDelMes} de {ahorro.LimiteRetirosMensuales}");
    Console.WriteLine($"  {corriente.Numero}  saldo {F(corriente.Saldo),9}  veces en sobregiro {corriente.VecesEnSobregiro}");
    Console.WriteLine($"  {inversion.Numero}  saldo {F(inversion.Saldo),9}  penalizaciones cobradas {F(inversion.PenalizacionesCobradas)}");
    Console.WriteLine($"  {empresarial.Numero}  saldo {F(empresarial.Saldo),9}  comisiones cobradas {F(empresarial.ComisionesCobradas)}, operaciones autorizadas {empresarial.OperacionesAutorizadas}");
}

static void Titulo(string texto) => Console.WriteLine($"\n――― {texto} ―――");

static string F(decimal monto) => Dinero.Formato(monto);

static string P(decimal tasa) => (tasa * 100m).ToString("0.##", CultureInfo.InvariantCulture) + " %";
