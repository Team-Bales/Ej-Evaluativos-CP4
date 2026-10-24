# Ejercicio 12: Sistema de Procesamiento de Transacciones Bancarias

## Problema

Un banco necesita procesar diferentes tipos de transacciones (depósito, retiro, transferencia, pago de servicios) sobre distintas cuentas (ahorro, corriente, inversión). Cada transacción debe poder ejecutarse, revertirse (rollback) y auditarse. El sistema debe permitir añadir nuevas operaciones (ej. "congelar cuenta", "calcular intereses") sin modificar las clases de las cuentas ni de las transacciones existentes. Además, algunas operaciones deben poder ejecutarse en paralelo sobre diferentes cuentas, pero manteniendo consistencia cuando afectan a la misma cuenta.

## Incisos

- [x] **a) Patrón:** ([respuesta](inciso-a.md)) Identifica el patrón de diseño más adecuado, justifica tu selección basándote en las características del problema y explica la propuesta de solución considerando las clases que intervienen y las relaciones entre ellas. ¿Por qué no usar Strategy o polimorfismo simple?
- [x] **b) Implementación:** ([respuesta](inciso-b.md)) Implementa con 4 tipos de elementos (CuentaAhorro, CuentaCorriente, CuentaInversión, CuentaEmpresarial) y 3 operaciones (EjecutarTransacción, RevertirTransacción, AuditarTransacción). Cada operación debe comportarse de manera diferente según el tipo de cuenta.
- [ ] **c) Integración:** Integra con Composite para manejar grupos de cuentas (ej. "Cuentas de una sucursal") y con Iterator para recorrer las transacciones de una cuenta. ¿Cómo afecta el Composite a la ejecución de operaciones sobre grupos?
- [ ] **d) Comparación:** Visitor vs. añadir métodos a cada elemento. ¿Cuándo conviene cada uno? Usa la métrica de "número de cambios por nueva operación" y "número de cambios por nuevo tipo de elemento". ¿Qué pasa si el número de operaciones crece mucho más rápido que el número de tipos de cuenta?

## Restricción

- [ ] Las operaciones deben poder ejecutarse en paralelo sobre diferentes cuentas (considera concurrencia y estado mutable del Visitor). Además, el sistema debe garantizar que las transacciones sobre una misma cuenta sean atómicas y consistentes (¿cómo lo resuelves con el patrón elegido?).

## Solución

- a) [Identificación del patrón](inciso-a.md): Visitor. Diagramas de clases en [`diagramas/`](diagramas/), con una [guía para leerlos](diagramas/como-leer-los-diagramas.md).
- b) [Implementación](inciso-b.md): código en [`codigo/`](codigo/)

### Cómo ejecutar el código

Requisito: SDK de .NET 10. Desde [`codigo/`](codigo/):

```sh
dotnet run --project Banco.Procesamiento   # la simulación (4 cuentas, 12 transacciones)
dotnet test                                 # las pruebas unitarias (xUnit)
```

Hay que indicar `--project` porque la carpeta tiene dos proyectos (el programa y sus pruebas). Las reglas de cada cuenta y los resultados están explicados en el [inciso b](inciso-b.md).
