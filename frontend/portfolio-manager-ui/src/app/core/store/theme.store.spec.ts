import { TestBed } from '@angular/core/testing';
import { ThemeStore } from './theme.store';

describe('ThemeStore', () => {
  beforeEach(() => {
    localStorage.removeItem('app-theme');
    document.body.classList.remove('light-theme');
  });

  it('defaults to dark and persists it', () => {
    const store = TestBed.inject(ThemeStore);
    expect(store.theme()).toBe('dark');
    expect(store.isDark()).toBe(true);
    expect(localStorage.getItem('app-theme')).toBe('dark');
    expect(document.body.classList.contains('light-theme')).toBe(false);
  });

  it('restores the persisted theme', () => {
    localStorage.setItem('app-theme', 'light');
    const store = TestBed.inject(ThemeStore);
    expect(store.theme()).toBe('light');
    expect(document.body.classList.contains('light-theme')).toBe(true);
  });

  it('toggles theme, body class and storage together', () => {
    const store = TestBed.inject(ThemeStore);
    store.toggle();
    expect(store.isDark()).toBe(false);
    expect(localStorage.getItem('app-theme')).toBe('light');
    expect(document.body.classList.contains('light-theme')).toBe(true);
    store.toggle();
    expect(store.theme()).toBe('dark');
    expect(document.body.classList.contains('light-theme')).toBe(false);
  });
});
