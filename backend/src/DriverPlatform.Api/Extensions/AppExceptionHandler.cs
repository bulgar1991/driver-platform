using DriverPlatform.Application.Abstractions;
using DriverPlatform.Application.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace DriverPlatform.Api.Extensions;

/// <summary>Turns business-rule exceptions from the application layer into problem details.</summary>
public sealed class AppExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        ProblemDetails? problem = exception switch
        {
            RequestValidationException e => new ValidationProblemDetails(e.Errors)
            {
                Status = StatusCodes.Status400BadRequest,
            },
            NotFoundException e => new ProblemDetails { Status = StatusCodes.Status404NotFound, Title = e.Message },
            InvalidWebhookException e => new ProblemDetails { Status = StatusCodes.Status400BadRequest, Title = e.Message },
            ConflictException e => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = e.Message,
                Extensions = { ["data"] = e.Payload },
            },
            _ => null,
        };
        if (problem is null)
        {
            return false;
        }

        context.Response.StatusCode = problem.Status!.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}
