using EmployeeApp.Api.Dtos;

namespace EmployeeApp.Api.Services;

public interface IEmployeeService
{
    Task<List<EmployeeDto>> GetEmployeesAsync(string? search);
    Task<EmployeeDto?> GetEmployeeAsync(int id);
    Task<EmployeeDto> CreateEmployeeAsync(CreateEmployeeRequest request);
    Task<bool> UpdateEmployeeAsync(int id, UpdateEmployeeRequest request);
    Task<bool> DeleteEmployeeAsync(int id);
}
