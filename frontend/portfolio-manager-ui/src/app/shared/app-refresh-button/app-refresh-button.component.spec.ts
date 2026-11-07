import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AppRefreshStore } from '../../core/store/app-refresh.store';
import { AppRefreshButtonComponent } from './app-refresh-button.component';

describe('AppRefreshButtonComponent', () => {
  const isRefreshing = signal(false);
  let calls: string[];

  beforeEach(async () => {
    isRefreshing.set(false);
    calls = [];
    await TestBed.configureTestingModule({
      imports: [AppRefreshButtonComponent],
      providers: [
        {
          provide: AppRefreshStore,
          useValue: { isRefreshing, refreshAll: (r: string) => calls.push(r) },
        },
      ],
    }).compileComponents();
  });

  function button(fixture: { nativeElement: HTMLElement }): HTMLButtonElement {
    return fixture.nativeElement.querySelector('button') as HTMLButtonElement;
  }

  it('starts a user-click refresh and emits refreshed', async () => {
    const fixture = TestBed.createComponent(AppRefreshButtonComponent);
    let emitted = 0;
    fixture.componentInstance.refreshed.subscribe(() => emitted++);
    await fixture.whenStable();

    button(fixture).click();

    expect(calls).toEqual(['user-click']);
    expect(emitted).toBe(1);
  });

  it('is disabled while a refresh is running', async () => {
    isRefreshing.set(true);
    const fixture = TestBed.createComponent(AppRefreshButtonComponent);
    await fixture.whenStable();

    expect(button(fixture).disabled).toBe(true);
  });

  it('is disabled while the host page is busy', async () => {
    const fixture = TestBed.createComponent(AppRefreshButtonComponent);
    fixture.componentRef.setInput('busy', true);
    await fixture.whenStable();

    expect(button(fixture).disabled).toBe(true);
  });
});
