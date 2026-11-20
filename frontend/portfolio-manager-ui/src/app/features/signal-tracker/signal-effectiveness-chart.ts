import { SignalEffectivenessResultRow } from '../../core/models/portfolio.models';

export type EffHorizon = '5D' | '20D' | '1M';

export interface EffBar {
  key: EffHorizon;
  /** Plotted value in percent: direction-adjusted for Sell signals, raw otherwise. Null when no period has completed. */
  value: number | null;
  n: number;
  /** Raw stock return, kept separate from the plotted value. */
  raw: number | null;
  adjusted: boolean;
  negative: boolean;
  /** Position of the bar's lower edge and its height as a percentage of the plot height. */
  bottomPct: number;
  heightPct: number;
  label: string;
  /** One-decimal label for narrow screens, where the full figure would collide with neighbouring bars. */
  shortLabel: string;
}

export interface EffGroup {
  signalType: string;
  label: string;
  direction: string;
  eligible: number;
  bars: EffBar[];
  ariaLabel: string;
}

export interface EffTick {
  value: number;
  label: string;
  bottomPct: number;
}

export interface EffChart {
  groups: EffGroup[];
  ticks: EffTick[];
  zeroPct: number;
  hasData: boolean;
}

const STEPS = [0.5, 1, 2, 5, 10, 20, 25, 50, 100];
const MIN_VISIBLE_PCT = 0.8;

export function formatPct(value: number): string {
  return `${value > 0 ? '+' : ''}${value.toFixed(2)}%`;
}

function pick(row: SignalEffectivenessResultRow, key: EffHorizon) {
  switch (key) {
    case '5D':
      return { n: row.observations5D, raw: row.rawAvg5D, adjusted: row.adjustedAvg5D };
    case '20D':
      return { n: row.observations20D, raw: row.rawAvg20D, adjusted: row.adjustedAvg20D };
    default:
      return { n: row.observations1M, raw: row.rawAvg1M, adjusted: row.adjustedAvg1M };
  }
}

/** A "nice" axis step so that roughly four ticks span the data. */
function niceStep(span: number): number {
  const target = span / 4;
  return STEPS.find((s) => s >= target) ?? STEPS[STEPS.length - 1];
}

/**
 * Pure geometry for the grouped bar chart of average returns by signal type and period. It plots only completed
 * periods (n > 0), never a missing result as zero, and uses the direction-adjusted figure for Sell-direction signals
 * so a fall after an exit reads as favourable; the raw return stays available in each group's description.
 */
export function buildEffectivenessChart(rows: readonly SignalEffectivenessResultRow[]): EffChart {
  const plotted = rows.flatMap((r) =>
    (['5D', '20D', '1M'] as const).map((key) => {
      const p = pick(r, key);
      const value = p.n > 0 ? (p.adjusted ?? p.raw) : null;
      return { row: r, key, n: p.n, raw: p.n > 0 ? p.raw : null, value, adjusted: p.adjusted !== null && r.direction === 'Sell' };
    }),
  );
  const values = plotted.map((p) => p.value).filter((v): v is number => v !== null);
  const hasData = values.length > 0;

  const lo = Math.min(0, ...values);
  const hi = Math.max(0, ...values);
  const step = niceStep(Math.max(hi - lo, 1));
  const bottom = Math.floor(lo / step) * step;
  const top = Math.max(Math.ceil(hi / step) * step, bottom + step);
  const range = top - bottom;
  const pct = (v: number) => ((v - bottom) / range) * 100;

  const ticks: EffTick[] = [];
  for (let v = bottom; v <= top + 1e-9; v += step) {
    const value = Math.round(v * 100) / 100;
    ticks.push({ value, label: `${value > 0 ? '+' : ''}${value}%`, bottomPct: pct(value) });
  }
  const zeroPct = pct(0);

  const groups: EffGroup[] = rows.map((r) => {
    const bars: EffBar[] = plotted
      .filter((p) => p.row === r)
      .map((p) => {
        if (p.value === null) {
          return { key: p.key, value: null, n: 0, raw: null, adjusted: false, negative: false, bottomPct: zeroPct, heightPct: 0, label: 'n/a', shortLabel: 'n/a' };
        }
        const valuePct = pct(p.value);
        return {
          key: p.key,
          value: p.value,
          n: p.n,
          raw: p.raw,
          adjusted: p.adjusted,
          negative: p.value < 0,
          bottomPct: Math.min(zeroPct, valuePct),
          heightPct: Math.max(Math.abs(valuePct - zeroPct), MIN_VISIBLE_PCT),
          label: formatPct(p.value),
          shortLabel: `${p.value > 0 ? '+' : ''}${p.value.toFixed(1)}`,
        };
      });
    const parts = bars.map((b) =>
      b.value === null
        ? `${b.key}: insufficient history`
        : `${b.key}: ${formatPct(b.value)}${b.adjusted && b.raw !== null ? ` (raw ${formatPct(b.raw)})` : ''}, n=${b.n}`,
    );
    const direction = r.directionDefined
      ? r.direction === 'Sell'
        ? 'direction-adjusted, a fall counts as positive'
        : 'a rise counts as positive'
      : 'raw returns, direction not defined';
    return {
      signalType: r.signalType,
      label: r.label,
      direction: r.direction,
      eligible: r.eligible,
      bars,
      ariaLabel: `${r.label} (${direction}). ${parts.join('; ')}.`,
    };
  });

  return { groups, ticks, zeroPct, hasData };
}
