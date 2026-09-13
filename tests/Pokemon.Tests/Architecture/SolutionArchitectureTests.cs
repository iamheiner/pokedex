using System.Reflection;
using System.Runtime.CompilerServices;
using MediatR;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Pokemon.Domain.Common.Persistence;

namespace Pokemon.Tests.Architecture;

/// <summary>
/// Reglas de arquitectura a nivel de solución: dirección de dependencias entre capas,
/// inmutabilidad del dominio, simetría CQRS, aislamiento entre Features y superficie
/// pública de Infrastructure. Complementan las reglas de persistencia sin depender de
/// bibliotecas adicionales: todo se comprueba por reflexión o leyendo el código fuente.
/// </summary>
public sealed class SolutionArchitectureTests
{
    private static readonly Assembly Domain = typeof(Pokemon.Domain.DamageCalculator).Assembly;
    private static readonly Assembly Application = typeof(Pokemon.Application.Common.Messaging.ICommand<>).Assembly;
    private static readonly Assembly Infrastructure = typeof(Pokemon.Infrastructure.DependencyInjection).Assembly;

    /// <summary>
    /// Comprueba que Application solo conoce Domain, MediatR y abstracciones; nunca ASP.NET, hosting ni almacenamiento.
    /// </summary>
    [Fact]
    public void Application_depends_only_on_domain_mediatr_and_abstractions()
    {
        var references = Application.GetReferencedAssemblies().Select(reference => reference.Name!).ToArray();
        Assert.Contains("Pokemon.Domain", references);
        Assert.DoesNotContain(references, name => name.StartsWith("Microsoft.AspNetCore") || name.StartsWith("Microsoft.Extensions.Hosting")
            || name is "Pokemon.Infrastructure" or "Pokemon.Api" or "Npgsql" or "Dapper");
    }

    /// <summary>
    /// Comprueba que API llega a Infrastructure únicamente desde la raíz de composición.
    /// </summary>
    [Fact]
    public void Api_reaches_infrastructure_only_from_the_composition_root()
    {
        foreach (var file in SourceFiles("Pokemon.Api"))
        {
            if (Path.GetFileName(file) == "Program.cs") continue;
            Assert.DoesNotContain("Pokemon.Infrastructure", File.ReadAllText(file));
        }
    }

    /// <summary>
    /// Comprueba que Infrastructure publica solo su punto de entrada de composición; las implementaciones son internas.
    /// </summary>
    [Fact]
    public void Infrastructure_exposes_only_its_composition_entry_point()
    {
        var visible = Infrastructure.GetExportedTypes().Select(type => type.FullName!).Order().ToArray();
        Assert.Equal(new[] { "Pokemon.Infrastructure.DependencyInjection" }, visible);
    }

    /// <summary>
    /// Comprueba que el estado público del dominio es inmutable: sin setters públicos salvo init en records.
    /// </summary>
    [Fact]
    public void Domain_public_state_is_immutable()
    {
        var mutable = PublicDomainTypes()
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(property => property.SetMethod is { IsPublic: true } setter
                && !setter.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit)))
            .Select(property => $"{property.DeclaringType!.Name}.{property.Name}")
            .ToArray();
        Assert.Empty(mutable);
    }

    /// <summary>
    /// Comprueba que el dominio no expone colecciones mutables: solo interfaces de solo lectura.
    /// </summary>
    [Fact]
    public void Domain_does_not_expose_mutable_collections()
    {
        var exposed = PublicDomainTypes().SelectMany(type =>
                type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Select(p => (Member: p.Name, Type: p.PropertyType))
                .Concat(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                    .Where(m => !m.IsSpecialName).Select(m => (Member: m.Name, Type: Unwrap(m.ReturnType))))
                .Where(member => IsMutableCollection(member.Type))
                .Select(member => $"{type.Name}.{member.Member}: {member.Type.Name}"))
            .ToArray();
        Assert.Empty(exposed);
    }

    /// <summary>
    /// Comprueba que todo Command handler recibe la unidad de trabajo y ningún lector ni repositorio suelto.
    /// </summary>
    [Fact]
    public void Command_handlers_receive_the_unit_of_work_and_no_loose_readers()
    {
        var handlers = Handlers().Where(handler => handler.Name.EndsWith("CommandHandler")).ToArray();
        Assert.NotEmpty(handlers);
        foreach (var handler in handlers)
        {
            var dependencies = Assert.Single(handler.GetConstructors()).GetParameters().Select(parameter => parameter.ParameterType).ToArray();
            Assert.Contains(typeof(IUnitOfWork), dependencies);
            Assert.DoesNotContain(dependencies, type => type == typeof(IReadSession) || type.Name.EndsWith("Reader") || type.Name.EndsWith("Repository"));
        }
    }

    /// <summary>
    /// Comprueba que todo Query handler recibe únicamente la sesión de lectura como acceso a datos.
    /// </summary>
    [Fact]
    public void Query_handlers_receive_only_the_read_session_for_data_access()
    {
        var handlers = Handlers().Where(handler => handler.Name.EndsWith("QueryHandler")).ToArray();
        Assert.NotEmpty(handlers);
        foreach (var handler in handlers)
        {
            var dependencies = Assert.Single(handler.GetConstructors()).GetParameters().Select(parameter => parameter.ParameterType).ToArray();
            Assert.DoesNotContain(dependencies, type => type == typeof(IUnitOfWork) || type.Name.EndsWith("Reader") || type.Name.EndsWith("Repository"));
        }
    }

    /// <summary>
    /// Comprueba que cada handler es sellado, se llama como su solicitud más «Handler» y comparte su espacio de nombres.
    /// </summary>
    [Fact]
    public void Handlers_are_sealed_and_paired_with_their_requests()
    {
        var handlers = Handlers().ToArray();
        Assert.NotEmpty(handlers);
        foreach (var handler in handlers)
        {
            var request = handler.GetInterfaces().Single(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)).GetGenericArguments()[0];
            Assert.True(handler.IsSealed, handler.Name);
            Assert.Equal(request.Name + "Handler", handler.Name);
            Assert.Equal(request.Namespace, handler.Namespace);
        }
    }

    /// <summary>
    /// Comprueba que las Features de Application solo dependen hacia abajo: Battle puede usar Damage; nada más se cruza.
    /// </summary>
    [Fact]
    public void Application_features_only_depend_downward()
    {
        var allowed = new HashSet<(string From, string To)> { ("Battle", "Damage") };
        var violations = Application.GetTypes()
            .Where(type => Feature(type) is not null && !type.IsDefined(typeof(CompilerGeneratedAttribute)))
            .SelectMany(type => ReferencedTypes(type).Select(referenced => (Type: type, Referenced: referenced)))
            .Where(pair => Feature(pair.Referenced) is { } target && target != Feature(pair.Type)! && !allowed.Contains((Feature(pair.Type)!, target)))
            .Select(pair => $"{pair.Type.FullName} -> {pair.Referenced.FullName}")
            .Distinct()
            .ToArray();
        Assert.Empty(violations);
    }

    private static IEnumerable<Type> PublicDomainTypes() =>
        Domain.GetExportedTypes().Where(type => !type.IsInterface && !type.IsEnum && !typeof(Delegate).IsAssignableFrom(type));

    private static IEnumerable<Type> Handlers() => Application.GetTypes().Where(type => type.IsClass && !type.IsAbstract
        && type.GetInterfaces().Any(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)));

    private static string? Feature(Type type)
    {
        const string prefix = "Pokemon.Application.Feature.";
        var ns = type.IsGenericParameter ? null : type.Namespace;
        return ns is not null && ns.StartsWith(prefix) ? ns[prefix.Length..].Split('.')[0] : null;
    }

    private static IEnumerable<Type> ReferencedTypes(Type type)
    {
        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var direct = new List<Type>();
        if (type.BaseType is not null) direct.Add(type.BaseType);
        direct.AddRange(type.GetInterfaces());
        direct.AddRange(type.GetConstructors(all).SelectMany(ctor => ctor.GetParameters().Select(p => p.ParameterType)));
        direct.AddRange(type.GetMethods(all).SelectMany(m => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType)));
        direct.AddRange(type.GetProperties(all).Select(p => p.PropertyType));
        direct.AddRange(type.GetFields(all).Select(f => f.FieldType));
        return direct.SelectMany(Expand);
    }

    private static IEnumerable<Type> Expand(Type type)
    {
        if (type.IsArray || type.IsByRef) return Expand(type.GetElementType()!);
        if (type.IsGenericType) return type.GetGenericArguments().SelectMany(Expand).Prepend(type.GetGenericTypeDefinition());
        return [type];
    }

    private static Type Unwrap(Type type) =>
        type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(Task<>) || type.GetGenericTypeDefinition() == typeof(ValueTask<>))
            ? type.GetGenericArguments()[0] : type;

    private static bool IsMutableCollection(Type type) => type.IsArray || (type.IsGenericType && new[]
    {
        typeof(List<>), typeof(IList<>), typeof(ICollection<>), typeof(HashSet<>), typeof(ISet<>), typeof(Dictionary<,>), typeof(IDictionary<,>)
    }.Contains(type.GetGenericTypeDefinition()));

    private static IEnumerable<string> SourceFiles(string project)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "PokemonTwo.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        return Directory.EnumerateFiles(Path.Combine(root.FullName, "src", project), "*.cs", SearchOption.AllDirectories)
            .Where(file => !file.Split(Path.DirectorySeparatorChar).Any(part => part is "obj" or "bin"));
    }
}

/// <summary>
/// Reglas de arquitectura del contrato HTTP: cada operación vive en su propia clase Endpoint
/// y queda documentada con resumen y etiqueta para OpenAPI.
/// </summary>
public sealed class HttpArchitectureTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    /// <summary>
    /// Comprueba que cada operación HTTP se declara en una clase *Endpoint y publica resumen y etiqueta.
    /// </summary>
    [Fact]
    public void Every_operation_lives_in_an_endpoint_class_with_summary_and_tag()
    {
        // Solo las operaciones declaradas en Pokemon.Api; OpenAPI y Scalar los registra el framework.
        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<MethodInfo>()?.DeclaringType?.Assembly == typeof(Program).Assembly).ToArray();
        Assert.NotEmpty(endpoints);
        foreach (var endpoint in endpoints)
        {
            var route = endpoint.RoutePattern.RawText!;
            var declaring = endpoint.Metadata.GetMetadata<MethodInfo>()!.DeclaringType!;
            Assert.True(declaring.Name.EndsWith("Endpoint"), $"{route} is handled by {declaring.FullName}");
            Assert.True(endpoint.Metadata.GetMetadata<IEndpointSummaryMetadata>() is not null, $"{route} has no summary");
            if (!route.StartsWith("/health"))
                Assert.True(endpoint.Metadata.GetMetadata<ITagsMetadata>()?.Tags.Count > 0, $"{route} has no tag");
        }
    }
}
