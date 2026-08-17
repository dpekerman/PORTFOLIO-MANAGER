import { effect, Injectable, signal } from '@angular/core';

export type Theme = 'dark' | 'light';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly _theme = signal<Theme>((localStorage.getItem('app-theme') as Theme) ?? 'dark');

  readonly theme = this._theme.asReadonly();
  readonly isDark = () => this._theme() === 'dark';

  constructor() {
    // Apply theme on boot and whenever it changes
    effect(() => {
      const t = this._theme();
      document.body.classList.toggle('light-theme', t === 'light');
      localStorage.setItem('app-theme', t);
    });
  }

  toggle(): void {
    this._theme.update((t) => (t === 'dark' ? 'light' : 'dark'));
  }
}
