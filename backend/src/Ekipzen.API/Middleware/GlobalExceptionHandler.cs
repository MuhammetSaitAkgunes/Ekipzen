using FluentValidation;
using Ekipzen.Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Ekipzen.API.Middleware;

/// <summary>
/// Uygulama istisnalarını RFC 7807 ProblemDetails yanıtlarına eşler.
/// Bilinen istisnalar (doğrulama/yetki/bulunamadı/çakışma) uygun 4xx koduna,
/// kalanlar 500'e döner.
/// </summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problemDetails = exception switch
        {
            ValidationException validationException => new ValidationProblemDetails(
                validationException.Errors
                    .GroupBy(e => e.PropertyName, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Doğrulama hatası.",
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1"
            },
            UnauthorizedException => new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = exception.Message,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.2"
            },
            ForbiddenAccessException => new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = exception.Message,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.4"
            },
            TenantInaccessibleException => new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = exception.Message,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.4"
            },
            TenantSchemaOutdatedException => new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = exception.Message,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.6.4"
            },
            NotFoundException => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = exception.Message,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.5"
            },
            ConflictException => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = exception.Message,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.10"
            },
            ArgumentException => new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Geçersiz istek.",
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1"
            },
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Beklenmeyen bir hata oluştu.",
                Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1"
            }
        };

        if (problemDetails.Status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "İşlenmeyen hata: {Message}", exception.Message);
        }
        else
        {
            logger.LogWarning("İstek hatası ({Status}): {Message}", problemDetails.Status, exception.Message);
        }

        httpContext.Response.StatusCode = problemDetails.Status!.Value;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails
        });
    }
}
