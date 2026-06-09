import { HttpClient } from "@angular/common/http";
import { Injectable } from "@angular/core";
import { CreateEmployee, Employee } from "./employee.model";

@Injectable({ providedIn: "root" })
export class EmployeeService {
  private readonly apiUrl = "http://localhost:5000/api/employees";

  constructor(private http: HttpClient) {}

  getEmployees(search = "") {
    return this.http.get<Employee[]>(
      `${this.apiUrl}?search=${encodeURIComponent(search)}`,
    );
  }

  createEmployee(employee: CreateEmployee) {
    return this.http.post<Employee>(this.apiUrl, employee);
  }

  deleteEmployee(id: number) {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}
