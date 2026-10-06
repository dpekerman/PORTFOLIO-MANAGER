using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

public interface IAccountTypeService
{
    Task InitializeAsync(CancellationToken ct);
    Task<IReadOnlyList<AccountTypeResponse>> GetAllAsync(CancellationToken ct);
    Task<AccountTypeResponse> AddAsync(string name, CancellationToken ct);
    Task<AccountTypeRenameResponse> RenameAsync(int id, UpdateAccountTypeRequest request, CancellationToken ct);
    Task DeleteAsync(int id, Guid version, CancellationToken ct);
}
