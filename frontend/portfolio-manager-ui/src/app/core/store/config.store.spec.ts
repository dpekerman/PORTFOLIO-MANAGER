import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { UserPreferencesStore } from './user-preferences.store';
import { ConfigStore } from './config.store';

describe('ConfigStore', () => {
  const dbPrefs = signal<unknown>(null);
  const prefs = { get: vi.fn(() => dbPrefs()), set: vi.fn(), remove: vi.fn() };

  beforeEach(() => {
    localStorage.clear();
    dbPrefs.set(null);
    vi.clearAllMocks();
    TestBed.configureTestingModule({
      providers: [{ provide: UserPreferencesStore, useValue: prefs }],
    });
  });

  it('starts from defaults and persists them', () => {
    const store = TestBed.inject(ConfigStore);
    TestBed.tick();
    expect(store.appRefreshSeconds()).toBe(120);
    expect(JSON.parse(localStorage.getItem('pm_app_config')!).rsiOversoldThreshold).toBe(30);
  });

  it('migrates the legacy portfolioRefreshSeconds key', () => {
    localStorage.setItem('pm_app_config', JSON.stringify({ portfolioRefreshSeconds: 45 }));
    const store = TestBed.inject(ConfigStore);
    expect(store.config().appRefreshSeconds).toBe(45);
  });

  it('falls back to defaults on corrupt storage', () => {
    localStorage.setItem('pm_app_config', '{nope');
    expect(TestBed.inject(ConfigStore).scanIntervalSeconds()).toBe(0);
  });

  it('update writes through to preferences; reset removes them', () => {
    const store = TestBed.inject(ConfigStore);
    store.update({ rsiOverboughtThreshold: 80 });
    expect(store.rsiOverboughtThreshold()).toBe(80);
    expect(prefs.set).toHaveBeenCalledWith(
      'pm_app_config',
      expect.objectContaining({ rsiOverboughtThreshold: 80 }),
    );
    store.reset();
    expect(store.rsiOverboughtThreshold()).toBe(75);
    expect(prefs.remove).toHaveBeenCalledWith('pm_app_config');
  });

  it('DB preferences overlay the local value once loaded', () => {
    const store = TestBed.inject(ConfigStore);
    TestBed.tick();
    dbPrefs.set({ appRefreshSeconds: 600 });
    TestBed.tick();
    expect(store.appRefreshSeconds()).toBe(600);
    expect(store.rsiOversoldThreshold()).toBe(30);
  });
});
