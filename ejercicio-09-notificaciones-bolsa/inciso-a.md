# Ejercicio 9, inciso a): Identificación del patrón

**Issue:** #1

> *Identifica el patrón de diseño más adecuado, justifica tu selección basándote en las características del problema y explica la propuesta de solución considerando las clases que intervienen y las relaciones entre ellas. ¿Por qué no usar Mediator o polling?*

## Patrón seleccionado: Observer (Publicador / Suscriptor)

Observer define una dependencia uno-a-muchos entre objetos: cuando un objeto cambia de estado, todos sus dependientes son notificados y se actualizan automáticamente (Gamma et al., 1994, p. 326).

## Justificación

El libro de la «banda de los cuatro» (*Gang of Four*, GoF), la referencia clásica de los patrones de diseño, recomienda Observer cuando un cambio en un objeto obliga a cambiar otros y no se sabe cuántos son, y cuando un objeto debe poder notificar a otros sin hacer suposiciones sobre quiénes son (Gamma et al., 1994, pp. 327–328). Ambas condiciones se cumplen en este problema:

| Característica del problema | Cómo la resuelve Observer |
|---|---|
| **Relación uno-a-muchos**: una acción, miles de clientes interesados. | El *Sujeto* (la acción) mantiene una lista de *Observadores* y les avisa a todos cuando cambia su precio. |
| **No se sabe de antemano cuántos dependientes hay.** Los clientes eligen acciones y umbrales y pueden cambiarlos en cualquier momento. | `Suscribir()` y `Desuscribir()` se llaman en tiempo de ejecución, sin tocar el código de la acción. |
| **Notificación inmediata.** | El cambio de precio dispara `Notificar()` en ese mismo instante; el cliente no tiene que preguntar. |
| **La acción no debe conocer a sus suscriptores** (bajo acoplamiento). | La acción solo conoce la interfaz `IObservadorPrecio`; no sabe si detrás hay un email, un SMS o un push. |
| **Extensibilidad** (nuevos canales: WhatsApp, Telegram, webhook…). | Se añade una nueva implementación de `ICanalNotificacion` sin modificar `Accion` (principio Abierto/Cerrado). |

> En .NET el patrón Observer viene incluso en la biblioteca base: `IObservable<T>` / `IObserver<T>` y la palabra clave `event`. Aquí se implementa de forma explícita para que se vean los roles del patrón.

## Propuesta de solución: clases y relaciones

![Diagrama de clases UML de la solución con Observer](diagramas/inciso-a-clases.png)

*Versión vectorial y editable del diagrama: [`diagramas/inciso-a-clases.svg`](diagramas/inciso-a-clases.svg).*

### Roles del patrón

- **Subject (`ISujetoPrecio`)**: interfaz con `Suscribir`, `Desuscribir` y `Notificar`.
- **ConcreteSubject (`Accion`)**: guarda el símbolo y el precio. Cuando el precio cambia, crea un `CambioPrecio` y notifica a sus observadores. Una acción tiene de 0 a muchos observadores.
- **Observer (`IObservadorPrecio`)**: interfaz con `Actualizar(CambioPrecio)`.
- **ConcreteObserver (`AlertaUmbral`)**: representa *una suscripción* de un cliente: acción, umbral y dirección (`Sube` o `Baja`). Decide si el cambio **cruza** su umbral y, si lo cruza, crea una `Notificacion`.

### Clases de apoyo (fuera de los roles de Observer)

- **Canales (`CanalEmail`, `CanalSms`, `CanalPush`)**: formas de envío intercambiables detrás de `ICanalNotificacion`. Funciona como un **Strategy**, porque cada cliente elige su canal. Los canales **no son observadores**: el observador decide *si* hay que avisar, y el canal decide *cómo* se entrega el aviso.
- **`DespachadorNotificaciones`**: separa la detección del evento del envío real. Se encarga de que el envío sea asíncrono (inciso c), de los reintentos y de la persistencia (restricción). En este inciso solo se presenta el diseño; se implementa en los incisos y en la restricción que lo usan. Guarda las notificaciones en un `IRepositorioNotificaciones`; `RepositorioMemoria` es la implementación en memoria que usa el inciso b.

### Decisiones de diseño

> **El observador es la suscripción (`AlertaUmbral`), no el `Cliente`.** Un cliente puede tener muchos umbrales sobre muchas acciones. Así cada observador tiene una sola responsabilidad: evaluar *su* condición. El umbral vive dentro del observador, no en la acción, que no necesita saber qué le interesa a cada cliente.
>
> **Se notifica al cruzar el umbral.** Solo hay aviso cuando el precio **cruza** el umbral, no en cada tick en que siga por encima. Así el cliente no recibe el mismo aviso una y otra vez.

### Modelo push o pull

GoF distingue dos extremos. En el modelo *push*, el sujeto envía a los observadores información detallada del cambio, la necesiten o no. En el modelo *pull*, el sujeto solo avisa que algo cambió, y cada observador pide después lo que necesita (Gamma et al., 1994, p. 332).

Esta solución usa el **modelo push**: `Notificar` envía un `CambioPrecio` con el símbolo, el precio anterior y el nuevo. Hay dos razones:

1. **Todos los observadores necesitan los mismos datos** (el precio anterior y el nuevo, para detectar el cruce). La desventaja que GoF señala para push, que el sujeto asume qué necesitan sus observadores, aquí no aplica.
2. **Con pull, cada uno de los miles de observadores volvería a consultar a la acción** después de cada aviso. Eso es justo la ineficiencia que GoF atribuye a ese modelo (Gamma et al., 1994, p. 332), y además el precio podría haber cambiado otra vez entre el aviso y la consulta.

## ¿Por qué no usar Mediator?

- **Mediator resuelve otro problema.** Sirve para encapsular cómo interactúa un conjunto de objetos, y conviene cuando esos objetos se comunican "de forma bien definida pero compleja", con interdependencias desestructuradas (Gamma et al., 1994, pp. 305, 308). Un ejemplo es la comunicación muchos-a-muchos entre aviones del ejercicio 6. Aquí el flujo es **uno-a-muchos y en un solo sentido**: la acción emite y los clientes solo reciben. Los clientes nunca se comunican entre sí, así que no hay interacciones complejas que encapsular.
- **Un mediador central concentraría toda la lógica** en una sola clase: qué cliente quiere qué acción, con qué umbral y por qué canal. Habría que modificarla cada vez que se añade un tipo de alerta, lo que viola los principios Abierto/Cerrado y de Responsabilidad Única. Observer, en cambio, reparte esa lógica: cada `AlertaUmbral` sabe evaluar su propia condición.
- **No son excluyentes.** GoF muestra que, cuando la lógica de actualización se vuelve compleja, un `ChangeManager` puede actuar como mediador entre sujetos y observadores (Gamma et al., 1994, p. 337). Aquí la regla de actualización es simple (comparar un precio con un umbral), así que ese intermediario no hace falta y Observer es el patrón principal.

## ¿Por qué no usar polling (consulta periódica)?

| Criterio | Polling | Observer (push) |
|---|---|---|
| Latencia | Hasta un intervalo completo. Si se consulta cada 5 s, el aviso puede llegar 5 s tarde, lo que en trading es inaceptable y contradice "inmediata". | Inmediata. |
| Carga | `clientes × acciones × frecuencia` consultas, **cambie o no el precio**. 1000 clientes que siguen 5 acciones, consultando cada 1 s, son 5000 peticiones/s, casi todas inútiles. | Solo hay trabajo cuando cambia el precio. |
| Eventos perdidos | Si el precio sube y vuelve a bajar entre dos consultas, el cruce **nunca se detecta**. | Cada cambio genera un evento. |
| Escalabilidad | El costo crece linealmente con los clientes y las acciones aunque no haya actividad. | El costo crece con los eventos relevantes. |

## Referencias

Gamma, E., Helm, R., Johnson, R., y Vlissides, J. (1994). *Design patterns: Elements of reusable object-oriented software*. Addison-Wesley.

> Los números de página corresponden a la copia del libro en la bibliografía de la asignatura (`Documentacion Patrones`).
