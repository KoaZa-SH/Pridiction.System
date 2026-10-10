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
        private static double Factorial(int n)
        {
            double value = 1.0;
            for (int i = 2; i <= n; i++)
                value *= i;
            return value;
        }

        public static double PoissonPmf(int goals, double lambda)
        {
            if (goals < 0)
                throw new ArgumentOutOfRangeException(nameof(goals), "Goals cannot be negative.");
            if (lambda < 0 || double.IsNaN(lambda) || double.IsInfinity(lambda))
                throw new ArgumentOutOfRangeException(nameof(lambda), "Lambda must be a finite, non-negative number.");

            return Math.Exp(-lambda) * Math.Pow(lambda, goals) / Factorial(goals);
        }

        public static MatchProbabilities Analyze(double homeRate, double awayRate)
        {
            if (double.IsNaN(homeRate) || double.IsInfinity(homeRate) ||
                double.IsNaN(awayRate) || double.IsInfinity(awayRate) ||
                homeRate < 0 || awayRate < 0 || homeRate > 8 || awayRate > 8)
            {
                throw new ArgumentOutOfRangeException(
                    "Rates", "Home and away rates must be finite numbers between 0 and 8.");
            }

            // At the supported maximum rate of 8, truncating at 30 goals leaves
            // negligible omitted probability mass compared with the previous 12-goal cap.
            const int maxGoals = 30;
            var homeProbabilities = Enumerable.Range(0, maxGoals + 1)
                .Select(goals => PoissonPmf(goals, homeRate)).ToArray();
            var awayProbabilities = Enumerable.Range(0, maxGoals + 1)
                .Select(goals => PoissonPmf(goals, awayRate)).ToArray();

            double homeWin = 0, draw = 0, awayWin = 0, over = 0, bothTeamsScore = 0, totalMass = 0;
            var scores = new List<ScoreProbability>();

            for (int homeGoals = 0; homeGoals <= maxGoals; homeGoals++)
            {
                for (int awayGoals = 0; awayGoals <= maxGoals; awayGoals++)
                {
                    double probability = homeProbabilities[homeGoals] * awayProbabilities[awayGoals];
                    totalMass += probability;

                    if (homeGoals > awayGoals) homeWin += probability;
                    else if (homeGoals == awayGoals) draw += probability;
                    else awayWin += probability;

                    if (homeGoals + awayGoals >= 3) over += probability;
                    if (homeGoals > 0 && awayGoals > 0) bothTeamsScore += probability;

                    scores.Add(new ScoreProbability
                    {
                        Score = homeGoals + " - " + awayGoals,
                        Value = probability,
                        Probability = (probability * 100).ToString("0.00") + "%"
                    });
                }
            }

            if (totalMass <= 0 || double.IsNaN(totalMass) || double.IsInfinity(totalMass))
                throw new InvalidOperationException("Poisson probability mass could not be normalized.");

            // Normalize every market and exact-score probability against the same
            // joint distribution. This prevents truncated tail mass from being
            // silently assigned to Under 2.5 or omitted from other markets.
            foreach (var score in scores)
            {
                score.Value /= totalMass;
                score.Probability = (score.Value * 100).ToString("0.00") + "%";
            }

            double normalizedOver = over / totalMass;
            return new MatchProbabilities
            {
                HomeWin = homeWin / totalMass,
                Draw = draw / totalMass,
                AwayWin = awayWin / totalMass,
                Over25 = normalizedOver,
                Under25 = 1.0 - normalizedOver,
                BothTeamsScore = bothTeamsScore / totalMass,
                TopScores = scores.OrderByDescending(score => score.Value).Take(10).ToList()
            };
        }
    }
}