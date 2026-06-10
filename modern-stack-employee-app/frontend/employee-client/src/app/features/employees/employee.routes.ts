import { Routes } from "@angular/router";

export const employeeRoutes: Routes = [
  {
    path: "",
    loadComponent: () =>
      import("./employee-list.component").then((m) => m.EmployeeListComponent),
  },
];
