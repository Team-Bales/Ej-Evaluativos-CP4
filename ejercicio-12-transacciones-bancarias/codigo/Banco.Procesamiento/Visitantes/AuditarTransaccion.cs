using Banco.Procesamiento.Cuentas;
using Banco.Procesamiento.Transacciones;

namespace Banco.Procesamiento.Visitantes;

/// <summary>
/// ConcreteVisitor (GoF): revisa, sin modificar nada, qué le hizo una
/// transacción a cada cuenta y si eso cumple las reglas de su tipo. Juzga la
/// transacción por su asiento (el cargo que cobró, la autorización y el saldo
/// en que dejó la cuenta o en que la dejó su reversión), no por el estado
/// actual de la cuenta, que pueden haber cambiado transacciones posteriores.
/// Por cada cuenta visitada anota un RegistroAuditoria (quién, qué, cuándo y
/// resultado). Es el único de los tres visitantes con estado propio: acumula
/// los registros mientras recorre las cuentas (GoF, «Accumulating state»).
/// </summary>
public sealed class AuditarTransaccion : IVisitanteCuenta
{
    private readonly List<RegistroAuditoria> _registros = [];

    public AuditarTransaccion(Transaccion transaccion)
    {
        Transaccion = transaccion;
    }

    /// <summary>La transacción que se audita.</summary>
    public Transaccion Transaccion { get; }

    /// <summary>Un registro por cuenta visitada, en el orden de la visita.</summary>
    public IReadOnlyList<RegistroAuditoria> Registros => _registros.AsReadOnly();

    /// <summary>Ahorro: la transacción no dejó la cuenta en negativo.</summary>
    public void Visitar(CuentaAhorro cuenta)
    {
        Asiento? asiento = cuenta.AsientoDe(Transaccion.Id);
        if (asiento == null)
        {
            RegistrarSinMovimiento(cuenta);
            return;
        }

        string detalle = asiento.Naturaleza == Naturaleza.Debito
            ? $"débito del mes {CuentaAhorro.MesDe(Transaccion.Fecha):yyyy-MM}"
            : "crédito";
        Registrar(cuenta, asiento, SaldoFinal(asiento) >= 0m, detalle);
    }

    /// <summary>
    /// Corriente: el saldo no quedó por debajo del tope de sobregiro, y el cargo
    /// se cobró solo si el débito dejó la cuenta en negativo.
    /// </summary>
    public void Visitar(CuentaCorriente cuenta)
    {
        Asiento? asiento = cuenta.AsientoDe(Transaccion.Id);
        if (asiento == null)
        {
            RegistrarSinMovimiento(cuenta);
            return;
        }

        bool dejoEnNegativo = asiento.Naturaleza == Naturaleza.Debito && asiento.SaldoResultante < 0m;
        decimal cargoEsperado = dejoEnNegativo ? cuenta.CargoPorSobregiro : 0m;
        string detalle = asiento.Naturaleza == Naturaleza.Credito ? "crédito"
            : dejoEnNegativo ? $"débito que dejó la cuenta en sobregiro; cargo {Dinero.Formato(asiento.Cargo)}"
            : "débito sin sobregiro";
        bool conforme = asiento.Cargo == cargoEsperado && SaldoFinal(asiento) >= -cuenta.LimiteSobregiro;
        Registrar(cuenta, asiento, conforme, detalle);
    }

    /// <summary>
    /// Inversión: un crédito debe llegar al depósito mínimo; un débito debe
    /// haber pagado la penalización que corresponde a su fecha. Ninguno puede
    /// dejar la cuenta en negativo.
    /// </summary>
    public void Visitar(CuentaInversion cuenta)
    {
        Asiento? asiento = cuenta.AsientoDe(Transaccion.Id);
        if (asiento == null)
        {
            RegistrarSinMovimiento(cuenta);
            return;
        }

        bool saldoValido = SaldoFinal(asiento) >= 0m;
        if (asiento.Naturaleza == Naturaleza.Credito)
        {
            Registrar(cuenta, asiento, saldoValido && asiento.Monto >= cuenta.DepositoMinimo,
                      $"depósito de {Dinero.Formato(asiento.Monto)} (mínimo {Dinero.Formato(cuenta.DepositoMinimo)})");
            return;
        }

        bool anticipado = Transaccion.Fecha < cuenta.FechaVencimiento;
        decimal penalizacionEsperada = anticipado ? Dinero.Redondear(asiento.Monto * cuenta.TasaPenalizacion) : 0m;
        string detalle = anticipado
            ? $"retiro anticipado (vence el {cuenta.FechaVencimiento:yyyy-MM-dd}): penalización {Dinero.Formato(asiento.Cargo)}"
            : "retiro al vencimiento: sin penalización";
        Registrar(cuenta, asiento, saldoValido && asiento.Cargo == penalizacionEsperada, detalle);
    }

    /// <summary>
    /// Empresarial: un débito debe haber pagado su comisión y, si es de monto
    /// alto, venir autorizado por otra persona. Ninguno puede dejar la cuenta en
    /// negativo.
    /// </summary>
    public void Visitar(CuentaEmpresarial cuenta)
    {
        Asiento? asiento = cuenta.AsientoDe(Transaccion.Id);
        if (asiento == null)
        {
            RegistrarSinMovimiento(cuenta);
            return;
        }

        bool saldoValido = SaldoFinal(asiento) >= 0m;
        if (asiento.Naturaleza == Naturaleza.Credito)
        {
            Registrar(cuenta, asiento, saldoValido, "crédito: sin comisión");
            return;
        }

        bool requiereAutorizacion = asiento.Monto > cuenta.UmbralAutorizacion;
        bool autorizada = EjecutarTransaccion.TieneAutorizacionValida(Transaccion);
        bool comisionCorrecta = asiento.Cargo == Dinero.Redondear(asiento.Monto * cuenta.TasaComision);
        string detalle = $"comisión {Dinero.Formato(asiento.Cargo)}";
        if (requiereAutorizacion)
        {
            detalle += autorizada
                ? $"; monto alto autorizado por {Transaccion.AutorizadaPor}"
                : "; monto alto sin una autorización válida";
        }

        Registrar(cuenta, asiento, saldoValido && comisionCorrecta && (!requiereAutorizacion || autorizada), detalle);
    }

    /// <summary>El saldo en que quedó la cuenta: tras la transacción o, si se revirtió, tras la reversión.</summary>
    private static decimal SaldoFinal(Asiento asiento) => asiento.SaldoTrasRevertir ?? asiento.SaldoResultante;

    private void RegistrarSinMovimiento(Cuenta cuenta) =>
        Registrar(cuenta, null, conforme: true, "sin movimiento en esta cuenta: la transacción se rechazó o no se ejecutó");

    /// <summary>
    /// Parte común: quién, qué y cuándo salen de la transacción; el resultado y
    /// el saldo en que quedó la cuenta, del asiento.
    /// </summary>
    private void Registrar(Cuenta cuenta, Asiento? asiento, bool conforme, string detalle)
    {
        EstadoTransaccion estado = EstadoTransaccion.NoAplicada;
        string observacion = detalle;
        if (asiento != null && asiento.Revertido)
        {
            estado = EstadoTransaccion.Revertida;
            observacion += $"; revertida (monto y cargos devueltos), saldo tras revertir {Dinero.Formato(SaldoFinal(asiento))}";
        }
        else if (asiento != null)
        {
            estado = EstadoTransaccion.Aplicada;
            observacion += $"; saldo tras la transacción {Dinero.Formato(asiento.SaldoResultante)}";
        }

        _registros.Add(new RegistroAuditoria(Transaccion.Id, cuenta.Numero, Transaccion.Usuario,
                                             Transaccion.Descripcion, Transaccion.Fecha, estado, conforme,
                                             observacion));
    }
}
