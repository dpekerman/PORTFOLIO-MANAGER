import { CurrencyPipe } from "@angular/common";
import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  inject,
  signal,
} from "@angular/core";
import { FormBuilder, ReactiveFormsModule, Validators } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatFormFieldModule } from "@angular/material/form-field";
import { MatIconModule } from "@angular/material/icon";
import { MatInputModule } from "@angular/material/input";
import { MatProgressSpinnerModule } from "@angular/material/progress-spinner";
import { MatTableModule } from "@angular/material/table";
import { MatTooltipModule } from "@angular/material/tooltip";
import { Employee } from "./employee.model";
import { EmployeeService } from "./employee.service";

@Component({
  selector: "app-employee-list",
  imports: [
    CurrencyPipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatTableModule,
    MatTooltipModule,
  ],
  templateUrl: "./employee-list.component.html",
  styleUrl: "./employee-list.component.scss",
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EmployeeListComponent implements OnInit {
  private readonly service = inject(EmployeeService);
  private readonly fb = inject(FormBuilder);

  protected readonly employees = signal<Employee[]>([]);
  protected readonly loading = signal(false);
  protected readonly displayedColumns = [
    "name",
    "email",
    "department",
    "salary",
    "actions",
  ];

  protected readonly form = this.fb.nonNullable.group({
    firstName: ["", Validators.required],
    lastName: ["", Validators.required],
    email: ["", [Validators.required, Validators.email]],
    department: ["", Validators.required],
    salary: [0, [Validators.required, Validators.min(1)]],
  });

  ngOnInit(): void {
    this.loadEmployees();
  }

  protected loadEmployees(): void {
    this.loading.set(true);
    this.service.getEmployees().subscribe({
      next: (data: Employee[]) => this.employees.set(data),
      complete: () => this.loading.set(false),
      error: () => this.loading.set(false),
    });
  }

  protected addEmployee(): void {
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

  protected deleteEmployee(id: number): void {
    this.service.deleteEmployee(id).subscribe(() => this.loadEmployees());
  }
}
