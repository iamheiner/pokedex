# Recorrido de la prueba técnica

Los ejemplos HTTP requieren un token Bearer de Keycloak. Antes de ejecutar los scripts, configura `POKEMON_CLIENT_SECRET` o `POKEMON_ACCESS_TOKEN` como explica [autenticación](autenticacion.md). Scalar dispone de login interactivo.

El backend reúne los tres ejercicios en una solución y un arranque Docker. Scalar permite inspeccionar y ejecutar el contrato; Aspire muestra las trazas y logs. No hay frontend de juego.

| Ejercicio | Implementación | Evidencia y explicación |
| --- | --- | --- |
| 1. Cálculo de daño | POST /damage, fórmula y tipos en Domain | Matriz independiente de 324 cruces; [decisiones](decisiones.md) y [ejemplos](ejemplos.md) |
| 2. Pokédex | Tres CRUD y consultas de aprendidos, posibles e inversas | [Pokédex](pokedex.md), scripts/verify-pokedex.ps1 |
| 3. Combate | Creación, consulta y resolución de acciones con estado | [Combate](combate.md), scripts/verify-battle.ps1 |

Para revisar la entrega:

1. Ejecutar `docker compose up --build -d` siguiendo los puertos del README. La construcción ejecuta las pruebas antes de publicar la API.
2. Abrir Scalar y probar uno de los seis ejemplos de Damage.
3. Consultar la Pokédex: distinguir los cuatro movimientos aprendidos del plan completo con niveles futuros. El catálogo inicial contiene cinco especies, cinco ejemplares y 21 movimientos.
4. Ejecutar el script de Pokédex para recorrer sus operaciones con datos temporales.
5. Ejecutar el script de combate y consultar después la partida por su identificador. Comparar salud inicial y final y comprobar que la colección no ha cambiado.
6. Abrir Aspire y localizar CreateBattleCommand, GetBattleQuery y PlayTurnCommand dentro de sus peticiones HTTP.

Las partidas usan PostgreSQL para conservar el estado tras reiniciar; la Pokédex mantiene memoria, permitida por el enunciado. Las diferencias y garantías se explican en [persistencia](persistencia.md). No se necesita caché. El diseño separa dominio, aplicación, transporte e infraestructura; el dominio no depende de ASP.NET, MediatR ni almacenamiento.

Para la entrevista, conviene poder explicar por qué un movimiento del catálogo tiene identidad mientras Move del cálculo es un objeto valor; por qué aprendizaje no equivale a coincidir en tipo; por qué la partida usa snapshots; cómo se evitan dos ataques concurrentes; y cómo se termina un combate con inmunidades. Las decisiones de simplificación son parte de la solución y están descritas en los documentos de cada ejercicio.

El desarrollo sigue Git Flow. main conserva la entrega inicial; Pokédex está integrada en develop y el combate se prepara en feature/battle. La integración del combate y la release final se realizarán tras revisar esta funcionalidad. No se ha publicado en un remoto ni ejecutado el workflow de GitHub allí.

La suite conjunta contiene 562 pruebas sin base de datos superadas en Windows y Docker Linux, más 8 pruebas superadas contra PostgreSQL real. Los recorridos HTTP de Pokédex y combate se han ejecutado contra sus imágenes. El detalle de los escenarios y límites de cada verificación está en los documentos de cada ejercicio.
