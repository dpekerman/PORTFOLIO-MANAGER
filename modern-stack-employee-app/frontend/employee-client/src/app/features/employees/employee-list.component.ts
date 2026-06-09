import { CommonModule } from "@angular/common";
import { Component, OnInit, inject, signal } from "@angular/core";
import { FormBuilder, ReactiveFormsModule, Validators } from "@angular/forms";
import { Employee } from "./employee.model";
import { EmployeeService } from "./employee.service";

@Component({
  selector: "app-employee-list",
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <form [formGroup]="form" (ngSubmit)="addEmployee()">
      <input formControlName="firstName" placeholder="First name" />
      <input formControlName="lastName" placeholder="Last name" />
      <input formControlName="email" placeholder="Email" />
      <input formControlName="department" placeholder="Department" />
      <input formControlName="salary" type="number" placeholder="Salary" />
      <button type="submit" [disabled]="form.invalid">Add</button>
    </form>

    <hr />

    @if (loading()) {
      <p>Loading...</p>
    } @else {
      <ul>
        @for (employee of employees(); track employee.id) {
          <li>
            {{ employee.firstName }} {{ employee.lastName }} -
            {{ employee.department }} - {{ employee.salary | currency }}
            <button type="button" (click)="deleteEmployee(employee.id)">
              Delete
            </button>
          </li>
        }
      </ul>
    }
  `,
})
export class EmployeeListComponent implements OnInit {
  private service = inject(EmployeeService);
  private fb = inject(FormBuilder);

  employees = signal<Employee[]>([]);
  loading = signal(false);

  form = this.fb.nonNullable.group({
    firstName: ["", Validators.required],
    lastName: ["", Validators.required],
    email: ["", [Validators.required, Validators.email]],
    department: ["", Validators.required],
    salary: [0, [Validators.required, Validators.min(1)]],
  });

  ngOnInit(): void {
    this.loadEmployees();
  }

  loadEmployees(): void {
    this.loading.set(true);
    this.service.getEmployees().subscribe({
      next: (data: Employee[]) => this.employees.set(data),
      complete: () => this.loading.set(false),
      error: () => this.loading.set(false),
    });
  }

  addEmployee(): void {
    if (this.form.invalid) return;

    this.service.createEmployee(this.form.getRawValue()).subscribe(() => {
      this.form.reset({
        firstName: "",
        lastName: "",
        email: "",
        department: "",
        salary: 0,
      });
      this.loadEmployees();
    });
  }

  deleteEmployee(id: number): void {
    this.service.deleteEmployee(id).subscribe(() => this.loadEmployees());
  }
}
