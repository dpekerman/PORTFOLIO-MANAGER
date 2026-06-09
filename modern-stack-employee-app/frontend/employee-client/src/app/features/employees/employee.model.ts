export interface Employee {
  id: number;
  firstName: string;
  lastName: string;
  email: string;
  department: string;
  salary: number;
}

export type CreateEmployee = Omit<Employee, 'id'>;
