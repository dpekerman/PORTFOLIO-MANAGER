import { HttpClient } from "@angular/common/http";
import { Injectable, inject } from "@angular/core";
import { Observable } from "rxjs";
import { CreateEmployee, Employee } from "./employee.model";

@Injectable({ providedIn: "root" })
export class EmployeeService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = "http://localhost:5000/api/employees";

  getEmployees(search = ""): Observable<Employee[]> {
    return this.http.get<Employee[]>(
      `${this.apiUrl}?search=${encodeURIComponent(search)}`,
    );
  }

  createEmployee(employee: CreateEmployee): Observable<Employee> {
    return this.http.post<Employee>(this.apiUrl, employee);
  }

  deleteEmployee(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}
