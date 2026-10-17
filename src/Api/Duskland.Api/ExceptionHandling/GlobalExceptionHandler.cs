using Duskland.Modules.Products.Domain.Common;
using Duskland.Modules.Products.Infrastructure.DAL.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Duskland.Api.ExceptionHandling;

public class GlobalExceptionHandler(IProblemDetailsService ProblemDetailsService, ILogger<GlobalExceptionHandler> Logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var statusCode = StatusCodes.Status500InternalServerError;
        var title = "Server Error";
        var detail = exception.Message;

        if (exception is ConflictException conflictException)
        {
            statusCode = StatusCodes.Status409Conflict;
            title = "Conflict Exception";
            detail = conflictException.Message;
        }
        else if (exception is DomainException domainException)
        {
            statusCode = StatusCodes.Status400BadRequest;
            title = "Domain Exception";
            detail = domainException.Message;            
        }
        else
        {
            Logger.LogError(exception, "Internal Server Error");
        }

        httpContext.Response.StatusCode = statusCode;

        var problemContext = new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail,
                Instance = httpContext.Request.Path
            }
        };

        return await ProblemDetailsService.TryWriteAsync(problemContext);
    }
}
