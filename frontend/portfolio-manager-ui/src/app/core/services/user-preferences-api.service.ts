import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class UserPreferencesApiService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/users/preferences';

  getAll(): Observable<Record<string, string>> {
    return this.http.get<Record<string, string>>(this.base);
  }

  upsert(key: string, value: string): Observable<void> {
    return this.http.put<void>(`${this.base}/${encodeURIComponent(key)}`, { value });
  }

  delete(key: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/${encodeURIComponent(key)}`);
  }
}
