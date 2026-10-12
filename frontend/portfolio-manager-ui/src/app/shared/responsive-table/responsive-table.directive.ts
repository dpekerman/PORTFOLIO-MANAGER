import { Directive, ElementRef, OnDestroy, effect, inject, untracked } from '@angular/core';
import { BreakpointStore } from '../../core/store/breakpoint.store';

/** Unique suffix per directive instance so each table owns its own generated rules. */
let instanceCounter = 0;

/** Columns whose header text must never become a card label. */
const UNLABELLED_COLUMN = /^colconfig$|actions$/i;

/**
 * Turns an existing `<table mat-table>` into a stacked card list on handsets — without touching
 * the feature's cell templates.
 *
 * WHY a directive instead of a second, mobile-only template per page: the app has ~11 grids with
 * 7-38 columns each. Duplicating their cell templates for mobile would double the surface that has
 * to stay in sync with every formatting, masking or tooltip change. Here the same `<td>` elements
 * are reused and only their *presentation* changes:
 *
 * 1. `data-responsive="cards"` is set on the host while {@link BreakpointStore} reports a handset.
 *    `styles/_responsive-table.scss` hangs every card rule off that attribute, so desktop CSS is
 *    byte-for-byte unaffected.
 * 2. Because the header row is hidden in card mode, each cell needs its own label. The directive
 *    reads the visible text of every `th.mat-column-<key>` and injects ONE stylesheet with
 *    `td.mat-column-<key>::before { content: "<label>" }` rules, scoped to this table instance.
 *    Generated content is exposed to assistive tech and costs no extra DOM nodes per row — with a
 *    few hundred rows an attribute-per-cell approach would be far more expensive.
 *
 * Invariants:
 * - zero effect on tablet/desktop: no `data-responsive`, no stylesheet, no observer work;
 * - never mutates feature templates or row data; cells are only ever re-labelled;
 * - labels follow the rendered header row, so hiding/reordering columns keeps them correct;
 * - the stylesheet and the observer are torn down with the directive.
 */
@Directive({
  selector: 'table[mat-table][appResponsiveTable]',
  host: {
    '[attr.data-responsive]': 'cardMode() ? "cards" : null',
    '[attr.data-responsive-table]': 'instanceId',
  },
})
export class ResponsiveTableDirective implements OnDestroy {
  private readonly table = inject<ElementRef<HTMLTableElement>>(ElementRef);
  private readonly breakpoints = inject(BreakpointStore);

  /** Identifies this table in the generated selectors. */
  protected readonly instanceId = `rt${++instanceCounter}`;
  protected readonly cardMode = this.breakpoints.isHandset;

  private styleEl: HTMLStyleElement | null = null;
  private headerObserver: MutationObserver | null = null;

  constructor() {
    effect(() => {
      const cards = this.cardMode();
      untracked(() => (cards ? this.startLabelSync() : this.stopLabelSync()));
    });
  }

  ngOnDestroy(): void {
    this.stopLabelSync();
  }

  /** Writes the label rules once and keeps them in sync with the rendered header row. */
  private startLabelSync(): void {
    // The app is client-only, but a directive is cheap to make bootstrap-safe.
    if (typeof document === 'undefined') return;
    this.syncLabels();
    if (this.headerObserver || typeof MutationObserver === 'undefined') return;

    // The table itself is observed rather than `tHead`: Material creates `<thead>` only when it
    // first renders the header row, which happens *after* this directive's effect runs, so an
    // observer bound to `tHead` would never see the initial labels. `syncLabels` is a no-op write
    // when the generated CSS is unchanged, so row-level churn costs one small DOM read.
    this.headerObserver = new MutationObserver(() => this.syncLabels());
    this.headerObserver.observe(this.table.nativeElement, {
      childList: true,
      subtree: true,
      characterData: true,
    });
  }

  private stopLabelSync(): void {
    this.headerObserver?.disconnect();
    this.headerObserver = null;
    this.styleEl?.remove();
    this.styleEl = null;
  }

  /** Rebuilds the `::before` rules from the header cells currently rendered. */
  private syncLabels(): void {
    const css = this.buildCss();
    if (!this.styleEl) {
      this.styleEl = document.createElement('style');
      this.styleEl.dataset['responsiveTable'] = this.instanceId;
      document.head.appendChild(this.styleEl);
    }
    if (this.styleEl.textContent !== css) this.styleEl.textContent = css;
  }

  private buildCss(): string {
    const scope = `table[data-responsive-table="${this.instanceId}"][data-responsive='cards']`;
    const rules: string[] = [];

    for (const th of Array.from(
      this.table.nativeElement.querySelectorAll<HTMLTableCellElement>('thead th'),
    )) {
      const key = columnKey(th);
      if (!key || UNLABELLED_COLUMN.test(key)) continue;
      const label = headerText(th);
      if (!label) continue;
      rules.push(
        `${scope} td.${escapeIdent(`mat-column-${key}`)}::before{content:"${escapeString(label)}"}`,
      );
    }
    return rules.join('\n');
  }
}

/** Reads the `mat-column-<key>` class Material puts on every header/data cell of a column. */
function columnKey(cell: Element): string | null {
  for (const cls of Array.from(cell.classList)) {
    if (cls.startsWith('mat-column-')) return cls.slice('mat-column-'.length);
  }
  return null;
}

/**
 * Visible text of a header cell.
 *
 * `mat-sort-header` wraps the caption in a button next to an arrow container, and some headers
 * embed `<mat-icon>` ligatures whose text ("settings", "arrow_upward") would otherwise leak into
 * the label — both are stripped from a detached clone so the live DOM is never touched.
 */
function headerText(th: HTMLTableCellElement): string {
  const clone = th.cloneNode(true) as HTMLElement;
  clone
    .querySelectorAll(
      '.mat-sort-header-arrow, mat-icon, .cdk-visually-hidden, [aria-hidden="true"]',
    )
    .forEach((el) => el.remove());
  return (clone.textContent ?? '').replace(/\s+/g, ' ').trim();
}

/** CSS identifier escaping (`CSS.escape` where available, conservative fallback otherwise). */
function escapeIdent(value: string): string {
  if (typeof CSS !== 'undefined' && typeof CSS.escape === 'function') return CSS.escape(value);
  return value.replace(/[^\w-]/g, (ch) => `\\${ch}`);
}

/** Escapes a CSS string literal: backslashes, double quotes and newlines. */
function escapeString(value: string): string {
  return value.replace(/\\/g, '\\\\').replace(/"/g, '\\"').replace(/\r?\n/g, '\\A ');
}
