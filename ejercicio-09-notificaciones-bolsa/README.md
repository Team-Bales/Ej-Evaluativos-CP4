# Ejercicio 9: Sistema de Notificaciones de Bolsa

## Problema

Una plataforma de trading necesita notificar automáticamente a miles de clientes cuando ciertas acciones alcanzan precios específicos. Los clientes quieren suscribirse a diferentes acciones y umbrales, y recibir notificaciones inmediatas.

## Incisos

- [x] **a) Patrón:** ([respuesta](inciso-a.md)) Identifica el patrón de diseño más adecuado, justifica tu selección basándote en las características del problema y explica la propuesta de solución considerando las clases que intervienen y las relaciones entre ellas. ¿Por qué no usar Mediator o polling?
- [x] **b) Implementación:** ([respuesta](inciso-b.md)) Implementa con 5 acciones, 1000 clientes y umbrales personalizados. Notificaciones por email, SMS y push.
- [x] **c) Análisis:** ([respuesta](inciso-c.md)) ¿Qué pasa si un cliente no puede ser notificado? ¿Cómo evitar bloquear el hilo principal? ¿Observer es síncrono o asíncrono?
- [x] **d) Rendimiento:** ([respuesta](inciso-d.md)) Evalúa el rendimiento con 1M de clientes. ¿Cómo cambia el orden de complejidad? Propón Observer con Thread Pool o Reactive Streams.

## Restricción

- [ ] El sistema debe garantizar entrega "at-least-once" y persistir notificaciones fallidas.

## Solución

- a) [Identificación del patrón](inciso-a.md)
- b) [Implementación](inciso-b.md): código en [`codigo/`](codigo/)
- c) [Análisis](inciso-c.md): aislamiento de fallos y entrega asíncrona
- d) [Rendimiento](inciso-d.md): benchmark hasta 1M de clientes e índice ordenado de umbrales

### Cómo ejecutar el código

Requisito: SDK de .NET 10. Desde [`codigo/`](codigo/):

```sh
dotnet run --project Bolsa.Notificaciones                 # inciso b: la simulación (5 acciones, 1000 clientes)
dotnet run --project Bolsa.Notificaciones -- asincrono    # inciso c: fallos, reintentos y mediciones
dotnet run -c Release --project Bolsa.Notificaciones -- rendimiento   # inciso d: benchmark hasta 1M
dotnet test                                               # las pruebas unitarias (xUnit)
```

Hay que indicar `--project` porque la carpeta tiene dos proyectos (el programa y sus pruebas). El benchmark necesita `-c Release`: en `Debug` los tiempos no significan nada, y admite un tope opcional de clientes (`-- rendimiento 100000`) para que tarde menos.

La regla de disparo y los resultados están explicados en el [inciso b](inciso-b.md#regla-de-disparo); el aislamiento de fallos y la cola, en el [inciso c](inciso-c.md); el orden de complejidad y las mediciones, en el [inciso d](inciso-d.md).
