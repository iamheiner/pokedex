using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Pokemon.Application.Common.Exceptions;
using Pokemon.Application.Feature.Pokedex.Exceptions;
using Pokemon.Domain.Battle.Exceptions;
using Pokemon.Domain.Common.Exceptions;
using Pokemon.Domain.Pokedex.Exceptions;

namespace Pokemon.Api.Middleware;

/// <summary>
/// Ofrece un contrato de error uniforme sin revelar detalles de fallos internos.
/// </summary>
public sealed class ApiExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var status = exception switch
        {
            InvalidDamageRequestException or PokedexRuleException or BattleRuleException => StatusCodes.Status400BadRequest,
            PokedexNotFoundException or BattleNotFoundException => StatusCodes.Status404NotFound,
            PokedexConflictException or PersistenceConflictException or BattleConflictException => StatusCodes.Status409Conflict,
            BadHttpRequestException badRequest => badRequest.StatusCode,
            _ => StatusCodes.Status500InternalServerError
        };
        context.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = status switch { 400 => "Invalid request", 
                                        404 => "Resource not found", 
                                        409 => "Resource conflict", 
                                        415 => "Unsupported media type", 
                                          _ => "Request failed" },
                Detail = exception switch
                {
                    InvalidDamageRequestException validation => validation.Message,
                    PokedexRuleException or PokedexNotFoundException or PokedexConflictException or PersistenceConflictException or
                    BattleRuleException or BattleNotFoundException or BattleConflictException => exception.Message,
                    BadHttpRequestException => "Send valid application/json with all required fields and supported type names.",
                    _ => "An unexpected error occurred. Use the traceId to locate the failure."
                }
            }
        });
    }
}
