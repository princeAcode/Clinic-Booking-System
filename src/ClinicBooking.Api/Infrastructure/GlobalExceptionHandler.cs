using ClinicBooking.Application.Exceptions;
using ClinicBooking.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace ClinicBooking.Api.Infrastructure;

/// <summary>
/// Turns exceptions into RFC 7807 problem details so every error has the same shape for the client.
/// Business errors get a meaningful status code and message; anything unexpected becomes a 500 without internals.
/// </summary>
internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = exception switch
        {
            ValidationException validation => CreateValidationProblem(validation),
            NotFoundException => Problem(StatusCodes.Status404NotFound, "Not found", exception.Message),
            ForbiddenAccessException => Problem(StatusCodes.Status403Forbidden, "Forbidden", exception.Message),
            AuthenticationFailedException => Problem(StatusCodes.Status401Unauthorized, "Sign-in failed", exception.Message),
            BookingConflictException => Problem(StatusCodes.Status409Conflict, "Booking conflict", exception.Message),
            BusinessRuleException => Problem(StatusCodes.Status422UnprocessableEntity, "Request not allowed", exception.Message),
            _ => Problem(StatusCodes.Status500InternalServerError, "Server error", "Something went wrong. Please try again."),
        };

        if (problem.Status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            logger.LogInformation("Request rejected with {StatusCode}: {Message}", problem.Status, exception.Message);
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static ProblemDetails Problem(int status, string title, string detail) =>
        new() { Status = status, Title = title, Detail = detail };

    private static ValidationProblemDetails CreateValidationProblem(ValidationException exception)
    {
        var errors = exception.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());

        return new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation failed",
            Detail = string.Join(" ", errors.Values.SelectMany(v => v)),
        };
    }
}
