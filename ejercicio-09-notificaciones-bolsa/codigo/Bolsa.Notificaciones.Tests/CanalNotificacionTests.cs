using Bolsa.Notificaciones.Entrega;

namespace Bolsa.Notificaciones.Tests;

/// <summary>
/// Prueba de que cada canal entrega el mensaje por el contacto de su cliente.
/// Los tres canales reales escriben en la consola; xUnit ejecuta los tests de
/// una misma clase en serie, así que la redirección de Console.Out es segura.
/// </summary>
public sealed class CanalNotificacionTests
{
    private const string Mensaje = "AAPL subió por encima de su umbral 152.00: 151.90 → 152.05";

    [Fact]
    public async Task CanalEmail_EnviaElMensajeAlContactoDelCliente()
    {
        string texto = await CapturarSalidaAsync(new CanalEmail(), "cliente0001@correo.cu");

        Assert.Contains("[EMAIL → cliente0001@correo.cu]", texto);
        Assert.Contains(Mensaje, texto);
    }

    [Fact]
    public async Task CanalSms_EnviaElMensajeAlContactoDelCliente()
    {
        string texto = await CapturarSalidaAsync(new CanalSms(), "+53 50000001");

        Assert.Contains("[SMS → +53 50000001]", texto);
        Assert.Contains(Mensaje, texto);
    }

    [Fact]
    public async Task CanalPush_EnviaElMensajeAlContactoDelCliente()
    {
        string texto = await CapturarSalidaAsync(new CanalPush(), "dispositivo-0001");

        Assert.Contains("[PUSH → dispositivo-0001]", texto);
        Assert.Contains(Mensaje, texto);
    }

    private static async Task<string> CapturarSalidaAsync(ICanalNotificacion canal, string contacto)
    {
        Cliente cliente = new("C0001", contacto, canal);
        Notificacion notificacion = new(Guid.NewGuid(), cliente, Mensaje);

        TextWriter salidaOriginal = Console.Out;
        await using StringWriter captura = new();
        Console.SetOut(captura);
        try
        {
            await canal.EnviarAsync(notificacion);
        }
        finally
        {
            Console.SetOut(salidaOriginal);
        }

        return captura.ToString();
    }
}
