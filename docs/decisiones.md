# Decisiones del ejercicio 1

## Modelo y alcance

Combatant representa un ejemplar concreto con identidad y movimientos aprendidos. Protege salud, nivel, estadísticas y máximo cuatro movimientos. Move es un objeto valor inmutable: nombre, tipo y poder determinan su igualdad. DamageCalculator es un servicio de dominio porque combina dos ejemplares y un movimiento sin pertenecer exclusivamente a uno de ellos.

El cálculo no modifica salud ni comprueba turnos: calcula daño teórico, incluso si la salud es cero. Resolver quién puede actuar y descontar salud corresponde a una partida del ejercicio 3. El ejercicio 2 se documenta en [Pokédex](pokedex.md); el ejercicio 3 sigue pendiente.

Se utiliza un único tipo por Pokémon y siempre Attack/Defense base. SpecialAttack/SpecialDefense se conservan por el enunciado, pero no participan. Introducir categoría física/especial sería una ampliación explícita; no se infiere solo por el tipo. No se añaden STAB, críticos, precisión, doble tipo ni efectos secundarios. Estos límites son deliberados y deben explicarse en entrevista.

El nivel se limita a 1–100; estadísticas a 1–10000 y poder a 1–250. Los dos últimos límites son decisiones del contrato para una calculadora acotada, no límites oficiales del juego. Salud actual admite 0–salud total. Se admiten cero movimientos para calcular daño recibido; no más de cuatro ni nombres repetidos ignorando mayúsculas. Los espacios dentro de nombres se conservan y no se normalizan silenciosamente.

## Aritmética y efectividad

Se sigue el PDF: ((2 × nivel / 5 + 2) × ataque × poder / defensa / 50) × efectividad × aleatorio / 100. Se usa decimal para evitar división entera prematura y se redondea hacia abajo solo al final. No se añade +2 después de dividir por 50 ni daño mínimo: una inmunidad sigue devolviendo cero. La defensa positiva evita dividir por cero; los límites actuales mantienen el resultado dentro de Int32.

El tipo del movimiento se compara con el del defensor. La tabla usa claves enum explícitas y comprueba que ninguna fila falte ni tenga reglas contradictorias. Los 324 cruces se contrastan con una matriz CSV transcrita del PDF, que no se deriva del código productivo.

## CQRS y fronteras

La operación es una Query porque no cambia estado de negocio, aunque use aleatoriedad y viaje mediante HTTP POST. La API convierte DTOs al dominio; ISender resuelve el handler; el handler coordina y delega la fórmula. IDamageRandom permite fijar el factor en pruebas. No hay Task.Run ni I/O artificial para una operación puramente de CPU.

Se mantiene MediatR exactamente en 12.5.0 por su licencia Apache 2.0. CQRS no exige una base de datos ni Commands ficticios. Feature separa funcionalidades; las futuras entidades no deben acumularse en Damage.

Los parámetros JSON de constructor son obligatorios y los valores no anulables se respetan. Omitir type no equivale a enviar Normal; omitir salud no equivale a enviar cero. Los nombres de propiedades desconocidas se rechazan para detectar erratas. Los errores esperados de entrada tienen una excepción específica; un fallo del proveedor aleatorio es interno y devuelve 500. El error HTTP contiene ProblemDetails y traceId para buscarlo en Aspire.

## Evolución hacia ejercicios 2 y 3

La separación prevista al cerrar el ejercicio 1 fue:

- Especie/base: datos compartidos de una especie.
- Ejemplar: identidad, nivel, salud y movimientos aprendidos.
- Definición de movimiento: identidad estable para el CRUD del catálogo. Move puede seguir siendo una instantánea para calcular.
- Aprendizaje: relación especie–movimiento–nivel; no se deduce únicamente del tipo.
- Partida: adversarios, turno/fase, estado y finalización.

Hay que decidir qué ocurre con ejemplares existentes al editar un movimiento del catálogo, y cómo terminar un combate sin progreso por inmunidades. En el ejercicio 1 no se anticiparon repositorios vacíos. El ejercicio 2 añade el puerto transaccional y el almacén en memoria descritos en pokedex.md. PostgreSQL será la opción si se necesita almacenamiento; Valkey (interpretación de «vaultkey») solo si un caso de uso justifica caché. El resultado aleatorio no se cachea.

## Operación y entrega

Scalar permite probar los seis ejemplos; OpenTelemetry registra HTTP, handler, logs y métricas. El comportamiento de MediatR es transversal para nuevos handlers. Aspire standalone en Docker es diagnóstico local con memoria volátil y acceso limitado a loopback; no es un almacén de observabilidad para producción.

La calidad se valida con pruebas de dominio, matriz independiente, integración real HTTP/DI/MediatR y construcción Docker. Las pruebas no requieren acceso a Pokémon Database en ejecución. No se afirma ausencia absoluta de defectos: las garantías corresponden al alcance y los casos comprobados.
