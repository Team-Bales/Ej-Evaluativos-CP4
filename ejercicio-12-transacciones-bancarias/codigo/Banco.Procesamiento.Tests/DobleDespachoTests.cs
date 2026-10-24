using Banco.Procesamiento.Cuentas;
using Banco.Procesamiento.Visitantes;

namespace Banco.Procesamiento.Tests;

/// <summary>
/// Aceptar elige el Visitar de la clase concreta aunque quien llama solo
/// conozca la interfaz ICuenta: es el doble despacho, sin is, switch ni casts.
/// </summary>
public sealed class DobleDespachoTests
{
    private sealed class VisitanteQueAnota : IVisitanteCuenta
    {
        public List<string> Visitadas { get; } = [];

        public void Visitar(CuentaAhorro cuenta) => Visitadas.Add($"ahorro {cuenta.Numero}");

        public void Visitar(CuentaCorriente cuenta) => Visitadas.Add($"corriente {cuenta.Numero}");

        public void Visitar(CuentaInversion cuenta) => Visitadas.Add($"inversión {cuenta.Numero}");

        public void Visitar(CuentaEmpresarial cuenta) => Visitadas.Add($"empresarial {cuenta.Numero}");
    }

    [Fact]
    public void Aceptar_LlamaAlVisitarDeLaClaseConcretaDeCadaCuenta()
    {
        ICuenta[] cuentas = [Escenario.Empresarial(), Escenario.Ahorro(), Escenario.Inversion(), Escenario.Corriente()];
        VisitanteQueAnota visitante = new();

        foreach (ICuenta cuenta in cuentas)
        {
            cuenta.Aceptar(visitante);
        }

        Assert.Equal(["empresarial EMP-001", "ahorro AHO-001", "inversión INV-001", "corriente COR-001"],
                     visitante.Visitadas);
    }
}
