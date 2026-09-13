# Git Flow

Se mantiene una solución para los tres ejercicios. Las carpetas Feature separan las funcionalidades y las ramas separan el trabajo en curso.

| Rama | Nace de | Se integra en | Uso |
| --- | --- | --- | --- |
| main | — | — | Entregas verificadas |
| develop | main | Mediante release | Integración del siguiente avance |
| feature/* | develop | develop | Funcionalidades y sus pruebas |
| release/* | develop | main y develop | Estabilización de una entrega |
| hotfix/* | main | main y develop | Corrección urgente de una entrega |

El ejercicio 1, verificado con 452 pruebas en Windows y Docker, constituye el commit inicial de main. develop parte del mismo estado. Las siguientes ramas se crean cuando comienza su trabajo.

Para comenzar el ejercicio 2:

```powershell
git switch develop
git switch -c feature/pokedex
# Implementar y verificar; guardar cada comportamiento junto a sus pruebas y documentación.
git add <archivos-del-cambio>
git commit -m "feat(pokedex): add Pokemon catalog queries"
git switch develop
git merge --no-ff feature/pokedex
```

El ejercicio 3 seguirá el mismo proceso con feature/battle. Si una funcionalidad crece, se divide en ramas de menor alcance.

Para preparar la entrega final, cuando los tres ejercicios estén completos:

```powershell
git switch develop
git switch -c release/1.0.0
dotnet restore PokemonTwo.slnx --locked-mode
dotnet test PokemonTwo.slnx -c Release --no-restore
docker compose build
# Corregir defectos y revisar documentación y ejemplos antes de integrar.
git switch main
git merge --no-ff release/1.0.0
git tag -a v1.0.0 -m "Entrega de los tres ejercicios"
git switch develop
git merge --no-ff release/1.0.0
```

Los comandos anteriores son una guía; no se ha creado una release ni una etiqueta de entrega. Un hotfix parte de main, se verifica y se integra en main y develop con una nueva versión de parche.

Se usa Git estándar; no hace falta instalar una extensión Git Flow. Cuando exista un remoto, se integrará preferentemente mediante pull requests con pruebas obligatorias y main como rama principal. Las protecciones del remoto todavía no están configuradas.

La integración de seguridad se desarrolla en `feature/keycloak`, basada en `feature/battle` porque utiliza las partidas persistentes. Para integrar estas ramas: primero `feature/battle` en `develop` y después `feature/keycloak`, ambas con `--no-ff`. Todavía no se han integrado ni creado una release.
