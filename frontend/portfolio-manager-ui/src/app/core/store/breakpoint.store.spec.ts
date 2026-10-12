import { BreakpointObserver, BreakpointState } from '@angular/cdk/layout';
import { TestBed } from '@angular/core/testing';
import { BehaviorSubject } from 'rxjs';
import {
  BreakpointStore,
  DESKTOP_QUERY,
  HANDSET_QUERY,
  TABLET_QUERY,
} from './breakpoint.store';

/** Builds the `BreakpointState` the CDK emits for the one query that currently matches. */
function state(match: typeof HANDSET_QUERY | typeof TABLET_QUERY | typeof DESKTOP_QUERY) {
  return {
    matches: true,
    breakpoints: {
      [HANDSET_QUERY]: match === HANDSET_QUERY,
      [TABLET_QUERY]: match === TABLET_QUERY,
      [DESKTOP_QUERY]: match === DESKTOP_QUERY,
    },
  } as BreakpointState;
}

describe('BreakpointStore', () => {
  let emissions: BehaviorSubject<BreakpointState>;
  let observed: string[][];

  beforeEach(() => {
    emissions = new BehaviorSubject(state(DESKTOP_QUERY));
    observed = [];
    TestBed.configureTestingModule({
      providers: [
        {
          provide: BreakpointObserver,
          useValue: {
            observe: (queries: string[]) => {
              observed.push(queries);
              return emissions.asObservable();
            },
          },
        },
      ],
    });
  });

  function store(): InstanceType<typeof BreakpointStore> {
    const s = TestBed.inject(BreakpointStore);
    TestBed.tick();
    return s;
  }

  it('observes the three app breakpoints exactly once', () => {
    store();
    expect(observed).toEqual([[HANDSET_QUERY, TABLET_QUERY, DESKTOP_QUERY]]);
  });

  it('reports desktop for a wide viewport', () => {
    const s = store();
    expect(s.isDesktop()).toBe(true);
    expect(s.isTablet()).toBe(false);
    expect(s.isHandset()).toBe(false);
  });

  it('switches to handset when the phone query matches', () => {
    const s = store();
    emissions.next(state(HANDSET_QUERY));
    expect(s.isHandset()).toBe(true);
    expect(s.isTablet()).toBe(false);
    expect(s.isDesktop()).toBe(false);
  });

  it('switches to tablet when the tablet query matches', () => {
    const s = store();
    emissions.next(state(TABLET_QUERY));
    expect(s.isTablet()).toBe(true);
    expect(s.isHandset()).toBe(false);
    expect(s.isDesktop()).toBe(false);
  });

  it('tracks every change, not just the first', () => {
    const s = store();
    emissions.next(state(HANDSET_QUERY));
    emissions.next(state(DESKTOP_QUERY));
    expect(s.isHandset()).toBe(false);
    expect(s.isDesktop()).toBe(true);
  });

  it('stops listening once the injector is destroyed', () => {
    const s = store();
    TestBed.resetTestingModule();
    emissions.next(state(HANDSET_QUERY));
    expect(s.isHandset()).toBe(false);
  });
});
