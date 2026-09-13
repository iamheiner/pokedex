using Pokemon.Domain.Common.Persistence;
using Pokemon.Domain.Battle.Repositories;
using Pokemon.Domain.Pokedex.Repositories;
using Pokemon.Infrastructure.Persistence;
using Pokemon.Infrastructure.Persistence.Repositories;
using Pokemon.Infrastructure.Persistence.Migrations;
using Pokemon.Infrastructure.Persistence.Serialization;

namespace Pokemon.Tests.Architecture;

public sealed class PersistenceArchitectureTests
{
    /// <summary>
    /// Comprueba que Infrastructure no publica implementaciones de persistencia en memoria.
    /// </summary>
    [Fact]
    public void Infrastructure_does_not_ship_in_memory_persistence()
    {
        Assert.DoesNotContain(typeof(BattleRepository).Assembly.GetTypes(),
            type => type.Name.StartsWith("InMemory", StringComparison.Ordinal));
    }

    /// <summary>
    /// Comprueba que Domain no depende de capas exteriores ni bibliotecas de infraestructura.
    /// </summary>
    [Fact]
    public void Domain_has_no_outward_dependencies()
    {
        var references = typeof(IBattleRepository).Assembly.GetReferencedAssemblies().Select(reference => reference.Name!);
        Assert.DoesNotContain(references, name => name.StartsWith("Pokemon.") || name.StartsWith("Npgsql") || name.StartsWith("Dapper") || name.StartsWith("Microsoft.Extensions"));
    }

    /// <summary>
    /// Comprueba que Infrastructure depende de Domain sin referenciar Application ni API.
    /// </summary>
    [Fact]
    public void Infrastructure_depends_on_domain_and_not_application_or_api()
    {
        var references = typeof(BattleRepository).Assembly.GetReferencedAssemblies().Select(reference => reference.Name!).ToArray();
        Assert.Contains("Pokemon.Domain", references);
        Assert.DoesNotContain("Pokemon.Application", references);
        Assert.DoesNotContain("Pokemon.Api", references);
    }

    /// <summary>
    /// Comprueba que los contratos de repositorio y unidad de trabajo pertenecen a Domain.
    /// </summary>
    [Fact]
    public void Repository_contracts_and_unit_of_work_belong_to_domain()
    {
        foreach (var contract in new[] { typeof(IBattleRepository), typeof(ISpeciesRepository), typeof(IMoveRepository),
                     typeof(IOwnedPokemonRepository), typeof(IUnitOfWork) })
        {
            Assert.True(contract.IsInterface);
            Assert.Equal("Pokemon.Domain", contract.Assembly.GetName().Name);
        }
    }

    /// <summary>
    /// Comprueba que API, Application y Domain no ejecutan comandos de base de datos directamente.
    /// </summary>
    [Fact]
    public void Api_and_application_do_not_execute_database_commands()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "PokemonTwo.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        foreach (var project in new[] { "Pokemon.Api", "Pokemon.Application", "Pokemon.Domain" })
            foreach (var file in Directory.EnumerateFiles(Path.Combine(root.FullName, "src", project), "*.cs", SearchOption.AllDirectories))
            {
                if (file.Split(Path.DirectorySeparatorChar).Any(part => part is "obj" or "bin")) continue;
                Assert.False(System.Text.RegularExpressions.Regex.IsMatch(File.ReadAllText(file),
                    @"(NpgsqlCommand|NpgsqlDataSource|DbCommand|DbConnection)"), file);
            }
    }
    /// <summary>
    /// Comprueba que Application no referencia implementaciones ni bibliotecas de base de datos.
    /// </summary>
    [Fact]
    public void Application_cannot_reference_database_implementations()
    {
        var references = typeof(Pokemon.Application.Feature.Pokedex.Queries.ListMoves.ListMovesQuery).Assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(references, value => value.Name is "Pokemon.Infrastructure" or "Npgsql" or "Dapper");
    }

    /// <summary>
    /// Comprueba que los handlers de consultas de Pokédex reciben únicamente el puerto de lectura.
    /// </summary>
    [Fact]
    public void Pokedex_query_handlers_only_receive_read_ports()
    {
        var assembly = typeof(Pokemon.Application.Feature.Pokedex.Queries.ListMoves.ListMovesQuery).Assembly;
        var handlers = assembly.GetTypes().Where(type => type.Namespace?.Contains("Pokedex.Queries") == true && type.Name.EndsWith("Handler"));
        Assert.NotEmpty(handlers);
        foreach (var handler in handlers)
        {
            var dependencies = Assert.Single(handler.GetConstructors()).GetParameters().Select(parameter => parameter.ParameterType).ToArray();
            Assert.Contains(typeof(IReadSession), dependencies);
            Assert.DoesNotContain(dependencies, type => type == typeof(IUnitOfWork) || type.Name.EndsWith("Repository"));
        }
        foreach (var reader in new[] { typeof(IMoveReader), typeof(ISpeciesReader), typeof(IOwnedPokemonReader), typeof(IReadSession) })
            Assert.DoesNotContain(reader.GetMethods(), method => method.Name.StartsWith("Save") || method.Name.StartsWith("Delete") || method.Name.StartsWith("Write"));
    }

    /// <summary>
    /// Comprueba que toda solicitud de MediatR declara explícitamente si es Command o Query.
    /// </summary>
    [Fact]
    public void Every_mediator_request_explicitly_declares_command_or_query()
    {
        var requests = typeof(Pokemon.Application.Feature.Pokedex.Queries.ListMoves.ListMovesQuery).Assembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && type.GetInterfaces().Any(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(MediatR.IRequest<>)));
        Assert.NotEmpty(requests);
        foreach (var request in requests)
        {
            var markers = request.GetInterfaces().Where(contract => contract.IsGenericType &&
                (contract.GetGenericTypeDefinition() == typeof(Pokemon.Application.Common.Messaging.ICommand<>) ||
                 contract.GetGenericTypeDefinition() == typeof(Pokemon.Application.Common.Messaging.IQuery<>)));
            Assert.Single(markers);
        }
    }
    /// <summary>
    /// Comprueba que la consulta de partidas no recibe capacidades de escritura.
    /// </summary>
    [Fact]
    public void Battle_query_handler_does_not_receive_write_capabilities()
    {
        var handler = typeof(Pokemon.Application.Feature.Battle.Queries.GetBattle.GetBattleQueryHandler);
        Assert.Equal(typeof(IReadSession), Assert.Single(Assert.Single(handler.GetConstructors()).GetParameters()).ParameterType);
        Assert.Single(typeof(IBattleReader).GetMethods());
    }

    /// <summary>
    /// Comprueba que los contratos de salida no reutilizan tipos de entrada.
    /// </summary>
    [Fact]
    public void Output_contracts_do_not_reuse_input_contracts()
    {
        var assembly = typeof(Pokemon.Application.Feature.Pokedex.Contracts.PokemonView).Assembly;
        foreach (var view in assembly.GetTypes().Where(type => type.Name.EndsWith("View")))
            Assert.DoesNotContain(view.GetProperties(), property => property.PropertyType.Name.EndsWith("Input"));
    }
    /// <summary>
    /// Comprueba que existe una unidad de trabajo común sin propiedades específicas de agregados.
    /// </summary>
    [Fact]
    public void Unit_of_work_is_shared_and_has_no_aggregate_properties()
    {
        var units = typeof(IUnitOfWork).Assembly.GetTypes().Where(type => type.Name.EndsWith("UnitOfWork"));
        Assert.Equal(typeof(IUnitOfWork), Assert.Single(units));
        Assert.Equal("Pokemon.Domain.Common.Persistence", typeof(IUnitOfWork).Namespace);
        Assert.Empty(typeof(IRepositoryScope).GetProperties());
        Assert.False(typeof(IReadRepository).IsAssignableFrom(typeof(IMoveRepository)));
        Assert.False(typeof(IReadRepository).IsAssignableFrom(typeof(IBattleRepository)));
    }

    /// <summary>
    /// Comprueba que las excepciones de dominio están agrupadas en espacios de nombres Exceptions.
    /// </summary>
    [Fact]
    public void Domain_exceptions_are_organized_in_exception_namespaces()
    {
        var exceptions = typeof(IUnitOfWork).Assembly.GetTypes().Where(type => type.IsSubclassOf(typeof(Exception)));
        Assert.NotEmpty(exceptions);
        Assert.All(exceptions, type => Assert.EndsWith(".Exceptions", type.Namespace));
    }
}
