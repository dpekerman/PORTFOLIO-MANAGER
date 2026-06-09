IF DB_ID('EmployeeDb') IS NULL
BEGIN
    CREATE DATABASE EmployeeDb;
END
GO

USE EmployeeDb;
GO

IF OBJECT_ID('dbo.Employees', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Employees
    (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        FirstName NVARCHAR(100) NOT NULL,
        LastName NVARCHAR(100) NOT NULL,
        Email NVARCHAR(200) NOT NULL UNIQUE,
        Department NVARCHAR(100) NULL,
        Salary DECIMAL(18,2) NOT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END
GO

INSERT INTO dbo.Employees (FirstName, LastName, Email, Department, Salary)
SELECT 'Dima', 'Pekerman', 'dima@example.com', 'Engineering', 120000
WHERE NOT EXISTS (SELECT 1 FROM dbo.Employees WHERE Email = 'dima@example.com');
GO
