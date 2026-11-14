import { computed } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { withStorageSync } from './features/storage-sync.feature';

export type Theme = 'dark' | 'light';

const STORAGE_KEY = 'app-theme';

interface ThemeState {
  theme: Theme;
}

/**
 * The app colour theme. The choice is persisted as the raw string under `app-theme` and applied
 * as the `light-theme` class on `<body>` at boot and on every change.
 */
export const ThemeStore = signalStore(
  { providedIn: 'root' },
  withState<ThemeState>(() => ({
    theme: (localStorage.getItem(STORAGE_KEY) as Theme) ?? 'dark',
  })),
  withComputed(({ theme }) => ({
    isDark: computed(() => theme() === 'dark'),
  })),
  withMethods((store) => ({
    toggle(): void {
      patchState(store, ({ theme }) => ({ theme: theme === 'dark' ? ('light' as Theme) : 'dark' }));
    },
  })),
  withStorageSync<ThemeState>(({ theme }) => {
    document.body.classList.toggle('light-theme', theme === 'light');
    localStorage.setItem(STORAGE_KEY, theme);
  }),
);
