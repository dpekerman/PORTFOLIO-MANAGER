/**
 * Generates golden Final Action results from the REAL Angular DecisionEngineService.
 *
 * The backend FinalActionEngine is a port of that service; this script is the executable specification.
 * It bundles the unmodified engine source with esbuild, runs a seeded corpus (boundary-heavy, every role,
 * watchlist / portfolio / scanner paths, account and price-structure contexts) through it and writes a
 * gzipped JSONL file the .NET parity tests replay with exact equality.
 *
 * Run from the frontend folder:  node scripts/generate-final-action-golden.mjs
 * Regenerate only when the Angular rules intentionally change (then port the change, never the reverse).
 */
import { build } from 'esbuild';
import { mkdirSync, mkdtempSync, writeFileSync } from 'node:fs';
import { gzipSync } from 'node:zlib';
import { tmpdir } from 'node:os';
import { join, resolve, dirname } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const engineSrc = resolve(here, '../src/app/core/services/decision-engine.service.ts');
const outFile = resolve(here, '../../../backend/PortfolioManager.Tests/Fixtures/final-action-golden.jsonl.gz');

const work = mkdtempSync(join(tmpdir(), 'fa-golden-'));
const stub = join(work, 'angular-core-stub.mjs');
writeFileSync(stub, 'export const Injectable = () => (target) => target;\n');
const bundle = join(work, 'engine.mjs');
await build({
  entryPoints: [engineSrc],
  bundle: true,
  format: 'esm',
  platform: 'node',
  outfile: bundle,
  alias: { '@angular/core': stub },
  tsconfigRaw: { compilerOptions: { experimentalDecorators: true } },
  logLevel: 'error',
});
const { DecisionEngineService } = await import(pathToFileURL(bundle).href);
const engine = new DecisionEngineService();
console.debug = () => {};

// ── Seeded PRNG so the corpus is reproducible ────────────────────────────────
let seed = 0x5eed1234;
const rnd = () => {
  seed |= 0;
  seed = (seed + 0x6d2b79f5) | 0;
  let t = Math.imul(seed ^ (seed >>> 15), 1 | seed);
  t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
  return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
};
const pick = (arr) => arr[Math.floor(rnd() * arr.length)];
const between = (a, b) => a + rnd() * (b - a);
const r2 = (n) => Math.round(n * 100) / 100;

const RSI_EDGES = [8, 18, 25, 29.9, 30, 33, 34.9, 35, 35.1, 39.9, 40, 45, 49.9, 50, 55, 55.1, 60, 64.99, 65, 65.1, 69.9, 70, 70.1, 75, 82, 91];
const ROLES = [null, 'Core', 'Strategic', 'Strategic-Income', 'Swing', 'Speculative', 'Options', 'Unknown'];
const STATUSES = ['Confirmed', 'EodConfirm', 'EarlyWarning', 'Neutral'];
const LEVEL_STATES = [
  'NONE', 'SUPPORT_TEST', 'SUPPORT_RECLAIM', 'BOUNCE_CONFIRMED', 'APPROACHING_SUPPORT', 'RESISTANCE_TEST',
  'APPROACHING_RESISTANCE', 'BREAKOUT_WATCH', 'BREAKOUT_CONFIRMED', 'FAILED_BREAKOUT', 'BREAKOUT_FAILED',
  'SUPPORT_BROKEN', 'BREAKDOWN_CONFIRMED', 'CHANNEL_BROKEN', 'CHANNEL_SUPPORT_BROKEN', 'WEDGE_BREAKDOWN',
  'TIGHT_WEDGE_BREAKDOWN', 'RISING_WEDGE_BREAKDOWN', 'TIGHT_RISING_WEDGE_BREAKDOWN', 'TRANSITION',
];
const PATTERN_TYPES = ['NONE', 'FALLING_WEDGE', 'RISING_WEDGE', 'ASCENDING_CHANNEL', 'DESCENDING_CHANNEL', 'TIGHT_FALLING_WEDGE', 'TRIANGLE'];
const PATTERN_STATES = ['NONE', 'DEVELOPING', 'TIGHTENING', 'NEAR_APEX', 'BREAKOUT', 'BREAKDOWN', 'CHANNEL_BROKEN', 'INTACT'];
const FIB_ZONES = ['', 'Value Zone', 'Key Fib Support', 'Trend Damage', 'Above 38.2', 'Shallow'];
const FIB_STATUSES = ['', 'Reclaimed 61.8', 'Testing 61.8', 'Below 78.6'];
const TREND_SHIFTS = ['Waiting', '🟢 Bull Turn', '🟡 Stabilizing', '🔴 Still Falling', '🟢 Bear Turn', '🔴 Still Rising'];
const ACCOUNTS = [null, 'Margin_D_TD', 'TFSA_L_RBC', 'tfsa_d_td', 'Corp_TD', 'RRSP'];

function makePriceStructure() {
  if (rnd() < 0.12) return null;
  const keyLevelState = pick(LEVEL_STATES);
  const primaryPatternState = pick(PATTERN_STATES);
  return {
    symbol: 'X',
    label: 'x',
    keyLevelRole: pick(['SUPPORT', 'RESISTANCE', 'NONE', 'TRANSITION']),
    keyLevelState,
    primaryPatternType: pick(PATTERN_TYPES),
    primaryPatternState,
    hasHardStructuralNegative:
      ['BREAKDOWN', 'CHANNEL_BROKEN'].includes(primaryPatternState) ||
      ['SUPPORT_BROKEN', 'BREAKDOWN_CONFIRMED', 'FAILED_BREAKOUT'].includes(keyLevelState),
  };
}

function makeScan(i) {
  const close = r2(between(4, 480));
  const rel = () => pick([0.9, 0.97, 0.99, 1, 1.004, 1.0051, 1.01, 1.03, 1.1, 0.8, between(0.85, 1.15)]);
  const rsi = rnd() < 0.55 ? pick(RSI_EDGES) : r2(between(5, 95));
  const sigAvail = rnd() < 0.9;
  const rsiSignal = sigAvail ? r2(rsi + pick([-6, -2, -0.01, 0, 0.01, 2, 6, between(-8, 8)])) : null;
  const open = rnd() < 0.2 ? 0 : r2(close * pick([0.97, 0.99, 1, 1.01, 1.02, 1.04, between(0.95, 1.06)]));
  const prevClose = rnd() < 0.08 ? 0 : r2(close * pick([0.96, 0.99, 1, 1.01, between(0.94, 1.06)]));
  const noRange = rnd() < 0.25;
  const dayHigh = noRange ? 0 : r2(close * between(1.0, 1.04));
  const dayLow = noRange ? 0 : r2(close * between(0.96, 1.0));
  const fib = rnd() < 0.55;
  const f50 = fib ? r2(close * between(0.95, 1.1)) : 0;
  const f618 = fib ? r2(close * between(0.9, 1.08)) : 0;
  const f786 = fib ? r2(close * between(0.85, 1.05)) : 0;
  return {
    symbol: `SYM${i}`,
    rsi,
    currentPrice: close,
    changePercent: r2(pick([-7, -3, -1.01, -0.5, 0, 0.5, 1.01, 3, 5.01, 8, between(-6, 6)])),
    dayHigh,
    dayLow,
    openPrice: open,
    previousClose: prevClose,
    volume: Math.round(between(1e5, 5e6)),
    volumeRatio: r2(pick([0, 0.6, 0.99, 1, 1.19, 1.2, 1.29, 1.3, 2.2, between(0.3, 3)])),
    rsiSignal,
    rsiSignalAvailable: sigAvail,
    macdHistogram: r2(between(-2, 2)),
    macdHistDelta: r2(pick([-0.5, -0.001, 0, 0.001, 0.4, between(-1, 1)])),
    ema9Price: rnd() < 0.05 ? 0 : r2(close * rel()),
    ema10Price: rnd() < 0.05 ? 0 : r2(close * rel()),
    ema20Price: rnd() < 0.05 ? 0 : r2(close * rel()),
    sma20Price: rnd() < 0.05 ? 0 : r2(close * pick([0.99, 0.995, 1, 1.009, 1.0101, 1.02, rel()])),
    sma50Price: rnd() < 0.05 ? 0 : r2(close * rel()),
    sma200: rnd() < 0.2 ? 0 : r2(close * rel()),
    status: pick(STATUSES),
    scanType: pick(['Oversold', 'Overbought']),
    analystTargetPrice: rnd() < 0.3 ? 0 : r2(close * pick([0.9, 0.99, 1, 1.01, 1.2, 1.5])),
    fib38_2: fib ? r2(close * between(0.95, 1.1)) : 0,
    fib50: f50,
    fib61_8: f618,
    fib78_6: f786,
    fibZone: pick(FIB_ZONES),
    fibStatus: pick(FIB_STATUSES),
    rsiDelta1D: rnd() < 0.15 ? null : r2(pick([-4, -0.01, 0, 0.01, 3, between(-5, 5)])),
    trendSetup200: pick(['Trend-Aligned', 'Counter-Trend', '']),
    trendShift: pick(TREND_SHIFTS),
    chaseRisk: rnd() < 0.12,
    week52High: rnd() < 0.1 ? 0 : r2(close * between(1.0, 1.4)),
    priceStructure: makePriceStructure(),
  };
}

function makeValueCtx() {
  const m = rnd();
  if (m < 0.4) return undefined;
  const ctx = {};
  if (rnd() < 0.7) ctx.valueScore = pick([null, 0, 4.9, 5, 7, 10]);
  if (rnd() < 0.4) ctx.valueTrapWarning = rnd() < 0.5;
  if (rnd() < 0.25) ctx.buyScore = pick([0, 2, 3, 4, 5]);
  return ctx;
}

function makeItemCtx() {
  if (rnd() < 0.15) return undefined;
  return {
    accountType: pick(ACCOUNTS),
    unrealizedGainPct: pick([null, -10, 0, 19.99, 20, 25, 29.99, 30, 45, 49.99, 50, 80]),
    holdingDays: pick([null, 5, 100, 183, 184, 400]),
    distanceFrom52WeekHighPct: pick([null, -30, -3, -2.01, -2, -1, 0]),
    positionSizePct: pick([null, 0.5, 1.99, 2, 3.99, 4, 9]),
    riskControlClosePct: pick([null, 0, 10, 24.99, 25, 37.5, 49.5, 50, 100]),
    decisionSource: pick([null, 'App Signal', 'Manual', 'Risk Control - Trim', 'Risk Control']),
  };
}

const strip = (v) => JSON.parse(JSON.stringify(v ?? null));

function summarise(d) {
  const cls = engine.actionCenterClassification(d.finalActionClass);
  return {
    finalAction: d.finalAction,
    trendSetup: d.trendSetup,
    trendSetupReason: d.trendSetupReason,
    momentumShift: d.momentumShift,
    momentumShiftReason: d.momentumShiftReason,
    baseAction: d.baseAction,
    gapStatus: d.gapStatus,
    gapScore: d.gapScore,
    gapPct: d.gapPct,
    hoverDescription: d.hoverDescription,
    finalActionClass: d.finalActionClass,
    trendSetupClass: d.trendSetupClass,
    momentumShiftClass: d.momentumShiftClass,
    finalActionReason: d.finalActionReason ?? null,
    diagnostics: d.watchlistDiagnostics ? strip(d.watchlistDiagnostics) : null,
    severity: cls.severity,
    priority: cls.priority,
  };
}

/** Healthy-trend / oversold-bounce scans that reach the rare entry and Uptrend rule paths. */
function makeBullishScan(i) {
  const s = makeScan(i);
  const close = s.currentPrice;
  const oversoldBounce = rnd() < 0.35;
  s.rsi = oversoldBounce ? r2(between(22, 34.9)) : r2(between(40, 68));
  s.rsiSignalAvailable = true;
  s.rsiSignal = r2(s.rsi - pick([0, 0.5, 2, 4]));
  s.ema10Price = r2(close * between(0.985, 1.0));
  s.ema20Price = r2(close * between(0.95, 0.98));
  s.ema9Price = r2(close * between(0.99, 1.0));
  s.sma50Price = r2(close * between(0.9, 0.97));
  s.sma20Price = r2(close * pick([0.995, 1, 1.004, between(0.97, 1.0)]));
  s.sma200 = r2(close * between(0.7, 0.95));
  s.macdHistDelta = r2(between(0.01, 0.6));
  s.volumeRatio = r2(between(1.0, 2.5));
  s.dayHigh = r2(close * between(1.0, 1.02));
  s.dayLow = r2(close * between(0.97, 0.995));
  s.chaseRisk = false;
  s.fibZone = pick(['', 'Value Zone', 'Key Fib Support']);
  s.fibStatus = pick(['', 'Reclaimed 61.8']);
  s.trendShift = pick(['🟢 Bull Turn', 'Waiting', '🟡 Stabilizing']);
  if (rnd() < 0.7) {
    s.openPrice = r2(close * between(0.995, 1.005));
    s.previousClose = r2(close * between(0.99, 1.005));
  }
  if (s.priceStructure && rnd() < 0.8) {
    s.priceStructure = {
      ...s.priceStructure,
      keyLevelState: pick(['BREAKOUT_CONFIRMED', 'SUPPORT_RECLAIM', 'BOUNCE_CONFIRMED', 'SUPPORT_TEST', 'APPROACHING_SUPPORT', 'NONE']),
      keyLevelRole: pick(['SUPPORT', 'SUPPORT', 'RESISTANCE']),
      primaryPatternState: pick(['BREAKOUT', 'DEVELOPING', 'TIGHTENING', 'NONE']),
      primaryPatternType: pick(['FALLING_WEDGE', 'ASCENDING_CHANNEL', 'NONE']),
      hasHardStructuralNegative: false,
    };
  }
  return s;
}

const cases = [];
const add = (kind, role, scan, valueCtx, itemCtx) => {
  let result;
  if (kind === 'watchlist') result = engine.translateForWatchlist(scan, role, valueCtx);
  else if (kind === 'portfolio') result = engine.translateForPortfolio(scan, role, true, itemCtx);
  else result = engine.translateForRsiScanner(scan);
  cases.push({ id: cases.length, kind, role, scan, valueCtx: strip(valueCtx), itemCtx: strip(itemCtx), expected: summarise(result) });
};

for (let i = 0; i < 2500; i++) add('watchlist', pick(ROLES), makeScan(i), makeValueCtx(), undefined);
for (let i = 0; i < 2500; i++) add('watchlist', pick(ROLES), makeBullishScan(5000 + i), makeValueCtx(), undefined);
for (let i = 0; i < 2500; i++) add('portfolio', pick(ROLES), makeScan(10000 + i), undefined, makeItemCtx());
for (let i = 0; i < 2000; i++) add('portfolio', pick(ROLES), makeBullishScan(15000 + i), undefined, makeItemCtx());
for (let i = 0; i < 500; i++) add('scanner', null, rnd() < 0.5 ? makeScan(20000 + i) : makeBullishScan(20000 + i), undefined, undefined);

const jsonl = cases.map((c) => JSON.stringify(c)).join('\n');
mkdirSync(dirname(outFile), { recursive: true });
writeFileSync(outFile, gzipSync(Buffer.from(jsonl, 'utf8'), { level: 9 }));

const tally = {};
for (const c of cases) tally[c.expected.finalAction] = (tally[c.expected.finalAction] ?? 0) + 1;
console.log(`wrote ${cases.length} cases -> ${outFile}`);
console.log('distinct final actions:', Object.keys(tally).length);
console.log(JSON.stringify(tally, null, 1));
