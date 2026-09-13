using Pokemon.Api.Feature.Battle;
using Pokemon.Domain.Battle;
using Pokemon.Application.Feature.Pokedex.Persistence;
using Pokemon.Infrastructure.Pokedex;
using Pokemon.Api.Feature.Pokedex.Moves;
using Pokemon.Api.Feature.Pokedex.Species;
using Pokemon.Api.Feature.Pokedex.Pokemon;
using System.Diagnostics;
using Pokemon.Api.Errors;
using Scalar.AspNetCore;
using Pokemon.Api.Observability;
using System.Text.Json.Serialization;
using Pokemon.Application;
using Pokemon.Api.Feature.Damage;
using Pokemon.Api.Feature.Health;
using Pokemon.Domain;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    // Ausencia de un campo no equivale a su valor cero (Normal o salud cero).
    options.SerializerOptions.RespectRequiredConstructorParameters = true;
    options.SerializerOptions.RespectNullableAnnotations = true;
    options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter<PokemonType>(allowIntegerValues: false));
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter<BattlePhase>(allowIntegerValues: false));
});
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
builder.Services.AddApplication();
builder.Services.AddBattlePersistence(builder.Configuration);
builder.Services.AddSingleton<IPokedexStore>(_ => new InMemoryPokedexStore());
builder.Services.AddDamageFeature();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier);
builder.Services.AddOpenApi();
builder.AddObservability();

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
// Scalar permite consultar y probar el contrato OpenAPI desde el navegador.
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("ApiDocumentation:Enabled"))
{
    app.MapOpenApi();
    app.MapScalarApiReference(options => options.WithTitle("Pokémon API"));
}
app.MapHealthEndpoints();
app.MapDamageEndpoints();
app.MapBattleEndpoints();
app.MapMovesEndpoints();
app.MapSpeciesEndpoints();
app.MapPokemonEndpoints();
app.Run();

// Hace accesible el punto de entrada al host HTTP de las pruebas de integración.
public partial class Program { }
