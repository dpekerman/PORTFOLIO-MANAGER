# FINAL ACTION — Strategic ADD confirmation safeguard

Branch: `fix/final-action-listing-fibonacci` (from `develop`). Nothing is staged, committed, pushed or deployed.

## 1. Review of the earlier fix

The earlier change fixed listing identity, allocation evidence, Signal Tracker price comparison and Fibonacci display. It did **not** change the recommendation rule, so NVDA.TO still received `ADD / STAGED ENTRY`. This change closes that gap.

The review also found a defect in my earlier change: a new `fibonacciSwing` field changed the serialized FINAL ACTION payload and broke the Pm029 golden test. The earlier focused run did not include that test. It is fixed by omitting the field when null, and the broader suite now includes it.

## 2. Root cause

| Screen | Rule that decided |
| --- | --- |
| Watchlist NVDA | Entry rule. An oversold RSI with positive momentum but no confirmed structure → `REVERSAL WATCH`. |
| Portfolio NVDA.TO | Strategic holding rule. `Active Buy Trigger` → `Add / Staged Entry`. |

`Active Buy Trigger` fires on an oversold RSI, a close in the upper half of the day's range, and high volume or improving MACD. The trend setup `Waterfall / Falling Knife` only blocks it when volume is *not* high. NVDA.TO had high volume, so the add passed the allocation checks (4.3 % against the Strategic limit) while the structure was still a falling knife.

## 3. Policy implemented

Reused existing facts only. No new scoring engine.

1. **Scope.** The target lot's role is Strategic and the decision is an ADD (an Entry-category action).
2. **Adverse structure.** The trend is `Waterfall / Falling Knife`, **or** an active hard structural negative exists (the Watchlist rule's own hard-negative test).
3. **Confirmation.** The Watchlist entry rule's own facts: a confirmed support reclaim, bounce or breakout (`BOUNCE_CONFIRMED`, `SUPPORT_RECLAIM`, `BREAKOUT_CONFIRMED`, or a breakout pattern), **and** no hard negative, no Fibonacci `Trend Damage`, and no `200 > 50 > P` moving-average stack. The checks were extracted from the Watchlist rule into shared helpers so both paths read identical facts.
4. **If unconfirmed.** The lot shows `REVERSAL WATCH — Wait for Confirmation` (Advisory / Neutral). It is not actionable and is not a sell or exit. The original role-rule output is kept in `originalAction`.
5. **Preserved.** Allocation, position-limit, sector, role-ambiguity and allocation-unavailable outcomes keep their own labels and codes. The reversal code is appended when it also applies, so no safeguard is weakened.

The role rules in the engine are unchanged, so the Angular/backend parity suite and golden files are untouched. The safeguard is applied in the shared gate that every ADD already passes through. Normal stocks and mapped CDRs use the same path.

### Reason codes

| Code | Meaning |
| --- | --- |
| `ADD_BLOCKED_REVERSAL_UNCONFIRMED` | The add waits for reversal confirmation. |
| `ADD_REVERSAL_CONFIRMED` | An adverse structure exists, but confirmation was found, so the add proceeds. Emitted together with `ADD_ELIGIBLE`. |
| Existing codes | `ADD_BLOCKED_POSITION_LIMIT`, `ADD_BLOCKED_SECTOR_OVER`, `ADD_ROLE_POLICY_AMBIGUOUS`, `ALLOCATION_EVIDENCE_UNAVAILABLE`, `ADD_BLOCKED_PORTFOLIO_ACTION`, `ALREADY_OWNED` are unchanged. |

## 4. Decision behaviour, before and after

Scenario: NVDA.TO (CDR, CAD, Strategic, about 4.3 % of the portfolio, limit 8 %). Technicals come from NVDA (USD): RSI 22 below its signal, price below SMA50, volume 1.4×, upper-half close.

| Evidence | Surface | Before | After |
| --- | --- | --- | --- |
| No confirmed reversal | Portfolio NVDA.TO | `Add / Staged Entry`; code `ADD_ELIGIBLE`; actionable | `REVERSAL WATCH — Wait for Confirmation`; original `Add / Staged Entry`; code `ADD_BLOCKED_REVERSAL_UNCONFIRMED`; not actionable |
| No confirmed reversal | Watchlist NVDA | `REVERSAL WATCH` | `REVERSAL WATCH` (unchanged) |
| Support bounce confirmed | Portfolio NVDA.TO | `Add / Staged Entry` | `Add / Staged Entry`; codes `ADD_ELIGIBLE` and `ADD_REVERSAL_CONFIRMED` |
| Support bounce confirmed | Watchlist NVDA | `ENTRY CANDIDATE` | `ENTRY CANDIDATE` (unchanged) |
| Bounce confirmed, but a hard negative remains | Portfolio | `Add / Staged Entry` | `REVERSAL WATCH — Wait for Confirmation` |
| Unconfirmed and at the position limit | Portfolio | `Hold / No Add` | `Hold / No Add`; the limit code plus `ADD_BLOCKED_REVERSAL_UNCONFIRMED` |
| Core role, same technicals | Portfolio | `Add to Position` | `Add to Position` (unchanged; Strategic only) |
| Strategic add with no adverse structure | Portfolio | `Add / Staged Entry` | `Add / Staged Entry` (unchanged) |

Unchanged in every case: the holding, the 4.3 % exposure, the CAD valuation, the USD analysis currency, Swing and Strategic scores, Portfolio Fit, Fibonacci and option accounting.

"Before" is the pre-policy gate behaviour with the same inputs. It was asserted by the earlier passing test and is still reproducible by evaluating the gate without role and reversal facts.

### Cross-screen consistency

All screens read the same evaluation and the same gate.

- **Portfolio and Watchlist.** An owned Watchlist entry is blocked by a Portfolio `REVERSAL WATCH — Wait for Confirmation` and shows the same label.
- **Trading Opportunities.** It copies the authoritative decision (test: the same label for NVDA.TO).
- **Signal Tracker.** The new label is Advisory, so it is never captured as a new recommendation. A prior `Add / Staged Entry` event keeps its type, price and date. The existing lifecycle closes the episode; the record is not edited.

## 5. Tests

| Suite | Result |
| --- | --- |
| New backend regressions (`StrategicAddConfirmationTests`, `ListingEvidenceTests`, Signal Tracker history test) | 25 passed |
| Broader backend run (FinalAction, Pm024, Pm029 including golden and parity, PortfolioEvidence, SignalsAudit, OpenDecisions, PortfolioActions, Opportunity, Scanner, Fibonacci, LegacyFallback) | 865 passed, 0 failed |
| Focused Angular (final-action dialog and labels, grid columns, swing-trading, opportunities, signal-tracker export, decision-engine) | 165 passed |
| Angular development build | Succeeded |

The new tests cover:

- NVDA.TO (mapped CDR) and ABC.TO (normal stock): unconfirmed and confirmed.
- A bounce with a hard negative.
- Position-limit precedence.
- Other roles.
- The equivalent hard negative without a falling knife.
- Missing reversal evidence.
- The structure facts shared with the Watchlist rule.
- The new action's catalog category and add-blocking.
- Trading Opportunities label parity.
- Signal Tracker history preserved.

No live Playwright, scans, protected refreshes, migrations or financial-record writes were run.

## 6. Known limits

- The Portfolio row's FINAL ACTION label is the wait advisory. There is no separate "Hold / Risk Review" field; the reason states that the holding is kept under its holding rules. If you want the underlying hold label shown too, that is a small display follow-up.
- Confirmation uses the existing structure facts, so a stock below both its 50-day and 200-day averages with a hard negative stays on Wait until structure recovers. This is the Watchlist rule's own standard, applied consistently.
- The Angular `DecisionEngineService` still exists only for the legacy scanner table and was not changed. The Portfolio, Watchlist, Trading Opportunities and Signal Tracker decisions all come from the backend evaluation.
- The existing active `ADD / STAGED ENTRY` signal for NVDA.TO will close through the normal lifecycle on the next evaluation. The record itself is not rewritten.

## 7. Manual UAT checklist

1. After the next EOD evaluation, open Portfolio and find NVDA.TO. Expect `REVERSAL WATCH — Wait for Confirmation` and an unchanged 4.3 % exposure.
2. Open the FINAL ACTION details. Expect `ADD_BLOCKED_REVERSAL_UNCONFIRMED`, the original `Add / Staged Entry`, "not a sell or exit", analysis ticker NVDA in USD, and CAD valuation.
3. Open Watchlist NVDA and Trading Opportunities (Swing and Strategic) for NVDA and NVDA.TO. Expect the Watchlist to show `REVERSAL WATCH` and the opportunity row for NVDA.TO to show the same wait label as Portfolio.
4. Open Signal Tracker. The earlier NVDA.TO `ADD / STAGED ENTRY` stays in history with its original USD signal price. There is no new `ADD / STAGED ENTRY`.
5. If NVDA later shows a confirmed bounce, reclaim or breakout with no hard negative, expect NVDA.TO to return to `Add / Staged Entry`, with `ADD_REVERSAL_CONFIRMED` in its reason codes.
6. Spot-check one other Strategic holding and one Core holding in a falling trend. The Strategic one waits; the Core one is unchanged.
