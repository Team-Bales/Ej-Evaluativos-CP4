# Ejercicio 9: Sistema de Notificaciones de Bolsa

## Problema

Una plataforma de trading necesita notificar automáticamente a miles de clientes cuando ciertas acciones alcanzan precios específicos. Los clientes quieren suscribirse a diferentes acciones y umbrales, y recibir notificaciones inmediatas.

## Incisos

- [ ] **a) Patrón:** Identifica el patrón de diseño más adecuado, justifica tu selección basándote en las características del problema y explica la propuesta de solución considerando las clases que intervienen y las relaciones entre ellas. ¿Por qué no usar Mediator o polling?
- [ ] **b) Implementación:** Implementa con 5 acciones, 1000 clientes y umbrales personalizados. Notificaciones por email, SMS y push.
- [ ] **c) Análisis:** ¿Qué pasa si un cliente no puede ser notificado? ¿Cómo evitar bloquear el hilo principal? ¿Observer es síncrono o asíncrono?
- [ ] **d) Rendimiento:** Evalúa el rendimiento con 1M de clientes. ¿Cómo cambia el orden de complejidad? Propón Observer con Thread Pool o Reactive Streams.

## Restricción

- [ ] El sistema debe garantizar entrega "at-least-once" y persistir notificaciones fallidas.

## Solución

*Pendiente.*
