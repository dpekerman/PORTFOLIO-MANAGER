# EOD Lifecycle Regression TODOs

- [x] **ATD.TO action alignment:** canonical hard-negative regression asserts `AVOID` despite an RSI Bull Turn and tight rising-wedge breakdown.
- [ ] **CPH.TO completed-bar identity:** persist Sunday 2026-08-16 scans derived from Friday 2026-08-14; assert exactly one equivalent lifecycle record and `TradingDate=2026-08-14`.
- [ ] **MSFT weekend date:** persist an Aug 8 weekend scan sourced from the prior completed bar; assert no `TradingDate=2026-08-08` record exists.
- [ ] **Historical repair:** seed legacy weekend rows, invoke `POST /api/eod-signals/repair-trading-dates`, and assert dates resolve from OHLCV history, collisions merge, states and notes survive, and unresolved symbols are reported.
- [ ] **Session count:** fixture Friday, Monday, and an exchange holiday gap; assert `TradingSessionsPassed` excludes weekends and absent OHLCV dates.
- [ ] **Idempotency:** persist a repeated same ticker, scan type, signal type, and TradingDate; assert one record exists, scan snapshot fields refresh, and lifecycle state is preserved.
- [ ] **Price language:** add Angular specs for role-flipped EMA/SMA/swing/Fib/channel/wedge/confluence labels, Near Recent High resistance text, and a hard structural-negative explanation that does not grant entry permission.
