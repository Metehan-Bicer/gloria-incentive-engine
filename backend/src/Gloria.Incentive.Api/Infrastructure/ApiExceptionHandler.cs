using Gloria.Incentive.Api.Rules;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Gloria.Incentive.Api.Infrastructure;

public class ApiExceptionHandler : IExceptionHandler
{
    private readonly ILogger<ApiExceptionHandler> _logger;

    public ApiExceptionHandler(ILogger<ApiExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Kayıt bulunamadı"),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Yetkisiz işlem"),
            ConflictException => (StatusCodes.Status409Conflict, "İşlem çakışması"),
            ValidationException => (StatusCodes.Status400BadRequest, "Geçersiz istek"),
            RuleParameterException => (StatusCodes.Status400BadRequest, "Geçersiz kural parametresi"),
            _ => (StatusCodes.Status500InternalServerError, "Beklenmeyen hata")
        };

        if (status == StatusCodes.Status500InternalServerError)
            _logger.LogError(exception, "Unhandled exception");

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status == StatusCodes.Status500InternalServerError ? null : exception.Message
        }, cancellationToken);

        return true;
    }
}
