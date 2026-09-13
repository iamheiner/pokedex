using Pokemon.Api.Feature.Authentication;
using Pokemon.Api.Feature.Battle;
using Pokemon.Domain.Battle;
using Pokemon.Infrastructure;
using Pokemon.Api.Feature.Pokedex.Moves;
using Pokemon.Api.Feature.Pokedex.Species;
using Pokemon.Api.Feature.Pokedex.Pokemon;
using System.Diagnostics;
using Pokemon.Api.Middleware;
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
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddDamageFeature();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions["traceId"] = Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier);
builder.Services.AddApiAuthentication(builder.Configuration);
if (DocumentationAuthentication.IsEnabled(builder.Environment, builder.Configuration))
    builder.Services.AddDocumentationAuthentication(builder.Configuration);
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecurityTransformer>());
builder.AddObservability();

var app = builder.Build();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/scalar") || context.Request.Path.StartsWithSegments("/openapi"))
    {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
    }
    await next(context);
});
app.UseAuthentication();
app.UseAuthorization();
if (DocumentationAuthentication.IsEnabled(app.Environment, app.Configuration))
    app.MapProtectedDocumentation();
app.MapHealthEndpoints();
app.MapDamageEndpoints();
app.MapBattleEndpoints();
app.MapMovesEndpoints();
app.MapSpeciesEndpoints();
app.MapPokemonEndpoints();
app.Run();

// Hace accesible el punto de entrada al host HTTP de las pruebas de integración.
public partial class Program { }
