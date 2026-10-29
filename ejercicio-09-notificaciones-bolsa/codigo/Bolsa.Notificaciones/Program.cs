using Bolsa.Notificaciones.Demos;

// ===========================================================================
// Punto de entrada. Cada inciso del ejercicio 9 tiene su propia demostración:
//
//   dotnet run --project Bolsa.Notificaciones                  → inciso b)
//   dotnet run --project Bolsa.Notificaciones -- asincrono     → inciso c)
//   dotnet run --project Bolsa.Notificaciones -- rendimiento   → inciso d)
//   dotnet run --project Bolsa.Notificaciones -- restriccion   → restricción E9-R
//
// El modo por omisión es la simulación del inciso b), para que el comando del
// README de ese inciso siga haciendo lo mismo que antes.
// ===========================================================================

string modo = args.Length > 0 ? args[0].ToLowerInvariant() : "simulacion";

switch (modo)
{
    case "simulacion":
        SimulacionBolsa.Ejecutar();
        return 0;

    case "asincrono":
        await DemoAsincrona.EjecutarAsync();
        return 0;

    case "rendimiento":
        // Argumento opcional: número máximo de clientes (por omisión, 1 000 000).
        await BenchmarkRendimiento.EjecutarAsync(args.Length > 1 ? args[1] : null);
        return 0;

    case "restriccion":
        await DemoPersistencia.EjecutarAsync();
        return 0;

    default:
        Console.Error.WriteLine($"Modo desconocido: «{modo}».");
        Console.Error.WriteLine("Modos: simulacion (inciso b), asincrono (inciso c), rendimiento (inciso d), restriccion (E9-R).");
        return 1;
}
