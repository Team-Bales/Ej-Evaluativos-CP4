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
