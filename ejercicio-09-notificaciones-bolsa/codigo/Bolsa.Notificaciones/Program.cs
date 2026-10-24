using Bolsa.Notificaciones.Demos;

// ===========================================================================
// Punto de entrada. Cada inciso del ejercicio 9 tiene su propia demostración:
//
//   dotnet run --project Bolsa.Notificaciones                  → inciso b)
//   dotnet run --project Bolsa.Notificaciones -- asincrono     → inciso c)
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

    default:
        Console.Error.WriteLine($"Modo desconocido: «{modo}».");
        Console.Error.WriteLine("Modos: simulacion (inciso b), asincrono (inciso c).");
        return 1;
}
