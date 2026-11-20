using System.IO.Compression;
using System.Text.Json;
using PortfolioManager.Api.Services.FinalAction;

namespace PortfolioManager.Tests;

/// <summary>
/// Parity gate for the backend Final Action engine. The golden file is produced by running the REAL
/// Angular DecisionEngineService (scripts/generate-final-action-golden.mjs) over a seeded corpus; every
/// case must match exactly: action, reasons, gap values, classes, diagnostics.
/// </summary>
public sealed class FinalActionParityTests
{
    private static readonly Lazy<List<JsonElement>> Cases = new(Load);

    private static List<JsonElement> Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "final-action-golden.jsonl.gz");
        using var gz = new GZipStream(File.OpenRead(path), CompressionMode.Decompress);
        using var reader = new StreamReader(gz);
        var list = new List<JsonElement>();
        string? line;
        while ((line = reader.ReadLine()) is not null)
            if (line.Length > 0) list.Add(JsonDocument.Parse(line).RootElement.Clone());
        return list;
    }

    [Fact]
    public void Catalog_CoversEveryActionTheAngularEngineProducedInTheGoldenCorpus()
    {
        var missing = Cases.Value
            .SelectMany(c => new[] { c.GetProperty("expected").GetProperty("finalAction").GetString()!, c.GetProperty("expected").GetProperty("baseAction").GetString()! })
            .Distinct().Where(a => !FinalActionCatalog.IsKnown(a)).ToList();
        Assert.True(missing.Count == 0, "Actions missing from FinalActionCatalog: " + string.Join(" | ", missing));
    }

    [Fact]
    public void Corpus_IsLargeAndCoversManyDistinctActions()
    {
        var cases = Cases.Value;
        Assert.True(cases.Count >= 10000);
        var actions = cases.Select(c => c.GetProperty("expected").GetProperty("finalAction").GetString()).Distinct().Count();
        Assert.True(actions >= 60, $"only {actions} distinct actions in the golden corpus");
        foreach (var kind in new[] { "watchlist", "portfolio", "scanner" })
            Assert.Contains(cases, c => c.GetProperty("kind").GetString() == kind);
    }

    [Fact]
    public void BackendEngine_MatchesAngularEngine_ForEveryGoldenCase()
    {
        var mismatches = new List<string>();
        foreach (var c in Cases.Value)
        {
            var kind = c.GetProperty("kind").GetString();
            var role = c.GetProperty("role").ValueKind == JsonValueKind.Null ? null : c.GetProperty("role").GetString();
            var input = ReadInput(c.GetProperty("scan"));

            FinalActionDecision actual = kind switch
            {
                "watchlist" => FinalActionEngine.TranslateForWatchlist(input, role, ReadValueCtx(c.GetProperty("valueCtx"))),
                "portfolio" => FinalActionEngine.TranslateForPortfolio(input, role, ReadItemCtx(c.GetProperty("itemCtx"))),
                _ => FinalActionEngine.TranslateForRsiScanner(input),
            };

            var diff = Compare(c.GetProperty("expected"), actual);
            if (diff is not null)
            {
                mismatches.Add($"#{c.GetProperty("id").GetInt32()} {kind} role={role}: {diff}");
                if (mismatches.Count >= 15) break;
            }
        }

        Assert.True(mismatches.Count == 0, "Parity mismatches (first 15):\n" + string.Join("\n", mismatches));
    }

    private static string? Compare(JsonElement e, FinalActionDecision a)
    {
        string? S(string name) => e.GetProperty(name).ValueKind == JsonValueKind.Null ? null : e.GetProperty(name).GetString();
        void Check(List<string> d, string name, string? expected, string? actual)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal)) d.Add($"{name}: expected '{expected}' got '{actual}'");
        }

        var diffs = new List<string>();
        Check(diffs, "finalAction", S("finalAction"), a.FinalAction);
        Check(diffs, "trendSetup", S("trendSetup"), a.TrendSetup);
        Check(diffs, "trendSetupReason", S("trendSetupReason"), a.TrendSetupReason);
        Check(diffs, "momentumShift", S("momentumShift"), a.MomentumShift);
        Check(diffs, "momentumShiftReason", S("momentumShiftReason"), a.MomentumShiftReason);
        Check(diffs, "baseAction", S("baseAction"), a.BaseAction);
        Check(diffs, "gapStatus", S("gapStatus"), a.GapStatus);
        Check(diffs, "hoverDescription", S("hoverDescription"), a.HoverDescription);
        Check(diffs, "finalActionClass", S("finalActionClass"), a.FinalActionClass);
        Check(diffs, "trendSetupClass", S("trendSetupClass"), a.TrendSetupClass);
        Check(diffs, "momentumShiftClass", S("momentumShiftClass"), a.MomentumShiftClass);
        Check(diffs, "finalActionReason", S("finalActionReason"), a.FinalActionReason);
        if (e.GetProperty("gapScore").GetInt32() != a.GapScore) diffs.Add($"gapScore: expected {e.GetProperty("gapScore")} got {a.GapScore}");
        if (e.GetProperty("gapPct").GetDouble() != a.GapPct) diffs.Add($"gapPct: expected {e.GetProperty("gapPct").GetDouble():R} got {a.GapPct:R}");

        var (sev, pri) = FinalActionEngine.ActionCenterClassification(a.FinalActionClass);
        Check(diffs, "severity", S("severity"), sev);
        Check(diffs, "priority", S("priority"), pri);

        var diag = e.GetProperty("diagnostics");
        if (diag.ValueKind == JsonValueKind.Null != (a.WatchlistDiagnostics is null))
            diffs.Add("diagnostics presence differs");
        else if (a.WatchlistDiagnostics is { } w)
        {
            Check(diffs, "diag.finalAction", diag.GetProperty("finalAction").GetString(), w.FinalAction);
            Check(diffs, "diag.reason", diag.GetProperty("finalActionReason").GetString(), w.FinalActionReason);
            if (diag.GetProperty("hasHardStructuralNegative").GetBoolean() != w.HasHardStructuralNegative) diffs.Add("diag.hardNegative");
            Check(diffs, "diag.hardReason", diag.GetProperty("hardNegativeReason").ValueKind == JsonValueKind.Null ? null : diag.GetProperty("hardNegativeReason").GetString(), w.HardNegativeReason);
            Check(diffs, "diag.priceStructureState", diag.GetProperty("priceStructureState").GetString(), w.PriceStructureState);
            Check(diffs, "diag.maStructure", diag.GetProperty("maStructure").GetString(), w.MaStructure);
            Check(diffs, "diag.role", diag.GetProperty("role").GetString(), w.Role);
            var bs = diag.GetProperty("buyScore");
            if ((bs.ValueKind == JsonValueKind.Null ? (int?)null : bs.GetInt32()) != w.BuyScore) diffs.Add("diag.buyScore");
            if (diag.GetProperty("entryBlockedByResistance").GetBoolean() != w.EntryBlockedByResistance) diffs.Add("diag.blockedResistance");
            if (diag.GetProperty("entryBlockedByHardNegative").GetBoolean() != w.EntryBlockedByHardNegative) diffs.Add("diag.blockedHard");
            if (diag.GetProperty("entryBlockedByMomentum").GetBoolean() != w.EntryBlockedByMomentum) diffs.Add("diag.blockedMomentum");
            if (diag.GetProperty("entryBlockedByRoleConfirmation").GetBoolean() != w.EntryBlockedByRoleConfirmation) diffs.Add("diag.blockedRole");
        }
        return diffs.Count == 0 ? null : string.Join("; ", diffs);
    }

    private static double D(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;
    private static double? DN(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;
    private static string St(JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";

    private static FinalActionInput ReadInput(JsonElement s)
    {
        FinalActionPriceStructure? ps = null;
        if (s.TryGetProperty("priceStructure", out var p) && p.ValueKind == JsonValueKind.Object)
            ps = new FinalActionPriceStructure(
                p.GetProperty("hasHardStructuralNegative").GetBoolean(), St(p, "keyLevelRole"), St(p, "keyLevelState"),
                St(p, "primaryPatternState"), St(p, "primaryPatternType"));

        return new FinalActionInput
        {
            Symbol = St(s, "symbol"), Rsi = D(s, "rsi"), CurrentPrice = D(s, "currentPrice"), ChangePercent = D(s, "changePercent"),
            DayHigh = D(s, "dayHigh"), DayLow = D(s, "dayLow"), OpenPrice = D(s, "openPrice"), PreviousClose = D(s, "previousClose"),
            VolumeRatio = D(s, "volumeRatio"), RsiSignal = DN(s, "rsiSignal"), RsiSignalAvailable = s.GetProperty("rsiSignalAvailable").GetBoolean(),
            MacdHistogram = D(s, "macdHistogram"), MacdHistDelta = D(s, "macdHistDelta"),
            Ema9 = D(s, "ema9Price"), Ema10 = D(s, "ema10Price"), Ema20 = D(s, "ema20Price"),
            Sma20 = D(s, "sma20Price"), Sma50 = D(s, "sma50Price"), Sma200 = D(s, "sma200"),
            Status = St(s, "status"), ScanType = St(s, "scanType"), AnalystTargetPrice = D(s, "analystTargetPrice"),
            Fib50 = D(s, "fib50"), Fib61_8 = D(s, "fib61_8"), Fib78_6 = D(s, "fib78_6"),
            FibZone = St(s, "fibZone"), FibStatus = St(s, "fibStatus"), RsiDelta1D = DN(s, "rsiDelta1D"),
            TrendSetup200 = St(s, "trendSetup200"), TrendShift = St(s, "trendShift"),
            ChaseRisk = s.GetProperty("chaseRisk").GetBoolean(), Week52High = D(s, "week52High"), PriceStructure = ps,
        };
    }

    private static WatchlistValueContext? ReadValueCtx(JsonElement v)
    {
        if (v.ValueKind != JsonValueKind.Object) return null;
        int? buy = v.TryGetProperty("buyScore", out var b) && b.ValueKind == JsonValueKind.Number ? b.GetInt32() : null;
        var trap = v.TryGetProperty("valueTrapWarning", out var t) && t.ValueKind == JsonValueKind.True;
        return new WatchlistValueContext(DN(v, "valueScore"), trap, buy);
    }

    private static PortfolioItemContext? ReadItemCtx(JsonElement v)
    {
        if (v.ValueKind != JsonValueKind.Object) return null;
        return new PortfolioItemContext(
            v.TryGetProperty("accountType", out var a) && a.ValueKind == JsonValueKind.String ? a.GetString() : null,
            DN(v, "unrealizedGainPct"), DN(v, "holdingDays"), DN(v, "distanceFrom52WeekHighPct"),
            DN(v, "positionSizePct"), DN(v, "riskControlClosePct"),
            v.TryGetProperty("decisionSource", out var d) && d.ValueKind == JsonValueKind.String ? d.GetString() : null);
    }
}
