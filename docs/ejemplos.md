# Seis Pokémon de ejemplo

Antes de probar los ejemplos, ejecuta `. ./scripts/initialize.ps1` desde la raíz del proyecto, con el puerto que utilices. El [arranque del README](../README.md#inicializar-el-proyecto-y-empezar-a-probar) prepara los servicios y la autenticación de esa terminal. Scalar dispone de login interactivo.

La fuente ejecutable está en `src/Pokemon.Api/Feature/Damage/Examples/damage-requests.json`. Scalar muestra seis escenarios y los tests envían todos por HTTP con factor 100. Los ejemplares tienen nivel 50 y salud total simplificada igual al HP base; no se calculan estadísticas oficiales por nivel, IV o EV. Solo se usan las formas normales, de un único tipo. El ejemplo no simula ni valida reglas de evolución.

| Ejemplar | Tipo | HP | Ataque | Defensa | At. especial | Def. especial | Velocidad |
|---|---|---:|---:|---:|---:|---:|---:|
| Charmander | Fire | 39 | 52 | 43 | 60 | 50 | 65 |
| Squirtle | Water | 44 | 48 | 65 | 50 | 64 | 43 |
| Pikachu | Electric | 35 | 55 | 40 | 50 | 50 | 90 |
| Sandshrew | Ground | 50 | 75 | 85 | 20 | 30 | 40 |
| Eevee | Normal | 55 | 55 | 50 | 45 | 65 | 55 |
| Machop | Fighting | 70 | 80 | 50 | 35 | 35 | 35 |

| Escenario | Ataque → defensor | Efectividad | Daño con factor 100 |
|---|---|---:|---:|
| weakness | Squirtle, Water Gun → Charmander | 2 | 39 |
| resistance | Charmander, Ember → Squirtle | 0.5 | 7 |
| immunity | Pikachu, Thunder Shock → Sandshrew | 0 | 0 |
| neutral | Eevee, Tackle → Machop | 1 | 19 |
| fighting | Machop, Low Sweep → Eevee | 2 | 91 |
| ground | Sandshrew, Sand Tomb → Pikachu | 2 | 57 |

Fuentes consultadas el 13/09/2026: [Charmander](https://pokemondb.net/pokedex/charmander), [Squirtle](https://pokemondb.net/pokedex/squirtle), [Pikachu](https://pokemondb.net/pokedex/pikachu), [Sandshrew](https://pokemondb.net/pokedex/sandshrew), [Eevee](https://pokemondb.net/pokedex/eevee), [Machop](https://pokemondb.net/pokedex/machop) y [movimientos](https://pokemondb.net/move/all). Los movimientos seleccionados son ejemplos conocidos por esas especies; no se ofrece un catálogo completo de aprendizaje por nivel en el ejercicio 1. Los efectos secundarios y categorías especiales no se aplican en esta fórmula simplificada.
