using Banco.Procesamiento.Cuentas;

namespace Banco.Procesamiento.Visitantes;

/// <summary>
/// ConcreteVisitor de apoyo: junta las cuentas (las hojas) de una cuenta o de
/// un grupo, en el orden del recorrido. Así el procesador obtiene las cuentas
/// de cualquier ICuenta sin preguntar si es una cuenta o un grupo. Aquí los
/// cuatro Visitar hacen lo mismo a propósito: lo único que importa es recorrer.
/// </summary>
public sealed class RecolectarCuentas : IVisitanteCuenta
{
    private readonly List<Cuenta> _cuentas = [];

    /// <summary>Las cuentas encontradas, en el orden del recorrido.</summary>
    public IReadOnlyList<Cuenta> Cuentas => _cuentas.AsReadOnly();

    /// <summary>Las cuentas de una cuenta (ella misma) o de un grupo (todas sus hojas).</summary>
    public static IReadOnlyList<Cuenta> De(ICuenta raiz)
    {
        RecolectarCuentas recolectar = new();
        raiz.Aceptar(recolectar);
        return recolectar.Cuentas;
    }

    public void Visitar(CuentaAhorro cuenta) => _cuentas.Add(cuenta);

    public void Visitar(CuentaCorriente cuenta) => _cuentas.Add(cuenta);

    public void Visitar(CuentaInversion cuenta) => _cuentas.Add(cuenta);

    public void Visitar(CuentaEmpresarial cuenta) => _cuentas.Add(cuenta);
}
