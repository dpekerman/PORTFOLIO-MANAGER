import { Injectable, inject, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  SaveSecurityAnalysisMappingRequest,
  SecurityAnalysisMapping,
} from '../models/portfolio.models';
import { PortfolioApiService } from './portfolio-api.service';

@Injectable({ providedIn: 'root' })
export class SecurityAnalysisMappingStateService {
  private readonly api = inject(PortfolioApiService);
  private readonly _mapping = signal<SecurityAnalysisMapping | null>(null);
  private readonly _loading = signal(false);
  private readonly _error = signal<string | null>(null);

  readonly mapping = this._mapping.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly error = this._error.asReadonly();

  async load(tradingTicker: string): Promise<SecurityAnalysisMapping | null> {
    this._loading.set(true);
    this._error.set(null);
    try {
      const mapping = await firstValueFrom(this.api.getSecurityAnalysisMapping(tradingTicker));
      this._mapping.set(mapping);
      return mapping;
    } catch (error: unknown) {
      this._error.set(this.messageFor(error));
      return null;
    } finally {
      this._loading.set(false);
    }
  }

  validate(tradingTicker: string, underlyingTicker: string): Promise<void> {
    return firstValueFrom(
      this.api.validateUnderlyingTicker(tradingTicker, {
        underlyingTicker: underlyingTicker.trim().toUpperCase(),
        useUnderlyingForAnalysis: true,
      }),
    );
  }

  async save(tradingTicker: string, request: SaveSecurityAnalysisMappingRequest): Promise<boolean> {
    this._loading.set(true);
    this._error.set(null);
    try {
      const mapping = await firstValueFrom(
        this.api.saveSecurityAnalysisMapping(tradingTicker, request),
      );
      this._mapping.set(mapping);
      return true;
    } catch (error: unknown) {
      this._error.set(this.messageFor(error));
      return false;
    } finally {
      this._loading.set(false);
    }
  }

  async remove(tradingTicker: string): Promise<boolean> {
    this._loading.set(true);
    this._error.set(null);
    try {
      await firstValueFrom(this.api.removeSecurityAnalysisMapping(tradingTicker));
      this._mapping.set(null);
      return true;
    } catch (error: unknown) {
      this._error.set(this.messageFor(error));
      return false;
    } finally {
      this._loading.set(false);
    }
  }

  private messageFor(error: unknown): string {
    if (typeof error === 'object' && error !== null && 'error' in error) {
      const body = (error as { error?: string | { title?: string } }).error;
      if (typeof body === 'string') return body;
      if (body?.title) return body.title;
    }
    return 'Unable to update the underlying security mapping.';
  }
}
