# Ejercicio 12, inciso d): Visitor frente a métodos en cada elemento

**Issue:** #9

> *Visitor vs. añadir métodos a cada elemento. ¿Cuándo conviene cada uno? Usa la métrica de "número de cambios por nueva operación" y "número de cambios por nuevo tipo de elemento". ¿Qué pasa si el número de operaciones crece mucho más rápido que el número de tipos de cuenta?*

Las dos alternativas son:

- **Visitor**, la solución de los incisos a–c: cada operación es una clase visitante con un `Visitar` por tipo de cuenta.
- **Métodos en cada elemento**, el «polimorfismo simple» del inciso a: cada operación es un método abstracto en `Cuenta` que cada cuenta redefine.

Se comparan con las dos métricas del enunciado, contando clases nuevas y clases existentes modificadas. Las cifras salen del propio proyecto: E = 4 tipos de cuenta, y O = 3 operaciones del enunciado (más las que se agregaron en los incisos b y c).

## Las dos métricas

| | Cambios por **nueva operación** | Cambios por **nuevo tipo de cuenta** |
|---|---|---|
| **Visitor** | 1 clase nueva; **0** clases modificadas | 1 clase nueva + la interfaz del visitante + los **O** visitantes modificados |
| **Métodos en cada elemento** | 0 clases nuevas; la clase base + las **E** cuentas modificadas | 1 clase nueva que implementa las O operaciones; **0** clases modificadas |

Las dos métricas se oponen: lo que una alternativa hace fácil, la otra lo hace difícil. GoF lo resume así: Visitor facilita agregar operaciones (Gamma et al., 1994, p. 370) y dificulta agregar elementos, porque cada uno obliga a tocar todos los visitantes (p. 371).

**El trabajo de escribir código es el mismo en las dos:** cada combinación de operación y tipo de cuenta necesita su cuerpo de método (un `Visitar` o un `override`). Lo que cambia es **dónde** va ese código:

- en clases **nuevas**, que no rompen nada de lo que ya funciona y se prueban solas;
- o **modificando** clases que ya funcionan, con su riesgo de romperlas, de volver a probarlas y de chocar con otros cambios en las mismas clases.

### Medido en este proyecto

- **Nueva operación con Visitor.** `CalcularIntereses` (inciso b) está en un commit aparte, `94e3d6a`, y `git show --stat 94e3d6a` muestra 3 ficheros: la clase nueva, sus pruebas y su sección en `Program.cs`. **Ninguna cuenta cambió.** Lo mismo pasó con `ResumirPorGrupo` y `RecolectarCuentas` en el inciso c: una clase nueva cada una.
- **Nuevo tipo de cuenta con Visitor.** Se probó a agregar una `CuentaNomina` hipotética con su `Visitar` en la interfaz. **El compilador marcó 7 clases que había que cambiar:**
  - los 6 visitantes (`EjecutarTransaccion`, `RevertirTransaccion`, `AuditarTransaccion`, `CalcularIntereses`, `ResumirPorGrupo` y `RecolectarCuentas`);
  - el buscador de ciclos interno de `GrupoCuentas`.

  Hay que sumarles la interfaz y los visitantes de prueba. Hoy, con O = 6, un tipo de cuenta nuevo cuesta 1 clase nueva y 8 modificadas. Que el programa no compile hasta completarlos es una ventaja: ninguna operación se queda sin regla para la cuenta nueva.
- **El mismo cambio con métodos en cada elemento.** Una operación nueva como `CalcularIntereses` habría modificado `Cuenta` y las 4 cuentas: 5 clases (6 si la operación también se aplica a los grupos del inciso c). La `CuentaNomina`, en cambio, sería 1 clase nueva y 0 modificadas.

### Un caso intermedio: el Composite del inciso c

`GrupoCuentas` es un tipo de elemento nuevo, y con Visitor puro habría obligado a tocar todos los visitantes. No hizo falta, porque la interfaz ganó `EntrarEnGrupo` y `SalirDeGrupo` **con una implementación vacía por omisión** (C# lo permite desde la versión 8). El costo fue 1 clase nueva y 1 interfaz modificada; ningún visitante cambió.

GoF admite esta salida, pero como excepción (p. 371): funciona cuando existe un comportamiento razonable por omisión. Para un grupo lo hay: «no hacer nada al entrar ni al salir». Para una cuenta nueva no: cada operación tiene que decidir su regla, y ahí conviene que el compilador obligue a escribirla.

## Escenarios de crecimiento

Partiendo del enunciado (E = 4, O = 3):

| Escenario | Visitor | Métodos en cada elemento |
|---|---|---|
| **Agregar 5 operaciones** (congelar cuenta, calcular intereses, cierre de mes, estado de cuenta, cobrar mantenimiento) | 5 clases nuevas, **0 modificadas** | 0 nuevas; **25 modificaciones**: 5 × (base + 4 cuentas), cada cuenta tocada 5 veces |
| **Agregar 2 tipos de cuenta** (nómina, plazo fijo) | 2 clases nuevas; la interfaz y los 3 visitantes modificados (**4 clases**, cada visitante con 2 `Visitar` nuevos) | 2 clases nuevas, **0 modificadas** |

## ¿Cuándo conviene cada uno?

**Visitor conviene cuando los tipos de elemento son estables y las operaciones cambian a menudo.** Es el caso de este banco:

- un banco crea productos nuevos (tipos de cuenta) muy de vez en cuando;
- el enunciado anticipa operaciones nuevas («congelar cuenta», «calcular intereses»);
- en este mismo proyecto se agregaron 3 operaciones (intereses, resumen por grupo y recolectar cuentas) y ningún tipo de cuenta.

GoF lo pone como condición del patrón: que las clases de la estructura rara vez cambien (p. 368).

**Métodos en cada elemento conviene en tres situaciones:**

- **Cuando los tipos crecen y las operaciones son estables.** Por ejemplo, un catálogo de productos financieros que suma tipos de cuenta cada mes, siempre con las mismas tres operaciones.
- **Cuando las operaciones necesitan mucho estado interno de la cuenta.** Con Visitor, la cuenta tiene que exponer ese estado al visitante, lo que debilita la encapsulación (p. 372). Aquí se pagó ese precio con los contadores: lectura pública y `set` interno.
- **Cuando hay una o dos operaciones fijas.** En ese caso, la indirección del doble despacho no aporta nada.

## ¿Qué pasa si las operaciones crecen mucho más rápido que los tipos de cuenta?

**Visitor escala mucho mejor.** Su costo por operación nueva es **constante**: 1 clase nueva y 0 modificadas, sin importar cuántas cuentas haya. Con métodos en cada elemento, el costo por operación **crece con E**, porque cada operación nueva toca la base y todas las cuentas.

Por ejemplo, en un año con 10 operaciones nuevas y 1 tipo de cuenta nuevo, empezando con E = 4 y O = 3:

| | Visitor | Métodos en cada elemento |
|---|---|---|
| 10 operaciones nuevas | 10 clases nuevas, 0 modificadas | 10 × 5 = **50 modificaciones** |
| 1 tipo de cuenta nuevo | 1 clase nueva + interfaz + entre 3 y 13 visitantes modificados (según cuándo llegue) | 1 clase nueva, 0 modificadas |
| **Total de modificaciones** | **entre 4 y 14** | **50** |

**La otra cara:** con Visitor, cada tipo de cuenta nuevo cuesta más cuanto más operaciones haya (1 + O modificaciones). Si algún día los tipos de cuenta empezaran a crecer tan rápido como las operaciones, la ventaja se invertiría. GoF lo plantea como la pregunta clave para elegir el patrón: qué es más probable que cambie, los algoritmos o las clases de la estructura (p. 371).

Este dilema tiene nombre: el **problema de la expresión** (*expression problem*, Wadler, 1998). Consiste en extender a la vez los tipos de datos y las operaciones sin modificar el código existente. La programación orientada a objetos clásica facilita agregar tipos; Visitor invierte la balanza y facilita agregar operaciones. Las implementaciones por omisión de las interfaces, como en el inciso c, suavizan el costo cuando existe un comportamiento razonable por omisión.

## Referencias

Gamma, E., Helm, R., Johnson, R., y Vlissides, J. (1994). *Design patterns: Elements of reusable object-oriented software*. Addison-Wesley.

Wadler, P. (1998). *The expression problem* [mensaje a la lista de correo Java-genericity].

> Los números de página del libro corresponden a la copia de la bibliografía de la asignatura (`Documentacion Patrones`), igual que en los incisos anteriores.
