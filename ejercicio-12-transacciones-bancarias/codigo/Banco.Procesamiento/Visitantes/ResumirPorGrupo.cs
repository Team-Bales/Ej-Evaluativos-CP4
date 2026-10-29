using Banco.Procesamiento.Cuentas;

namespace Banco.Procesamiento.Visitantes;

/// <summary>
/// ConcreteVisitor del inciso c: totales por grupo (por sucursal, por región,
/// del banco) en un solo recorrido del árbol. Usa EntrarEnGrupo y SalirDeGrupo
/// para saber qué grupos están abiertos: cada cuenta suma en su sucursal y
/// también en la región y el banco que la contienen. Los cargos cobrados se
/// leen distinto según el tipo de cuenta.
/// </summary>
public sealed class ResumirPorGrupo : IVisitanteCuenta
{
    private readonly List<Acumulado> _abiertos = [];
    private readonly List<ResumenGrupo?> _resumenes = [];

    /// <summary>Un resumen por grupo, en el orden en que se recorrieron (el banco primero).</summary>
    public IReadOnlyList<ResumenGrupo> Resumenes => _resumenes.Select(r => r!).ToList();

    public void EntrarEnGrupo(GrupoCuentas grupo)
    {
        _abiertos.Add(new Acumulado(grupo.Nombre, _abiertos.Count, _resumenes.Count));
        _resumenes.Add(null); // se completa al salir, para conservar el orden de entrada
    }

    public void SalirDeGrupo(GrupoCuentas grupo)
    {
        Acumulado acumulado = _abiertos[^1];
        _abiertos.RemoveAt(_abiertos.Count - 1);
        _resumenes[acumulado.Posicion] = new ResumenGrupo(acumulado.Nombre, acumulado.Nivel, acumulado.Cuentas,
                                                          acumulado.Saldo, acumulado.Cargos);
    }

    /// <summary>Ahorro: no cobra cargos.</summary>
    public void Visitar(CuentaAhorro cuenta) => Sumar(cuenta, cargos: 0m);

    /// <summary>Corriente: el cargo fijo por cada vez que quedó en sobregiro.</summary>
    public void Visitar(CuentaCorriente cuenta) => Sumar(cuenta, cuenta.VecesEnSobregiro * cuenta.CargoPorSobregiro);

    /// <summary>Inversión: las penalizaciones por retiro anticipado.</summary>
    public void Visitar(CuentaInversion cuenta) => Sumar(cuenta, cuenta.PenalizacionesCobradas);

    /// <summary>Empresarial: las comisiones por débito.</summary>
    public void Visitar(CuentaEmpresarial cuenta) => Sumar(cuenta, cuenta.ComisionesCobradas);

    /// <summary>La cuenta suma en todos los grupos abiertos: los que la contienen.</summary>
    private void Sumar(Cuenta cuenta, decimal cargos)
    {
        foreach (Acumulado grupo in _abiertos)
        {
            grupo.Cuentas++;
            grupo.Saldo += cuenta.Saldo;
            grupo.Cargos += cargos;
        }
    }

    private sealed class Acumulado(string nombre, int nivel, int posicion)
    {
        public string Nombre { get; } = nombre;

        public int Nivel { get; } = nivel;

        public int Posicion { get; } = posicion;

        public int Cuentas { get; set; }

        public decimal Saldo { get; set; }

        public decimal Cargos { get; set; }
    }
}

/// <summary>Totales de un grupo de cuentas.</summary>
/// <param name="Nombre">Nombre del grupo.</param>
/// <param name="Nivel">Profundidad en el árbol: 0 para la raíz.</param>
/// <param name="Cuentas">Cuentas que contiene, contando las de sus subgrupos.</param>
/// <param name="SaldoTotal">Suma de sus saldos.</param>
/// <param name="CargosCobrados">Comisiones, penalizaciones y cargos por sobregiro cobrados.</param>
public sealed record ResumenGrupo(string Nombre, int Nivel, int Cuentas, decimal SaldoTotal, decimal CargosCobrados);
