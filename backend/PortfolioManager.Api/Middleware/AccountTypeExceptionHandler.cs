using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Middleware;

public sealed class AccountTypeExceptionHandler(ILogger<AccountTypeExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is not AccountTypeException error) return false;
        logger.LogWarning("Account type operation rejected: {Reason}", error.Message);
        context.Response.StatusCode = error.StatusCode;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = error.StatusCode,
            Title = "Account type operation failed",
            Detail = error.Message
        }, ct);
        return true;
    }
}
