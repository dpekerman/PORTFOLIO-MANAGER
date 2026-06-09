using EmployeeApp.Api.Data;
using EmployeeApp.Api.Dtos;
using EmployeeApp.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace EmployeeApp.Api.Services;

public class EmployeeService : IEmployeeService
{
    private readonly AppDbContext _db;

    public EmployeeService(AppDbContext db) => _db = db;

    public async Task<List<EmployeeDto>> GetEmployeesAsync(string? search)
    {
        var query = _db.Employees.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(e =>
                e.FirstName.Contains(search) ||
                e.LastName.Contains(search) ||
                e.Email.Contains(search) ||
                e.Department.Contains(search));
        }

        return await query
            .OrderBy(e => e.LastName)
            .Select(e => new EmployeeDto(e.Id, e.FirstName, e.LastName, e.Email, e.Department, e.Salary))
            .ToListAsync();
    }

    public async Task<EmployeeDto?> GetEmployeeAsync(int id)
    {
        return await _db.Employees.AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new EmployeeDto(e.Id, e.FirstName, e.LastName, e.Email, e.Department, e.Salary))
            .FirstOrDefaultAsync();
    }

    public async Task<EmployeeDto> CreateEmployeeAsync(CreateEmployeeRequest request)
    {
        var employee = new Employee
        {
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            Department = request.Department,
            Salary = request.Salary
        };

        _db.Employees.Add(employee);
        await _db.SaveChangesAsync();

        return new EmployeeDto(employee.Id, employee.FirstName, employee.LastName, employee.Email, employee.Department, employee.Salary);
    }

    public async Task<bool> UpdateEmployeeAsync(int id, UpdateEmployeeRequest request)
    {
        var employee = await _db.Employees.FindAsync(id);
        if (employee is null) return false;

        employee.FirstName = request.FirstName;
        employee.LastName = request.LastName;
        employee.Email = request.Email;
        employee.Department = request.Department;
        employee.Salary = request.Salary;

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteEmployeeAsync(int id)
    {
        var employee = await _db.Employees.FindAsync(id);
        if (employee is null) return false;

        _db.Employees.Remove(employee);
        await _db.SaveChangesAsync();
        return true;
    }
}
