using Banco.Procesamiento.Visitantes;

namespace Banco.Procesamiento.Cuentas;

/// <summary>
/// Element (GoF): lo que una operación puede visitar. Solo declara Aceptar;
/// las operaciones (ejecutar, revertir, auditar…) no viven en las cuentas sino
/// en los visitantes. Es lo único que el inciso c necesita para que un grupo
/// de cuentas (Composite) se trate igual que una cuenta.
/// </summary>
public interface ICuenta
{
    /// <summary>
    /// Doble despacho: cada cuenta concreta llama al Visitar de su propia clase,
    /// así el código que se ejecuta depende del tipo de operación (el visitante)
    /// y del tipo de cuenta, sin preguntar por ninguno de los dos.
    /// </summary>
    void Aceptar(IVisitanteCuenta visitante);
}
