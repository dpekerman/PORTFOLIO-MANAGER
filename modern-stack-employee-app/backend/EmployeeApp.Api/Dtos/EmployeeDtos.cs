namespace EmployeeApp.Api.Dtos;

public record EmployeeDto(int Id, string FirstName, string LastName, string Email, string Department, decimal Salary);
public record CreateEmployeeRequest(string FirstName, string LastName, string Email, string Department, decimal Salary);
public record UpdateEmployeeRequest(string FirstName, string LastName, string Email, string Department, decimal Salary);
