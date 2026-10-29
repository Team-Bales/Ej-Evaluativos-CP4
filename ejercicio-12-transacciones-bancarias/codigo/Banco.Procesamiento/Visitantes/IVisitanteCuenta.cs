using Banco.Procesamiento.Cuentas;

namespace Banco.Procesamiento.Visitantes;

/// <summary>
/// Visitor (GoF): una operación sobre las cuentas. Declara un Visitar por cada
/// clase de cuenta; la firma identifica la clase concreta, así cada operación
/// trata a cada tipo de cuenta a su manera sin preguntar por su tipo. Una
/// operación nueva es una clase nueva que implementa esta interfaz; ninguna
/// cuenta cambia.
/// </summary>
public interface IVisitanteCuenta
{
    void Visitar(CuentaAhorro cuenta);

    void Visitar(CuentaCorriente cuenta);

    void Visitar(CuentaInversion cuenta);

    void Visitar(CuentaEmpresarial cuenta);
}
