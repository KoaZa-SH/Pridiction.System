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
    public sealed class MatchProbabilities
    {
        public double HomeWin { get; set; }
        public double Draw { get; set; }
        public double AwayWin { get; set; }
        public double Over25 { get; set; }
        public double Under25 { get; set; }
        public double BothTeamsScore { get; set; }
        public List<ScoreProbability> TopScores { get; set; }
    }
    public static class PoissonModel
    {
        private static double Factorial(int n) { double v = 1.0; for (int i = 2; i <= n; i++) v *= i; return v; }
        public static double PoissonPmf(int goals, double lambda)
        {
            if (lambda < 0 || double.IsNaN(lambda) || double.IsInfinity(lambda)) throw new ArgumentOutOfRangeException("lambda");
            return Math.Exp(-lambda) * Math.Pow(lambda, goals) / Factorial(goals);
        }
        public static MatchProbabilities Analyze(double homeRate, double awayRate)
        {
            if (homeRate < 0 || awayRate < 0 || homeRate > 8 || awayRate > 8) throw new ArgumentOutOfRangeException("Rates must be between 0 and 8.");
            const int maxGoals = 12;
            var hp = Enumerable.Range(0, maxGoals + 1).Select(i => PoissonPmf(i, homeRate)).ToArray();
            var ap = Enumerable.Range(0, maxGoals + 1).Select(i => PoissonPmf(i, awayRate)).ToArray();
            double hw = 0, draw = 0, aw = 0, over = 0, btts = 0;
            var scores = new List<ScoreProbability>();
            for (int h = 0; h <= maxGoals; h++)
            for (int a = 0; a <= maxGoals; a++)
            {
                double p = hp[h] * ap[a];
                if (h > a) hw += p; else if (h == a) draw += p; else aw += p;
                if (h + a >= 3) over += p;
                if (h > 0 && a > 0) btts += p;
                scores.Add(new ScoreProbability { Score = h + " - " + a, Value = p, Probability = (p * 100).ToString("0.00") + "%" });
            }
            // Normalize 1X2 for the negligible probability mass above the 12-goal truncation.
            double sum = hw + draw + aw;
            return new MatchProbabilities { HomeWin = hw / sum, Draw = draw / sum, AwayWin = aw / sum,
                Over25 = over, Under25 = 1 - over, BothTeamsScore = btts,
                TopScores = scores.OrderByDescending(s => s.Value).Take(10).ToList() };
        }
    }
}
