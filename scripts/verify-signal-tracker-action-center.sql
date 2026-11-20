-- READ-ONLY verification for Signal Tracker + Dashboard Action Center.
-- Run against the PortfolioManager database before and after the EOD run. Nothing here modifies data.
SET NOCOUNT ON;

-- 1. Latest EOD automation runs. A usable run: Success, RefreshStatus + RsiStatus = Succeeded, no ErrorStep.
SELECT TOP 5 Id, TradingDate, TriggerType, OverallStatus, RefreshStatus, RsiStatus, SnapshotStatus,
       ActualStartUtc, CompletedAtUtc, LastHeartbeatStep, ErrorStep, ErrorMessage
FROM AutomationRunLogs ORDER BY Id DESC;

-- 2. Validation settings the Action Center banner is derived from.
--    Validated needs: LastEodEvaluationComplete = true, LastCompleteEvaluationUtc within 1 weekday, nothing pending.
SELECT UserId, UpdatedAt,
       COALESCE(JSON_VALUE(PreferenceValue, '$.lastEodEvaluationUtc'), JSON_VALUE(PreferenceValue, '$.LastEodEvaluationUtc')) AS LastEodEvaluationUtc,
       COALESCE(JSON_VALUE(PreferenceValue, '$.lastEodEvaluationComplete'), JSON_VALUE(PreferenceValue, '$.LastEodEvaluationComplete')) AS LastEodEvaluationComplete,
       COALESCE(JSON_VALUE(PreferenceValue, '$.lastCompleteEvaluationUtc'), JSON_VALUE(PreferenceValue, '$.LastCompleteEvaluationUtc')) AS LastCompleteEvaluationUtc
FROM UserPreferences WHERE PreferenceKey = 'signalsAudit.settings';

-- 3. Every open Final Action recommendation and whether the Action Center can show it.
--    Shown in Action Center = Status Active AND EpisodeEndedDate NULL AND LastValidatedAtUtc NOT NULL AND actionable bucket.
SELECT Id, Symbol, Source, Role, SignalType, SignalDate, SignalPrice, Origin, Status, MyAction, PerformanceAudited,
       CapturedAtUtc, LastValidatedAtUtc, EpisodeEndedDate, StatusReason,
       CASE WHEN Status <> 'Active' THEN 'Hidden: status ' + Status
            WHEN EpisodeEndedDate IS NOT NULL THEN 'Hidden: episode ended'
            WHEN LastValidatedAtUtc IS NULL THEN 'Pending: never validated by a complete EOD'
            ELSE 'Visible (if bucket is actionable)' END AS ActionCenterVisibility
FROM SignalAuditEvents
WHERE SignalType LIKE 'FA:%' AND EpisodeEndedDate IS NULL
ORDER BY Source, Symbol;

-- 4. Counts by visibility (compare with the tracker cards and Action Center chips).
SELECT Source, Status, COUNT(*) AS Rows,
       SUM(CASE WHEN LastValidatedAtUtc IS NULL THEN 1 ELSE 0 END) AS NeverValidated
FROM SignalAuditEvents
WHERE SignalType LIKE 'FA:%' AND EpisodeEndedDate IS NULL
GROUP BY Source, Status;

-- 5. Integrity: must return 0 rows. More than one open episode for the same recommendation identity.
SELECT UserId, Symbol, SignalType, Source, COUNT(*) AS OpenEpisodes
FROM SignalAuditEvents WHERE EpisodeEndedDate IS NULL
GROUP BY UserId, Symbol, SignalType, Source HAVING COUNT(*) > 1;

-- 6. Integrity: must return 0 rows. Closed episodes without an end date / reason, or Active rows that carry an end date.
SELECT Id, Symbol, SignalType, Status, EpisodeEndedDate, StatusReason
FROM SignalAuditEvents
WHERE (Status IN ('Expired','Invalidated','Superseded') AND (EpisodeEndedDate IS NULL OR StatusReason IS NULL))
   OR (Status = 'Active' AND EpisodeEndedDate IS NOT NULL)
   OR (Status = 'Superseded' AND SupersededByEventId IS NULL);

-- 7. Integrity: must return 0 rows. Non-positive signal prices or missing currency (breaks Today-vs-Signal %).
SELECT Id, Symbol, SignalPrice, Currency FROM SignalAuditEvents WHERE SignalPrice <= 0;

-- 8. What changed in the last day (new / closed recommendations) to review row by row after EOD.
SELECT Id, Symbol, Source, SignalType, Status, StatusReason, SignalDate, CapturedAtUtc, StatusChangedAtUtc, LastValidatedAtUtc
FROM SignalAuditEvents
WHERE CapturedAtUtc >= DATEADD(day, -1, SYSUTCDATETIME()) OR StatusChangedAtUtc >= DATEADD(day, -1, SYSUTCDATETIME())
ORDER BY COALESCE(StatusChangedAtUtc, CapturedAtUtc) DESC;
