using System.Diagnostics;
using Majordomo.SharedKernel;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Majordomo.Infrastructure.Web;

public static class ProblemDetailsSetup
{
    /// <summary>ProblemDetails (RFC 9457) con <c>traceId</c> e mappatura centralizzata delle eccezioni note.</summary>
    public static IServiceCollection AddMajordomoProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
            ctx.ProblemDetails.Extensions["traceId"] = Activity.Current?.TraceId.ToString() ?? ctx.HttpContext.TraceIdentifier);
        services.AddExceptionHandler<MajordomoExceptionHandler>();
        return services;
    }
}

internal sealed class MajordomoExceptionHandler(IProblemDetailsService problemDetails, ILogger<MajordomoExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails? problem = exception switch
        {
            RequestValidationException e => new ValidationProblemDetails(e.Errors)
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Parametri non validi",
                Type = "https://majordomo.k-digitale.cloud/problems/validation",
            },
            DomainException e => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Operazione non ammessa nello stato corrente",
                Detail = e.Message,
                Type = $"https://majordomo.k-digitale.cloud/problems/{e.Code}",
            },
            NotFoundException e => new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = "Risorsa non trovata", Detail = e.Message },
            ForbiddenOperationException e => new ProblemDetails { Status = StatusCodes.Status403Forbidden, Title = "Operazione non consentita", Detail = e.Message },
            PreconditionFailedException e => new ProblemDetails { Status = StatusCodes.Status412PreconditionFailed, Title = "Precondizione fallita", Detail = e.Message },
            DbUpdateConcurrencyException => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflitto di concorrenza",
                Detail = "La risorsa è stata modificata da un'altra richiesta: rileggerla e riprovare.",
            },
            BadHttpRequestException e => new ProblemDetails { Status = e.StatusCode, Title = "Richiesta non valida", Detail = e.Message },
            _ => null,
        };

        if (problem is null)
        {
            logger.LogError(exception, "Errore non gestito su {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
            return false; // gestito dal default (500 ProblemDetails senza dettagli interni)
        }

        httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}
