using EthioServe.Application.Common.Exceptions;
using EthioServe.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EthioServe.API.Middleware;

/// <summary>
/// Converts exceptions into RFC 7807 ProblemDetails responses.
/// Unexpected exceptions are logged in full but never exposed to the client.
/// </summary>
public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // Client disconnected: not an error worth a 500 and a stack trace.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            return true;
        }

        var (status, title) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
            UnauthorizedException => (StatusCodes.Status401Unauthorized, "Authentication failed"),
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found"),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Access denied"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            ServiceUnavailableException => (StatusCodes.Status503ServiceUnavailable, "Service unavailable"),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Conflict"),
            // Safety net: the database's unique indexes decided a race that the application checks could not.
            // Two bookings racing for the same slot can deadlock inside PostgreSQL. The loser is simply told to try again.
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure } }
                => (StatusCodes.Status409Conflict, "Conflict"),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ExclusionViolation } }
                => (StatusCodes.Status409Conflict, "Conflict"),
            DomainException => (StatusCodes.Status422UnprocessableEntity, "Business rule violation"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception on {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            logger.LogWarning("Handled {ExceptionType}: {Message}", exception.GetType().Name, exception.Message);
        }

        httpContext.Response.StatusCode = status;

        ProblemDetails problem = exception is ValidationException validation
            ? new HttpValidationProblemDetails(ToErrors(validation))
            {
                Status = status,
                Title = title,
                Detail = "One or more validation errors occurred."
            }
            : new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = SafeDetail(exception, status)
            };

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem
        });
    }

    // Our own exceptions carry safe messages; database and unexpected errors never reach the client.
    private static string SafeDetail(Exception exception, int status) => exception switch
    {
        _ when status == StatusCodes.Status500InternalServerError =>
            "Something went wrong on our side. Please try again later.",
        DbUpdateConcurrencyException => "This item was changed by someone else. Please reload and try again.",
        DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.DeadlockDetected or PostgresErrorCodes.SerializationFailure } } =>
            "Another change was being saved at the same moment. Please try again.",
        DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.ExclusionViolation } } =>
            "That time was just taken by another appointment. Please choose a different time.",
        DbUpdateException => "This item conflicts with an existing one.",
        _ => exception.Message
    };

    private static Dictionary<string, string[]> ToErrors(ValidationException exception) =>
        exception.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());
}