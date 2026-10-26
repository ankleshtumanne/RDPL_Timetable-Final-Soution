using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Rdpl.Timetable.Data;

namespace Rdpl.Timetable.Api.Infrastructure;

/// <summary>
/// Centralized exception handling for controllers and endpoints. Known errors become RFC 7807 problem responses with
/// safe messages; everything else is a generic 500. Stack traces, SQL and connection details never reach clients.
/// </summary>
public sealed class SafeExceptionHandler : IExceptionHandler
{
    private readonly ILogger<SafeExceptionHandler> _logger;
    private readonly IProblemDetailsService _problemDetails;

    public SafeExceptionHandler(ILogger<SafeExceptionHandler> logger, IProblemDetailsService problemDetails)
    {
        _logger = logger;
        _problemDetails = problemDetails;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        ProblemDetails problem = exception switch
        {
            RequestValidationException e => new HttpValidationProblemDetails(e.Errors.ToDictionary(kv => kv.Key, kv => kv.Value))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred.",
            },
            BadHttpRequestException => Problem(StatusCodes.Status400BadRequest, "Bad request", "The request could not be read."),
            ResourceNotFoundException e => Problem(StatusCodes.Status404NotFound, "Not Found", e.Message),
            GenerationInProgressException e => Problem(StatusCodes.Status409Conflict, "Conflict", e.Message),
            DataInputException e => Problem(StatusCodes.Status422UnprocessableEntity, "Input data error", e.Message),
            DbUpdateConcurrencyException => Problem(StatusCodes.Status409Conflict, "Conflict", "The record was changed by another request. Reload and retry."),
            NpgsqlException or DbUpdateException or TimeoutException or RetryLimitExceededException
                => Problem(StatusCodes.Status503ServiceUnavailable, "Database unavailable", "The database is unavailable or rejected the operation. Try again later."),
            OperationCanceledException when httpContext.RequestAborted.IsCancellationRequested
                => Problem(499, "Client closed request", "The request was cancelled."),
            _ => Problem(StatusCodes.Status500InternalServerError, "Unexpected error", "An unexpected error occurred. It has been logged."),
        };

        if (problem.Status >= 500)
        {
            _logger.LogError(exception, "Request to {Path} failed with {Status}.", httpContext.Request.Path, problem.Status);
        }
        else
        {
            _logger.LogInformation("Request to {Path} rejected with {Status}: {Type}.", httpContext.Request.Path, problem.Status, exception.GetType().Name);
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await _problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        }).ConfigureAwait(false);
    }

    private static ProblemDetails Problem(int status, string title, string detail) => new() { Status = status, Title = title, Detail = detail };
}
