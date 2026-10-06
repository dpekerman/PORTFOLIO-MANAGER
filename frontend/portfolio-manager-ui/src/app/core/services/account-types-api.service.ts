import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AccountType, AccountTypeRename } from '../models/portfolio.models';
import { PortfolioApiService } from './portfolio-api.service';

@Injectable({ providedIn: 'root' })
export class AccountTypesApiService {
  private readonly api = inject(PortfolioApiService);

  getAll(): Observable<AccountType[]> {
    return this.api.getAccountTypes();
  }
  add(name: string): Observable<AccountType> {
    return this.api.addAccountType(name);
  }
  rename(item: AccountType, name: string): Observable<AccountTypeRename> {
    return this.api.renameAccountType(item, name);
  }
  delete(item: AccountType): Observable<void> {
    return this.api.deleteAccountType(item);
  }
}
