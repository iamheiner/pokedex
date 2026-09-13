using Pokemon.Domain.Battle.Repositories;
using Pokemon.Domain.Pokedex.Repositories;
using Pokemon.Infrastructure.Battle.Postgres;

namespace Pokemon.Tests.Architecture;

public sealed class PersistenceArchitectureTests
{
    [Fact]
    public void Domain_has_no_outward_dependencies()
    {
        var references = typeof(IBattleRepository).Assembly.GetReferencedAssemblies().Select(reference => reference.Name!);
        Assert.DoesNotContain(references, name => name.StartsWith("Pokemon.") || name.StartsWith("Npgsql") || name.StartsWith("Microsoft.Extensions"));
    }

    [Fact]
    public void Infrastructure_depends_on_domain_and_not_application_or_api()
    {
        var references = typeof(PostgresBattleRepository).Assembly.GetReferencedAssemblies().Select(reference => reference.Name!).ToArray();
        Assert.Contains("Pokemon.Domain", references);
        Assert.DoesNotContain("Pokemon.Application", references);
        Assert.DoesNotContain("Pokemon.Api", references);
    }

    [Fact]
    public void Repository_contracts_and_unit_of_work_belong_to_domain()
    {
        foreach (var contract in new[] { typeof(IBattleRepository), typeof(ISpeciesRepository), typeof(IMoveRepository),
                     typeof(IOwnedPokemonRepository), typeof(IPokedexUnitOfWork) })
        {
            Assert.True(contract.IsInterface);
            Assert.Equal("Pokemon.Domain", contract.Assembly.GetName().Name);
        }
    }

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
}
