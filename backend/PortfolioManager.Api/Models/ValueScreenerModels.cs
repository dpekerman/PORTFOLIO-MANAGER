using System;
using System.Collections.Generic;

namespace PortfolioManager.Api.Models
{
    public enum TechnicalState
    {
        DeepValueReversal,
        OverboughtMomentum,
        OverboughtPullback,
        SidewaysConsolidation,
        MeanReversion,
        HighVolumeExhaustion
    }

    public enum ActionTrigger
    {
        AccumulateYield,
        AccumulateValue,
        BuyLimitAlert,
        HoldRideTrend,
        ValueTrapWarning,
        Observe
    }

    public enum ValueOrigin { Portfolio, Watchlist }

    public enum ValueTier { HighConviction, FairValue, ValueTrap }

    public class ValueScreenerResult
    {
        public string Symbol { get; set; } = "";
        public string Description { get; set; } = "";
        public ValueOrigin Origin { get; set; }
        public TechnicalState TechnicalState { get; set; }
        public ValueTier Tier { get; set; }
        public decimal Score { get; set; }
        public ActionTrigger ActionTrigger { get; set; }
        public decimal ScoreEarningsYield { get; set; }
        public decimal ScoreFcfYield { get; set; }
        public decimal ScorePriceToBook { get; set; }
        public decimal ScorePiotroski { get; set; }
        public decimal ScoreRoic { get; set; }
        public decimal EarningsYield { get; set; }
        public decimal FcfYieldProxy { get; set; }
        public decimal PriceToBook { get; set; }
        public int PiotroskiScore { get; set; }
        public decimal RoicProxy { get; set; }
        public decimal DividendYield { get; set; }
        public decimal CurrentPrice { get; set; }
        public decimal CurrentRsi { get; set; }
        public decimal Week52High { get; set; }
        public decimal Week52Low { get; set; }
        public string Sector { get; set; } = "";
        public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
    }

    public class ValueScreenerRequest
    {
        public List<string> AdHocSymbols { get; set; } = new();
        public bool IncludePortfolio { get; set; } = true;
        public bool IncludeWatchlist { get; set; } = true;
    }
}
