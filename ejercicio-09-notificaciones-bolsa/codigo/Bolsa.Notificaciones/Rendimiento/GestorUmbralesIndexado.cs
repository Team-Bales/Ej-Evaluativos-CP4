using Bolsa.Notificaciones.Entrega;
using Bolsa.Notificaciones.Observer;

namespace Bolsa.Notificaciones.Rendimiento;

/// <summary>
/// Inciso d): un único observador que concentra todas las suscripciones de una
/// acción en un índice ordenado por umbral.
///
/// Es el <i>ChangeManager</i> que GoF propone cuando la lógica de actualización
/// se vuelve costosa (Gamma et al., 1994, p. 333): la acción sigue siendo el
/// sujeto y sigue sin conocer a los clientes, porque solo ve un
/// <see cref="IObservadorPrecio"/>; el índice vive aquí dentro.
///
/// La ganancia está en que un cambio de precio ya no recorre los n umbrales:
/// como están ordenados, los que se cruzaron forman un tramo contiguo que se
/// localiza con dos búsquedas binarias. El costo pasa de O(n) a O(log n + k),
/// donde k son los clientes que de verdad hay que avisar.
/// </summary>
public sealed class GestorUmbralesIndexado : IObservadorPrecio
{
    private readonly decimal[] _umbralesSube;
    private readonly Cliente[] _clientesSube;
    private readonly decimal[] _umbralesBaja;
    private readonly Cliente[] _clientesBaja;
    private readonly IDespachadorNotificaciones _despachador;

    private GestorUmbralesIndexado(
        (decimal[] Umbrales, Cliente[] Clientes) sube,
        (decimal[] Umbrales, Cliente[] Clientes) baja,
        IDespachadorNotificaciones despachador,
        bool enParalelo)
    {
        _umbralesSube = sube.Umbrales;
        _clientesSube = sube.Clientes;
        _umbralesBaja = baja.Umbrales;
        _clientesBaja = baja.Clientes;
        _despachador = despachador;
        EnParalelo = enParalelo;
    }

    /// <summary>Si los avisos del tramo encontrado se reparten por el thread pool.</summary>
    public bool EnParalelo { get; }

    /// <summary>Suscripciones indexadas.</summary>
    public int Suscripciones => _umbralesSube.Length + _umbralesBaja.Length;

    /// <summary>
    /// Construye el índice. Ordenar cuesta O(n log n) una sola vez; después,
    /// cada cambio de precio se resuelve en O(log n + k).
    /// </summary>
    /// <param name="enParalelo">
    /// Reparte los avisos del tramo entre los hilos del thread pool. Solo tiene
    /// sentido con un despachador seguro para varios hilos.
    /// </param>
    public static GestorUmbralesIndexado Construir(
        IEnumerable<SuscripcionUmbral> suscripciones,
        IDespachadorNotificaciones despachador,
        bool enParalelo = false)
    {
        ArgumentNullException.ThrowIfNull(suscripciones);
        ArgumentNullException.ThrowIfNull(despachador);

        SuscripcionUmbral[] todas = suscripciones.ToArray();

        return new GestorUmbralesIndexado(
            Indexar(todas, Direccion.Sube),
            Indexar(todas, Direccion.Baja),
            despachador,
            enParalelo);
    }

    /// <summary>
    /// Misma regla de disparo que <see cref="AlertaUmbral"/>, pero resuelta
    /// sobre el índice: una subida solo puede cruzar umbrales «Sube» con
    /// umbral en [precio anterior, precio nuevo), y una bajada solo umbrales
    /// «Baja» con umbral en (precio nuevo, precio anterior].
    /// </summary>
    public void Actualizar(CambioPrecio cambio)
    {
        if (cambio.PrecioNuevo > cambio.PrecioAnterior)
        {
            Avisar(
                _umbralesSube,
                _clientesSube,
                PrimeroMayorOIgual(_umbralesSube, cambio.PrecioAnterior),
                PrimeroMayorOIgual(_umbralesSube, cambio.PrecioNuevo),
                Direccion.Sube,
                cambio);
        }
        else if (cambio.PrecioNuevo < cambio.PrecioAnterior)
        {
            Avisar(
                _umbralesBaja,
                _clientesBaja,
                PrimeroMayor(_umbralesBaja, cambio.PrecioNuevo),
                PrimeroMayor(_umbralesBaja, cambio.PrecioAnterior),
                Direccion.Baja,
                cambio);
        }
    }

    private void Avisar(
        decimal[] umbrales,
        Cliente[] clientes,
        int desde,
        int hasta,
        Direccion direccion,
        CambioPrecio cambio)
    {
        if (EnParalelo)
        {
            Parallel.For(desde, hasta, i => Encolar(umbrales, clientes, i, direccion, cambio));
            return;
        }

        for (int i = desde; i < hasta; i++)
        {
            Encolar(umbrales, clientes, i, direccion, cambio);
        }
    }

    private void Encolar(decimal[] umbrales, Cliente[] clientes, int i, Direccion direccion, CambioPrecio cambio) =>
        _despachador.Encolar(new Notificacion(
            Guid.NewGuid(), clientes[i], AlertaUmbral.Mensaje(cambio, umbrales[i], direccion)));

    private static (decimal[] Umbrales, Cliente[] Clientes) Indexar(
        SuscripcionUmbral[] suscripciones, Direccion direccion)
    {
        SuscripcionUmbral[] ordenadas = suscripciones
            .Where(s => s.Direccion == direccion)
            .OrderBy(s => s.Umbral)
            .ToArray();

        decimal[] umbrales = new decimal[ordenadas.Length];
        Cliente[] clientes = new Cliente[ordenadas.Length];

        for (int i = 0; i < ordenadas.Length; i++)
        {
            umbrales[i] = ordenadas[i].Umbral;
            clientes[i] = ordenadas[i].Cliente;
        }

        return (umbrales, clientes);
    }

    /// <summary>Primer índice cuyo umbral es mayor o igual que el objetivo.</summary>
    private static int PrimeroMayorOIgual(decimal[] umbrales, decimal objetivo)
    {
        int izquierda = 0;
        int derecha = umbrales.Length;

        while (izquierda < derecha)
        {
            int medio = izquierda + (derecha - izquierda) / 2;

            if (umbrales[medio] < objetivo)
            {
                izquierda = medio + 1;
            }
            else
            {
                derecha = medio;
            }
        }

        return izquierda;
    }

    /// <summary>Primer índice cuyo umbral es estrictamente mayor que el objetivo.</summary>
    private static int PrimeroMayor(decimal[] umbrales, decimal objetivo)
    {
        int izquierda = 0;
        int derecha = umbrales.Length;

        while (izquierda < derecha)
        {
            int medio = izquierda + (derecha - izquierda) / 2;

            if (umbrales[medio] <= objetivo)
            {
                izquierda = medio + 1;
            }
            else
            {
                derecha = medio;
            }
        }

        return izquierda;
    }
}
