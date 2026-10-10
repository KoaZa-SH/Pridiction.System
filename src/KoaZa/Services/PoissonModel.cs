using System;
using System.Collections.Generic;
using System.Linq;

namespace KoaZa.Services
{
    public sealed class ScoreProbability
    {
        public string Score { get; set; }
        public string Probability { get; set; }
        public double Value { get; set; }
    }

    public sealed class MarketProbability
    {
        public string Market { get; set; }
        public string Outcome { get; set; }
        public double Probability { get; set; }
        public string Label { get { return Market + " — " + Outcome; } }
    }

    public sealed class MatchProbabilities
    {
        public double HomeWin { get; set; }
        public double Draw { get; set; }
        public double AwayWin { get; set; }
        public double Over15 { get; set; }
        public double Under15 { get; set; }
        public double Over25 { get; set; }
        public double Under25 { get; set; }
        public double Over35 { get; set; }
        public double Under35 { get; set; }
        public double BothTeamsScore { get; set; }
        public double BothTeamsNoScore { get; set; }
        public double HomeOver05 { get; set; }
        public double HomeOver15 { get; set; }
        public double AwayOver05 { get; set; }
        public double AwayOver15 { get; set; }
        public List<MarketProbability> Markets { get; set; }
        public List<ScoreProbability> TopScores { get; set; }
    }

    public static class PoissonModel
    {
        private static double Factorial(int n) { double v = 1.0; for (int i = 2; i <= n; i++) v *= i; return v; }
        public static double PoissonPmf(int goals, double lambda)
        {
            if (goals < 0 || lambda < 0 || double.IsNaN(lambda) || double.IsInfinity(lambda)) throw new ArgumentOutOfRangeException("lambda");
            return Math.Exp(-lambda) * Math.Pow(lambda, goals) / Factorial(goals);
        }

        public static MatchProbabilities Analyze(double homeRate, double awayRate)
        {
            if (homeRate < 0 || awayRate < 0 || homeRate > 8 || awayRate > 8) throw new ArgumentOutOfRangeException("Rates must be between 0 and 8.");
            const int maxGoals = 30;
            var hp = Enumerable.Range(0, maxGoals + 1).Select(i => PoissonPmf(i, homeRate)).ToArray();
            var ap = Enumerable.Range(0, maxGoals + 1).Select(i => PoissonPmf(i, awayRate)).ToArray();
            double normalizer = hp.Sum() * ap.Sum();
            double hw = 0, draw = 0, aw = 0, over05 = 0, over15 = 0, over25 = 0, over35 = 0, btts = 0;
            var scores = new List<ScoreProbability>();
            for (int h = 0; h <= maxGoals; h++)
            for (int a = 0; a <= maxGoals; a++)
            {
                double p = hp[h] * ap[a] / normalizer;
                if (h > a) hw += p; else if (h == a) draw += p; else aw += p;
                if (h + a >= 1) over05 += p;
                if (h + a >= 2) over15 += p;
                if (h + a >= 3) over25 += p;
                if (h + a >= 4) over35 += p;
                if (h > 0 && a > 0) btts += p;
                scores.Add(new ScoreProbability { Score = h + " - " + a, Value = p, Probability = (p * 100).ToString("0.00") + "%" });
            }
            double homeSum = hp.Sum();
            double awaySum = ap.Sum();
            double homeOver05 = (homeSum - hp[0]) / homeSum;
            double homeOver15 = (homeSum - hp[0] - hp[1]) / homeSum;
            double awayOver05 = (awaySum - ap[0]) / awaySum;
            double awayOver15 = (awaySum - ap[0] - ap[1]) / awaySum;
            var markets = new List<MarketProbability>
            {
                M("Match result", "Home win", hw), M("Match result", "Draw", draw), M("Match result", "Away win", aw),
                M("Double chance", "1X", hw + draw), M("Double chance", "X2", draw + aw), M("Double chance", "12", hw + aw),
                M("Both teams to score", "Yes", btts),
                M("Total goals", "Over 0.5", over05), M("Total goals", "Under 0.5", 1 - over05),
                M("Total goals", "Over 1.5", over15), M("Total goals", "Under 1.5", 1 - over15),
                M("Total goals", "Over 2.5", over25), M("Total goals", "Under 2.5", 1 - over25),
                M("Total goals", "Over 3.5", over35), M("Total goals", "Under 3.5", 1 - over35)
            };
            return new MatchProbabilities
            {
                HomeWin = hw, Draw = draw, AwayWin = aw, Over15 = over15, Under15 = 1 - over15,
                Over25 = over25, Under25 = 1 - over25, Over35 = over35, Under35 = 1 - over35,
                BothTeamsScore = btts, BothTeamsNoScore = 1 - btts,
                HomeOver05 = homeOver05, HomeOver15 = homeOver15, AwayOver05 = awayOver05, AwayOver15 = awayOver15,
                Markets = markets, TopScores = scores.OrderByDescending(s => s.Value).Take(10).ToList()
            };
        }

        private static MarketProbability M(string market, string outcome, double p)
        {
            return new MarketProbability { Market = market, Outcome = outcome, Probability = Math.Max(0, Math.Min(1, p)) };
        }
    }
}