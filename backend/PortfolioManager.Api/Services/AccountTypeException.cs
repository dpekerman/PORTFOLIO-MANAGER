namespace PortfolioManager.Api.Services;

public sealed class AccountTypeException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
