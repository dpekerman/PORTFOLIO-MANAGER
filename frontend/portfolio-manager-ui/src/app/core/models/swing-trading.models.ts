/** PM-033 Swing Trading: risk plans, user-recorded broker stops, trade episodes and the three workspace tabs. */

export type RiskSeverity = 'Critical' | 'High' | 'Medium' | 'Low' | 'None';
export type PriceBasis = 'Live' | 'EOD' | 'Stale' | 'Unavailable';
export type CoverageState = 'Unknown' | 'None' | 'Partial' | 'Full' | 'OverCovered' | 'Ambiguous';
export type EpisodeResolution = 'Persisted' | 'Derived' | 'Unresolved';
export type BrokerOrderType = 'StopMarket' | 'StopLimit' | 'Other';
export type StopType = 'Fixed' | 'Trailing';
export type BrokerUserStatus =
  | 'UserConfirmedEstablished'
  | 'UserReportedCancelled'
  | 'UserReportedFilled'
  | 'Superseded'
  | 'EpisodeClosed';
export type RiskStatus =
  | 'StopBreached'
  | 'ApproachingStop'
  | 'MissingRiskPlan'
  | 'NoBrokerProtection'
  | 'PartialProtection'
  | 'UnknownCoverage'
  | 'AmbiguousCoverage'
  | 'OverCovered'
  | 'StopMismatch'
  | 'StaleEvidence';

export interface BrokerStop {
  id: number;
  orderIdentity: string;
  orderType: BrokerOrderType;
  triggerPrice: number;
  limitPrice: number | null;
  currency: string;
  userStatus: BrokerUserStatus;
  brokerVerificationStatus: 'Unverified' | 'Verified';
  /** null = unknown. Never shown or exported as zero or full. */
  coveredQuantity: number | null;
  placedDate: string | null;
  brokerOrderRef: string | null;
  lastUserVerifiedDate: string | null;
  confirmationSource: string;
  notes: string | null;
  episodeOpenDate: string | null;
  isActive: boolean;
  belongsToCurrentEpisode: boolean;
  /** The trade closed in Portfolio Manager; the broker order may still be open. */
  brokerCancellationUnverified: boolean;
  replacedByRecordId: number | null;
  updatedAtUtc: string;
  rowVersion: string;
}

export interface RiskPlan {
  id: number;
  accountType: string;
  symbol: string;
  plannedStopPrice: number | null;
  targetPrice: number | null;
  stopType: StopType;
  stopRationale: string | null;
  tradeRationale: string | null;
  riskNotes: string | null;
  currency: string | null;
  episodeOpenDate: string | null;
  belongsToCurrentEpisode: boolean;
  updatedAtUtc: string;
  rowVersion: string;
}

export interface RiskAuditChange {
  field: string;
  old: string | null;
  new: string | null;
}

export interface RiskAuditEvent {
  id: number;
  occurredAtUtc: string;
  userName: string | null;
  source: string;
  eventType: string;
  changes: RiskAuditChange[];
  reason: string | null;
  brokerStopRecordId: number | null;
}

export interface RiskPosition {
  accountType: string;
  symbol: string;
  hasOpenPosition: boolean;
  totalQuantity: number;
  swingQuantity: number;
  mixedRole: boolean;
  episodeResolution: EpisodeResolution;
  episodeNote: string | null;
  episodeOpenDate: string | null;
  currency: string;
  currencyBasis: string;
  plan: RiskPlan | null;
  brokerStops: BrokerStop[];
  history: RiskAuditEvent[];
}

export interface SaveRiskPlanRequest {
  accountType: string;
  symbol: string;
  plannedStopPrice: number | null;
  targetPrice: number | null;
  stopType: StopType;
  stopRationale: string | null;
  tradeRationale: string | null;
  riskNotes: string | null;
  reason: string | null;
  rowVersion: string | null;
}

export interface SaveBrokerStopRequest {
  id: number | null;
  accountType: string;
  symbol: string;
  orderType: BrokerOrderType;
  triggerPrice: number | null;
  limitPrice: number | null;
  coveredQuantity: number | null;
  placedDate: string | null;
  brokerOrderRef: string | null;
  lastUserVerifiedDate: string | null;
  notes: string | null;
  reason: string | null;
  allowOverCoverage: boolean;
  rowVersion: string | null;
}

export interface BrokerStopTransitionRequest {
  status: 'UserReportedCancelled' | 'UserReportedFilled' | 'Superseded';
  replacedByRecordId: number | null;
  reason: string | null;
  rowVersion: string | null;
}

export interface SwingTradeRow {
  key: string;
  symbol: string;
  companyName: string;
  account: string;
  anchorItemId: number;
  lotIds: number[];
  decisionSource: string | null;
  episodeOpenDate: string | null;
  firstPurchaseDate: string | null;
  lastExecutionDate: string | null;
  daysHeld: number | null;
  episodeResolution: EpisodeResolution;
  episodeNote: string | null;
  swingQuantity: number;
  totalQuantity: number;
  mixedRole: boolean;
  averageCost: number;
  currency: string;
  currencyBasis: string;
  currentPrice: number | null;
  priceAsOfUtc: string | null;
  priceSessionDate: string | null;
  priceBasis: PriceBasis;
  priceSource: string;
  marketValue: number | null;
  unrealizedPnl: number | null;
  unrealizedPnlPct: number | null;
  plannedStop: number | null;
  targetPrice: number | null;
  stopType: StopType | null;
  planFromPriorEpisode: boolean;
  brokerStopTrigger: number | null;
  brokerOrderType: BrokerOrderType | null;
  brokerStatus: BrokerUserStatus | null;
  brokerVerification: 'Unverified' | 'Verified' | null;
  brokerStops: BrokerStop[];
  coveredQuantity: number | null;
  coverageState: CoverageState;
  protection: string;
  riskStatuses: RiskStatus[];
  severity: RiskSeverity;
  severityRank: number;
  needsAttention: boolean;
  distanceAbs: number | null;
  distancePct: number | null;
  governingStopSource: string | null;
  observation: string | null;
}

export interface CurrencyTotal {
  currency: string;
  marketValue: number;
  unrealizedPnl: number;
  unrealizedPnlPct: number;
}

export interface OpenTradesSummary {
  openTrades: number;
  needsAttention: number;
  totals: CurrencyTotal[];
  asOfUtc: string | null;
  anyStale: boolean;
  anyUnavailable: boolean;
}

export interface OpenTradesResponse {
  summary: OpenTradesSummary;
  rows: SwingTradeRow[];
  approachingThresholdPct: number;
  thresholdNote: string;
}

export interface JournalHistoryItem {
  occurredAtUtc: string;
  eventType: string;
  summary: string;
}

export interface JournalRow {
  key: string;
  symbol: string;
  account: string;
  episodeId: string | null;
  episodeResolution: EpisodeResolution;
  openDate: string | null;
  finalCloseDate: string | null;
  daysHeld: number | null;
  closedQuantity: number;
  entryPrice: number | null;
  exitPrice: number | null;
  realizedPnl: number | null;
  realizedPnlPct: number | null;
  currency: string;
  originalDecisionSource: string | null;
  closingDecisionSource: string | null;
  thesis: string | null;
  notes: string | null;
  outcome: 'Win' | 'Loss' | 'Flat' | 'Unknown' | 'Partially closed';
  episodeStillOpen: boolean;
  riskHistory: JournalHistoryItem[];
  lotIds: number[];
}

/** What the Manage Risk dialog needs from the caller; everything else is loaded from the API. */
export interface ManageRiskDialogData {
  account: string;
  symbol: string;
  companyName?: string | null;
  role?: string | null;
  averageCost?: number | null;
  currentPrice?: number | null;
  priceBasis?: PriceBasis | null;
  priceAsOfUtc?: string | null;
  marketValue?: number | null;
  unrealizedPnl?: number | null;
  unrealizedPnlPct?: number | null;
}
