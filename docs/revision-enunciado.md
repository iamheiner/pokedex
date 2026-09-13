# Revisión de adecuación al enunciado

Estado: revisión inicial conservada como histórico; consultar el cierre al final.

Fecha: 13 de septiembre de 2026. Revisión del código actual, el PDF original y las aclaraciones del usuario. Alcance de implementación acordado: ejercicio 1. No se modificó lógica durante esta revisión.

## Dictamen

El ejercicio 1 cubre el cálculo básico, pero todavía requiere correcciones y mejores pruebas para una entrega sólida. La prueba completa no está terminada: los ejercicios 2 y 3 siguen pendientes por la delimitación acordada. Las herramientas incorporadas no sustituyen la calidad del modelo ni las evidencias de corrección.

## Cobertura de requisitos

| Requisito | Estado | Evidencia / límite |
|---|---|---|
| Método con atacante, movimiento y rival | Cubierto | DamageCalculator.Calculate; fachada CQRS y POST /damage. |
| Atributos del Pokémon y máximo cuatro movimientos | Cubierto en el modelo actual | Combatant conserva todos los atributos y protege la colección. |
| Tipo del movimiento frente a tipo del defensor | Cubierto en cálculo; fallo en entrada HTTP | Against(move.Type, defender.Type), pero omitir tipos se acepta como Normal. |
| Fórmula y factor 85–100 | Cubierto en implementación | Decimal, floor final y proveedor aleatorio inyectable. |
| Tabla de efectividad | Implementada; comprobación parcial | 18 tipos, 324 cruces representados; falta validación independiente completa. |
| Aproximadamente 5–6 Pokémon demostrativos | Pendiente | Solo un ejemplo HTTP con dos Pokémon simplificados. |
| CRUD de especies/base, movimientos y ejemplares | Pendiente, ejercicio 2 | No hay recursos ni repositorios; no es un fallo del alcance actual. |
| Movimientos aprendidos y posibles por nivel | Pendiente, ejercicio 2 | Moves representa aprendidos, no existe relación de aprendizaje. |
| Consulta inversa por movimiento | Pendiente, ejercicio 2 | Sin modelo ni consulta. |
| Partida con dos adversarios y estado hasta salud cero | Pendiente, ejercicio 3 | Calculadora sin mutaciones ni turnos, conforme al alcance actual. |
| Backend ejecutable y documentado | Parcialmente cubierto | Docker, Scalar y README; faltan pulir entrega y automatizar comprobaciones HTTP. |

## Hallazgos que corregir antes de cerrar el ejercicio 1

### R1. Se aceptan tipos omitidos y se altera el resultado silenciosamente — prioridad alta

Ubicación: src/Pokemon.Api/Feature/Damage/Contracts/CombatantRequest.cs:7, MoveRequest.cs y Program.cs.

En el contrato actual, la deserialización admite parámetros de constructor omitidos y asigna el valor cero del enum, Normal. Se reprodujo omitiendo defender.type o attacker.moves[0].type: ambos responden HTTP 200 y efectividad 1, en lugar de exigir el dato. El ejemplo completo tiene efectividad 2. Omitir currentHealth también se acepta como cero.

Acción: exigir presencia de campos obligatorios al deserializar y probarlo a través de HTTP. Distinguir presencia de valor de validación de rango: cero puede ser salud válida cuando se envía explícitamente.

### R2. Las pruebas no demuestran suficientemente la fórmula ni la tabla — prioridad alta

Ubicación: tests/Pokemon.Tests/DamageTests.cs:9.

Los casos numéricos usan nivel 50, ataque 100, defensa 100 y poder 100. Intercambiar ataque y defensa no cambiaría esos resultados. Solo cuatro cruces de tipos se verifican numéricamente: Fire/Grass, Fire/Water, Fire/Normal y Electric/Ground. Los tests de CQRS no amplían esa cobertura de tipos.

Acción: usar estadísticas asimétricas, varios niveles/poderes, valores fraccionarios y extremos; verificar el método de dominio con un movimiento no aprendido y valores límite. Añadir una matriz esperada independiente de la implementación para los 324 cruces. No generar las expectativas desde TypeEffectiveness.

### R3. El contrato de errores HTTP no es uniforme — prioridad media

Ubicación: src/Pokemon.Api/Feature/Damage/DamageEndpoints.cs:19 y Program.cs.

Se documenta ProblemDetails para HTTP 400, pero JSON mal formado y tipo desconocido devuelven HTTP 400 con cuerpo vacío. Estos errores ocurren antes de entrar en el endpoint. Además, capturar cualquier ArgumentException como error del cliente también clasificaría un factor aleatorio inválido del proveedor interno como 400.

Acción: homogeneizar errores de binding y validación, distinguir errores del cliente de fallos internos e incluir pruebas del contrato HTTP completo. El consumidor debe poder identificar qué dato corregir.

### R4. La tabla depende del orden numérico del enum — prioridad media

Ubicación: src/Pokemon.Domain/TypeEffectiveness.cs:6 y :38.

Las filas no indican explícitamente el tipo atacante; su posición corresponde al enum. Reordenar el enum puede alterar asociaciones silenciosamente. Se usan cadenas convertidas en enums y dimensiones fijas 18×18.

Acción: declarar asociaciones explícitas por PokemonType, verificar completitud y mantener una prueba independiente de la tabla. No se ha encontrado ni demostrado un cruce incorrecto actual; el hallazgo es fragilidad de mantenimiento.

## Lo que insinúa el enunciado sobre DDD y evolución

Se necesitan conceptos distintos: especie/base (datos compartidos), ejemplar (identidad, nivel, salud, movimientos aprendidos), definición de movimiento (catálogo) y aprendizaje (especie, movimiento, nivel). El tipo ayuda a calcular efectividad; no basta para deducir todos los movimientos que una especie puede aprender.

Combatant es útil para la calculadora. No debería convertirse automáticamente en el único modelo de todos los recursos futuros. Move como objeto valor es defendible para una instantánea de cálculo, pero el CRUD del catálogo requerirá identidad estable o un modelo de definición separado. Debe decidirse qué ocurre con los ejemplares cuando se edita una definición de movimiento. Esto es diseño pendiente para ejercicio 2, no una obligación de introducir tablas ahora.

El constructor de Combatant recibe muchos enteros consecutivos del mismo tipo. Facilita intercambiar ataque, defensa o salud por accidente. Objetos valor de estadísticas/salud y nombres de argumentos pueden reducir ese riesgo; añadir una clase por propiedad sin una regla que proteger no aporta automáticamente calidad.

La futura partida debe controlar turnos, salud, finalización y acciones permitidas, con pruebas. No corresponde descontar salud dentro del cálculo actual. Debe resolverse también qué hacer si un enfrentamiento no puede progresar (por ejemplo, movimientos mutuamente inmunes); el requisito de terminar merece una decisión explícita.

Las carpetas Feature y MediatR organizan responsabilidades, pero no constituyen por sí mismas DDD. La calidad del modelo se demuestra con límites, invariantes y comportamiento coherentes.

## Decisiones aceptables que deben explicarse

- Un solo tipo, estadísticas normales y sin mecánicas avanzadas: simplificaciones acordadas y documentadas. El enunciado deja fórmula/modelos a criterio del candidato. No es obligatorio reproducir el juego oficial.
- Estadísticas especiales: su presencia sugiere un posible uso y será una pregunta razonable en entrevista. Debe defenderse su exclusión actual o añadirse categoría física/especial de forma explícita; no se trata como requisito confirmado.
- Límites de poder 250 y estadísticas 10000: restricciones propias documentadas; conviene justificarlas con el alcance elegido y no presentarlas como reglas del enunciado.
- CQRS: cálculo clasificado como Query por no mutar estado; HTTP POST no implica necesariamente Command.
- PostgreSQL y Valkey: no hacen falta para esta calculadora. La escalabilidad valorada también consiste en no introducir servicios sin necesidad.
- Aspire: útil para diagnóstico; el dashboard local no es almacenamiento duradero de telemetría.

## Presentación y documentación

Conservar comentarios pedagógicos en español, solicitados por el usuario. Para entregar, separar el material de aprendizaje de un README conciso orientado al evaluador: requisitos, arranque, ejemplo, decisiones, pruebas y pendientes. Actualmente el README acumula cifras de 11 y 15 pruebas y notas cronológicas de puertos/sesiones; aunque no son falsas, dificultan localizar el estado actual.

No hay repositorio Git ni automatización CI en esta carpeta. No son requisitos expresos, pero una entrega profesional se beneficiaría de instrucciones reproducibles y una comprobación automatizada de compilación/pruebas. El catálogo de ejemplos y una guía breve de decisiones defendibles tienen más prioridad que incorporar más herramientas.

## Verificación realizada

- dotnet test PokemonTwo.slnx -c Release --no-restore: 15 correctas, 0 fallidas.
- Se arrancó el ensamblado recién compilado en 127.0.0.1:52071 para comprobar el código revisado, sin depender de la imagen Docker anterior.
- Ejemplo válido: HTTP 200, efectividad 2.
- Sin defender.type: HTTP 200, efectividad 1.
- Sin tipo del movimiento: HTTP 200, efectividad 1.
- Sin attacker.currentHealth: HTTP 200.
- Tipo desconocido y JSON mal formado: HTTP 400, cuerpo vacío.
- Revisión estática de dominio, contratos, handler, pruebas, observabilidad, Docker y README. No se realizó una auditoría de seguridad completa ni un cotejo independiente de las 324 celdas.

## Orden propuesto

1. Corregir presencia de datos y contrato de errores HTTP con tests de regresión.
2. Completar pruebas de fórmula y tabla y hacer explícitas las claves de tipos.
3. Preparar 5–6 ejemplos coherentes y documentar decisiones de dominio.
4. Consolidar README de entrega y explicación para entrevista.
5. Antes del ejercicio 2, definir especies, ejemplares, catálogo y aprendizaje; después elegir persistencia.

## Cierre de correcciones — ejercicio 1

- R1 resuelto: constructor JSON obligatorio, referencias no anulables, tests de omisión de cada campo; Normal y salud cero solo se aceptan enviados explícitamente.
- R2 resuelto: matriz independiente de 324 cruces, casos asimétricos y límites de fórmula, validaciones del modelo y pruebas HTTP completas. Suite final: 452 casos.
- R3 resuelto: handler central de excepciones, ProblemDetails para binding y validación, traceId y distinción de error interno 500, probado sustituyendo el proveedor aleatorio.
- R4 resuelto: filas tipadas con claves PokemonType explícitas y validación de completitud y contradicciones, sin índices numéricos implícitos ni Enum.Parse sobre cadenas productivas.
- Seis ejemplares y seis escenarios verificados por HTTP, compartidos con Scalar. Fuentes y simplificaciones en ejemplos.md.
- README consolidado y decisiones separadas, argumentos nombrados al construir Combatant, lockfiles y verificación CI preparada.
- Verificación de entrega: 452 pruebas superadas en Windows y durante la construcción Linux de Docker con restauración bloqueada. Imagen final arrancada: seis escenarios HTTP correctos, errores 400 con traceId para campos omitidos y JSON mal formado, Scalar y Aspire accesibles (HTTP 200). El workflow remoto de GitHub todavía no se ha ejecutado.

Se mantiene el alcance en ejercicio 1. Los conceptos futuros de Pokédex/partida están documentados, no implementados. No se declara perfección absoluta ni cobertura de la prueba técnica completa. Este cierre sustituye el dictamen inicial para los cuatro hallazgos del ejercicio 1; el contenido anterior explica por qué se hicieron los cambios.
