import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { CreateUserRequest, UserInfo } from '../models/portfolio.models';

@Injectable({ providedIn: 'root' })
export class UsersApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/users';

  getAll(): Observable<UserInfo[]> {
    return this.http.get<UserInfo[]>(this.base);
  }

  create(request: CreateUserRequest): Observable<UserInfo> {
    return this.http.post<UserInfo>(this.base, request);
  }

  assignRole(userId: string, role: string): Observable<void> {
    return this.http.put<void>(`${this.base}/${userId}/role`, { role });
  }

  delete(userId: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${userId}`);
  }
}
