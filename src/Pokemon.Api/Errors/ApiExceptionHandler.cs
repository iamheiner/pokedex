using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Pokemon.Application.Common.Exceptions;

namespace Pokemon.Api.Errors;

/// <summary>Ofrece un contrato de error uniforme sin revelar detalles de fallos internos.</summary>
public sealed class ApiExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var status = exception switch
        {
            InvalidDamageRequestException => StatusCodes.Status400BadRequest,
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
                Title = status == 400 ? "Invalid damage request" : status == 415 ? "Unsupported media type" : "Request failed",
                Detail = exception switch
                {
                    InvalidDamageRequestException validation => validation.Message,
                    BadHttpRequestException => "Send valid application/json with all required fields and supported type names.",
                    _ => "An unexpected error occurred. Use the traceId to locate the failure."
                }
            }
        });
    }
}
