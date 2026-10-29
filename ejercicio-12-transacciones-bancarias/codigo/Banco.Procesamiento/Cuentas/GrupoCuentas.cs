using Banco.Procesamiento.Visitantes;

namespace Banco.Procesamiento.Cuentas;

/// <summary>
/// Composite (GoF): un grupo de cuentas, por ejemplo las de una sucursal.
/// Implementa la misma interfaz que una cuenta, así que el cliente le pasa un
/// visitante igual que a una cuenta. Sus hijos pueden ser cuentas u otros
/// grupos (sucursal → región → banco), y Aceptar los recorre en profundidad,
/// en el orden en que se agregaron.
/// </summary>
public sealed class GrupoCuentas : ICuenta
{
    private readonly List<ICuenta> _hijos = [];

    public GrupoCuentas(string nombre)
    {
        Nombre = nombre;
    }

    /// <summary>Nombre del grupo (p. ej. «Sucursal Centro»).</summary>
    public string Nombre { get; }

    /// <summary>
    /// Iterator (GoF): los hijos directos, en orden, sin exponer la lista
    /// interna. Quien lo recorre no puede agregar ni quitar hijos.
    /// </summary>
    public IEnumerable<ICuenta> Hijos
    {
        get
        {
            foreach (ICuenta hijo in _hijos)
            {
                yield return hijo;
            }
        }
    }

    /// <summary>
    /// Agrega cuentas o grupos. Rechaza un hijo que ya contenga a este grupo
    /// (o que sea el propio grupo): el árbol no puede tener ciclos, porque el
    /// recorrido no terminaría.
    /// </summary>
    public GrupoCuentas Agregar(params ICuenta[] hijos)
    {
        foreach (ICuenta hijo in hijos)
        {
            BuscarGrupo buscar = new(this);
            hijo.Aceptar(buscar);
            if (buscar.Encontrado)
            {
                throw new ArgumentException($"Agregar ese hijo a «{Nombre}» formaría un ciclo.", nameof(hijos));
            }

            _hijos.Add(hijo);
        }

        return this;
    }

    /// <summary>
    /// Avisa al visitante de que entra en el grupo, le pasa cada hijo (que a su
    /// vez, si es un grupo, recorre los suyos) y avisa de que sale. Los avisos de
    /// entrada y salida permiten acumular resultados por grupo.
    /// </summary>
    public void Aceptar(IVisitanteCuenta visitante)
    {
        visitante.EntrarEnGrupo(this);
        foreach (ICuenta hijo in _hijos)
        {
            hijo.Aceptar(visitante);
        }

        visitante.SalirDeGrupo(this);
    }

    /// <summary>Busca un grupo concreto dentro de un árbol, para detectar ciclos.</summary>
    private sealed class BuscarGrupo(GrupoCuentas buscado) : IVisitanteCuenta
    {
        public bool Encontrado { get; private set; }

        public void EntrarEnGrupo(GrupoCuentas grupo) => Encontrado |= grupo == buscado;

        public void Visitar(CuentaAhorro cuenta) { }

        public void Visitar(CuentaCorriente cuenta) { }

        public void Visitar(CuentaInversion cuenta) { }

        public void Visitar(CuentaEmpresarial cuenta) { }
    }
}
