FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY global.json Directory.Build.props PokemonTwo.slnx ./
COPY src/ src/
COPY tests/ tests/
RUN dotnet restore PokemonTwo.slnx --locked-mode
RUN dotnet publish src/Pokemon.Api -c Release --no-restore -o /app /p:UseAppHost=false

# Las pruebas no forman parte de la construcción de la imagen: se ejecutan en CI
# (dotnet test) y, para PostgreSQL real, mediante `docker compose run --rm postgres-tests`.
FROM build AS postgres-tests
RUN dotnet build tests/Pokemon.Tests -c Release --no-restore
ENTRYPOINT ["dotnet", "test", "tests/Pokemon.Tests", "-c", "Release", "--no-build", "--no-restore", "--filter", "Category=Postgres"]

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Pokemon.Api.dll"]
