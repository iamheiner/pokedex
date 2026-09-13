# Datos esperados de efectividad

`type-chart.csv` es una transcripción independiente de la tabla de la página 1 de `docs/POKÉMON 2.pdf`. Filas y columnas siguen el orden del PDF (Acero, Agua, Bicho...), traducido a los nombres del enum. Los guiones del PDF se convierten en 1; ½ en 0.5. No se genera ni se actualiza desde TypeEffectiveness.

Cada cambio debe cotejarse con la fuente y conservar las 18 filas, 18 columnas y 324 pares únicos. La tabla de https://pokemondb.net/type es una referencia adicional, no una dependencia en ejecución.
