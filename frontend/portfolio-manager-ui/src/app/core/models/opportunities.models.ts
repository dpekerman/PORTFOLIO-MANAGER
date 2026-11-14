// PM-029 Trading Opportunities — mirrors backend Services/Opportunities/OpportunityDtos.cs + OpportunityModels.cs.
// Every value is computed server-side per refresh and stored in an immutable snapshot; nothing here is a prototype literal.
import { FinalActionResult } from './portfolio.models';

export type ScoreStatus = 'Verified' | 'Provisional' | 'Insufficient' | 'Unavailable';
export type ScoreBand = 'None' | 'Weak' | 'Caution' | 'Moderate' | 'Attractive' | 'Strong';
export type ComponentState = 'Observed' | 'Partial' | 'Missing';
export type ScorePerspective = 'Swing' | 'Strategic';
export type SectorHeatStatus =
  | 'Matched'
  | 'Unmapped'
  | 'Stale'
  | 'InsufficientObservations'
  | 'CurrencyMismatch'
  | 'PossibleCorporateAction'
  | 'Unclassified';
export type SectorHeatDirection = 'None' | 'Positive' | 'Neutral' | 'Negative';
export type SetupKind = 'ValidCandidate' | 'NotActionable' | 'Unavailable';
export type RiskCategory = 'Unavailable' | 'Low' | 'Medium' | 'High';
export type FitStatus = 'Eligible' | 'Review' | 'NoAdd' | 'Unavailable';
export type OpportunityRowStatus = 'Ok' | 'IdentityUnresolved' | 'NoScan' | 'StaleScan';
export type FinalActionSummaryKind = 'Single' | 'Multiple' | 'None';

export interface OpportunityScoreSummary {
  score: number | null;
  completenessPct: number;
  status: ScoreStatus;
  band: ScoreBand;
  reasonCodes: string[];
}

export interface OpportunitySectorHeat {
  /** Displayed 20D difference (0.1 pp, signed); null when unavailable. */
  diffPp: number | null;
  diff60Pp: number | null;
  stockReturnPct: number | null;
  benchmarkReturnPct: number | null;
  status: SectorHeatStatus;
  direction: SectorHeatDirection;
  intensity: number;
  benchmarkSymbol: string | null;
}

export interface OpportunitySetup {
  kind: SetupKind;
  entry: number | null;
  stop: number | null;
  target: number | null;
  targetSource: string | null;
  rewardRisk: number | null;
  riskPct: number | null;
  rewardPct: number | null;
  stopDistanceAtr: number | null;
  currency: string | null;
  reasonCodes: string[];
}

export interface OpportunityRisk {
  category: RiskCategory;
  riskToStopPct: number | null;
  atrPct: number | null;
  hardStructuralNegative: boolean;
  basis: string;
}

export interface OpportunityFit {
  status: FitStatus;
  reasonCodes: string[];
  weightPct: number | null;
  positionLimitPct: number | null;
  positionHeadroomPct: number | null;
  sectorWeightPct: number | null;
  sectorTargetPct: number | null;
  sectorHeadroomPct: number | null;
}

/** Grid summary of the authoritative Final Action decisions (never a new decision). */
export interface OpportunityFinalAction {
  kind: FinalActionSummaryKind;
  label: string | null;
  finalActionId: string | null;
  category: string | null;
  finalActionClass: string | null;
  isActionable: boolean;
  evaluationState: string | null;
  evidenceHealth: string | null;
  reason: string | null;
  decisionCount: number;
  distinctLabels: string[];
}

export interface OpportunityIndicators {
  price: number | null;
  changePct: number | null;
  rsi14: number | null;
  rsiSignal: number | null;
  macdHistogram: number | null;
  ema20: number | null;
  sma50: number | null;
  sma200: number | null;
  atr: number | null;
  volumeRatio: number | null;
  week52High: number | null;
  week52Low: number | null;
  dynamicStopLoss: number | null;
  trendShift: string | null;
  maStructure: string | null;
  fibZone: string | null;
  channelState: string | null;
  rs20Pp: number | null;
  rs60Pp: number | null;
}

export interface OpportunityFreshness {
  dataAsOfDate: string | null;
  expectedSession: string;
  scannedAtUtc: string | null;
  isStale: boolean;
  fundamentalsAsOfUtc: string | null;
  fundamentalsOrigin: string | null;
}

export interface OpportunityRow {
  symbol: string;
  companyName: string;
  analysisTicker: string | null;
  usesUnderlying: boolean;
  exchange: string;
  currency: string | null;
  sources: string[];
  owned: boolean;
  accounts: string[];
  roles: string[];
  watchlistRole: string | null;
  sector: string | null;
  industry: string | null;
  sectorSource: string;
  swing: OpportunityScoreSummary;
  strategic: OpportunityScoreSummary;
  sectorHeat: OpportunitySectorHeat;
  setup: OpportunitySetup;
  risk: OpportunityRisk;
  fit: OpportunityFit;
  finalAction: OpportunityFinalAction;
  indicators: OpportunityIndicators;
  freshness: OpportunityFreshness;
  rowStatus: OpportunityRowStatus;
}

export interface OpportunityUniverseCounts {
  watchlist: number;
  portfolio: number;
  tsx60: number;
  total: number;
}

export interface OpportunityLastAttempt {
  atUtc: string | null;
  status: string | null;
  errorSummary: string | null;
}

export interface OpportunitySnapshot {
  snapshotId: number | null;
  scoringVersion: string;
  disclaimer: string;
  computedAtUtc: string | null;
  finalActionComputedAtUtc: string | null;
  dataAsOfSession: string | null;
  status: string | null;
  errorSummary: string | null;
  lastAttempt: OpportunityLastAttempt | null;
  counts: OpportunityUniverseCounts;
  isStale: boolean;
  portfolioChangedSinceEvaluation: boolean;
  refreshInFlight: boolean;
  rows: OpportunityRow[];
}

// ── Detail (drawer) ─────────────────────────────────────────────────────────

export interface ScoreSubItem {
  code: string;
  label: string;
  max: number;
  points: number;
  available: boolean;
  detail: string | null;
}

export interface ScoreComponent {
  code: string;
  name: string;
  max: number;
  essential: boolean;
  state: ComponentState;
  awarded: number;
  availableMax: number;
  items: ScoreSubItem[];
  missingReasons: string[];
  note: string | null;
}

export interface ScoreResult {
  perspective: ScorePerspective;
  scoringVersion: string;
  score: number | null;
  normalizedScore: number | null;
  awardedTotal: number;
  availableMax: number;
  completenessPct: number;
  status: ScoreStatus;
  band: ScoreBand;
  components: ScoreComponent[];
  reasonCodes: string[];
}

export interface SectorWindowResult {
  sessions: number;
  status: SectorHeatStatus;
  startSession: string | null;
  endSession: string | null;
  stockReturnPct: number | null;
  benchmarkReturnPct: number | null;
  diffPp: number | null;
  displayDiffPp: number | null;
  direction: SectorHeatDirection;
  intensity: number;
  stockObservations: number;
  benchmarkObservations: number;
  expectedObservations: number;
  isAvailable: boolean;
}

export interface SectorRelativeResult {
  status: SectorHeatStatus;
  benchmarkSymbol: string | null;
  benchmarkName: string | null;
  expectedSession: string;
  d20: SectorWindowResult;
  d60: SectorWindowResult;
  methodology: string;
}

export interface TradeSetupResult extends OpportunitySetup {
  isScorable: boolean;
}

/** "Your saved risk plan" — display only, never scored. */
export interface SavedRiskPlan {
  account: string;
  averageCost: number | null;
  plannedStop: number | null;
  target: number | null;
  rewardRisk: number | null;
  stopType: string;
  currency: string | null;
  updatedAtUtc: string;
}

export interface OpportunityEvidence {
  scoringVersion: string;
  disclaimer: string;
  sectorMethodology: string;
  historySource: string;
  benchmarkSymbol: string | null;
  benchmarkName: string | null;
  sectorSource: string;
  scanAsOfUtc: string | null;
  scanTradingDate: string | null;
  expectedSession: string;
  fundamentalsOrigin: string | null;
  fundamentalsAsOfUtc: string | null;
  earningsYieldBasis: string | null;
  fcfYieldBasis: string | null;
  capitalEfficiencyBasis: string | null;
  allocationStatus: string | null;
  portfolioUpdatedAtUtc: string | null;
  finalActionEvaluationId: string;
  finalActionComputedAtUtc: string;
  warnings: string[];
}

export interface OpportunityDetail {
  snapshotId: number;
  row: OpportunityRow;
  swingScore: ScoreResult;
  strategicScore: ScoreResult;
  sectorRelative: SectorRelativeResult | null;
  setup: TradeSetupResult;
  /** Every original authoritative account/lot decision, unchanged. */
  decisions: FinalActionResult[];
  savedPlans: SavedRiskPlan[];
  evidence: OpportunityEvidence;
}

// ── Config / reference ──────────────────────────────────────────────────────

/** Published scoring parameters (pm029-v1); the UI never duplicates thresholds. */
export interface OpportunityConfig {
  version: string;
  disclaimer: string;
  sectorMethodology: string;
  strongMin: number;
  attractiveMin: number;
  moderateMin: number;
  cautionMin: number;
  minCompletenessPct: number;
  shortWindowSessions: number;
  longWindowSessions: number;
  neutralBandPp: number;
  intensity2Pp: number;
  intensity3Pp: number;
  actionableRewardRisk: number;
  fundamentalsMaxAgeDays: number;
}

export interface IndexConstituent {
  id: number;
  indexCode: string;
  symbol: string;
  companyName: string;
  effectiveFrom: string;
  effectiveTo: string | null;
  source: string;
  sourceAsOfDate: string;
  verifiedAtUtc: string | null;
  verifiedBy: string | null;
}

export interface SectorBenchmarkMapping {
  id: number;
  sector: string;
  market: string;
  benchmarkSymbol: string;
  benchmarkName: string;
  currency: string;
  /** JSON array of provider sector names. */
  providerSectorAliasesJson: string;
  isActive: boolean;
  notes: string | null;
  source: string | null;
  sourceAsOfDate: string | null;
  updatedAtUtc: string;
  updatedBy: string | null;
}

export interface OpportunityReference {
  constituents: IndexConstituent[];
  benchmarks: SectorBenchmarkMapping[];
}

export interface IndexConstituentRequest {
  symbol: string;
  companyName: string | null;
  indexCode: string | null;
  effectiveFrom: string;
  source: string;
  sourceAsOfDate: string;
}

export interface SectorBenchmarkRequest {
  id: number | null;
  sector: string;
  market: string;
  benchmarkSymbol: string;
  benchmarkName: string | null;
  currency: string;
  providerSectorAliases: string[] | null;
  isActive: boolean;
  notes: string | null;
  source: string | null;
  sourceAsOfDate: string | null;
}
