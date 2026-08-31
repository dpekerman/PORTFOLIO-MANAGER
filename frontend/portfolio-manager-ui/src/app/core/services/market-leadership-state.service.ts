import { Injectable, inject, signal } from '@angular/core';
import { finalize, firstValueFrom } from 'rxjs';
import { CreateMarketLeadershipTrackerRequest } from '../models/portfolio.models';
import { DashboardStateService } from './dashboard-state.service';
import { MarketLeadershipApiService } from './market-leadership-api.service';

@Injectable({ providedIn: 'root' })
export class MarketLeadershipStateService {
  private readonly api = inject(MarketLeadershipApiService);
  private readonly dashboard = inject(DashboardStateService);
  private readonly _saving = signal(false);
  private readonly _error = signal<string | null>(null);

  readonly saving = this._saving.asReadonly();
  readonly error = this._error.asReadonly();

  async addTracker(request: CreateMarketLeadershipTrackerRequest): Promise<boolean> {
    this._saving.set(true);
    this._error.set(null);
    try {
      await firstValueFrom(this.api.addTracker(request));
      this.dashboard.loadMarketLeadership();
      return true;
    } catch (error: unknown) {
      this._error.set(this.messageFor(error));
      return false;
    } finally {
      this._saving.set(false);
    }
  }

  async updateTracker(
    trackerId: number,
    request: CreateMarketLeadershipTrackerRequest,
  ): Promise<boolean> {
    this._saving.set(true);
    this._error.set(null);
    try {
      await firstValueFrom(this.api.updateTracker(trackerId, request));
      this.dashboard.loadMarketLeadership();
      return true;
    } catch (error: unknown) {
      this._error.set(this.messageFor(error));
      return false;
    } finally {
      this._saving.set(false);
    }
  }

  removeTracker(trackerId: number): void {
    this._saving.set(true);
    this._error.set(null);
    this.api
      .removeTracker(trackerId)
      .pipe(finalize(() => this._saving.set(false)))
      .subscribe({
        next: () => this.dashboard.loadMarketLeadership(),
        error: (error: unknown) => this._error.set(this.messageFor(error)),
      });
  }

  private messageFor(error: unknown): string {
    if (typeof error === 'object' && error !== null && 'error' in error) {
      const body = (error as { error?: { message?: string } }).error;
      if (body?.message) return body.message;
    }
    if (typeof error === 'object' && error !== null && 'status' in error) {
      const status = (error as { status?: number }).status;
      if (status === 404 || status === 405) {
        return 'The backend must be restarted before tracker changes can be saved.';
      }
    }
    return 'Unable to update market trackers.';
  }
}
