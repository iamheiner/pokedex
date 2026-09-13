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
});
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
builder.Services.AddApplication();
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
app.Run();

// Hace accesible el punto de entrada al host HTTP de las pruebas de integración.
public partial class Program { }
