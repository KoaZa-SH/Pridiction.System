using System;
using System.Linq;
using KoaZa.Services;

namespace KoaZa.ModelSmokeTests
{
    internal static class Program
    {
        private static int _assertions;

        private static int Main()
        {
            try
            {
                PoissonPmfHandlesZeroRate();
                PoissonPmfRejectsInvalidInputs();
                AnalyzeProducesConsistentProbabilities();
                AnalyzeHandlesZeroRates();
                AnalyzeRejectsInvalidRates();

                Console.WriteLine("PASS: " + _assertions + " Poisson model assertions.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("FAIL: " + ex.Message);
                return 1;
            }
        }

        private static void PoissonPmfHandlesZeroRate()
        {
            AssertNear(1.0, PoissonModel.PoissonPmf(0, 0), 1e-12, "P(X=0 | lambda=0)");
            AssertNear(0.0, PoissonModel.PoissonPmf(1, 0), 1e-12, "P(X=1 | lambda=0)");
        }

        private static void PoissonPmfRejectsInvalidInputs()
        {
            AssertThrows<ArgumentOutOfRangeException>(() => PoissonModel.PoissonPmf(-1, 1), "negative goals");
            AssertThrows<ArgumentOutOfRangeException>(() => PoissonModel.PoissonPmf(1, -1), "negative lambda");
            AssertThrows<ArgumentOutOfRangeException>(() => PoissonModel.PoissonPmf(1, double.NaN), "NaN lambda");
            AssertThrows<ArgumentOutOfRangeException>(() => PoissonModel.PoissonPmf(1, double.PositiveInfinity), "infinite lambda");
        }

        private static void AnalyzeProducesConsistentProbabilities()
        {
            MatchProbabilities result = PoissonModel.Analyze(1.45, 1.10);
            AssertProbability(result.HomeWin, "home win");
            AssertProbability(result.Draw, "draw");
            AssertProbability(result.AwayWin, "away win");
            AssertProbability(result.Over25, "over 2.5");
            AssertProbability(result.Under25, "under 2.5");
            AssertProbability(result.BothTeamsScore, "both teams score");
            AssertNear(1.0, result.HomeWin + result.Draw + result.AwayWin, 1e-10, "1X2 sum");
            AssertNear(1.0, result.Over25 + result.Under25, 1e-10, "Over/Under sum");
            AssertTrue(result.TopScores != null && result.TopScores.Count == 10, "top score count");
            AssertTrue(result.TopScores.Zip(result.TopScores.Skip(1), (a, b) => a.Value >= b.Value).All(x => x),
                "top scores are ordered by descending probability");
        }

        private static void AnalyzeHandlesZeroRates()
        {
            MatchProbabilities result = PoissonModel.Analyze(0, 0);
            AssertNear(0.0, result.HomeWin, 1e-12, "zero-rate home win");
            AssertNear(1.0, result.Draw, 1e-12, "zero-rate draw");
            AssertNear(0.0, result.AwayWin, 1e-12, "zero-rate away win");
            AssertNear(0.0, result.Over25, 1e-12, "zero-rate over 2.5");
            AssertNear(1.0, result.Under25, 1e-12, "zero-rate under 2.5");
            AssertNear(0.0, result.BothTeamsScore, 1e-12, "zero-rate BTTS");
        }

        private static void AnalyzeRejectsInvalidRates()
        {
            AssertThrows<ArgumentOutOfRangeException>(() => PoissonModel.Analyze(-0.1, 1), "negative home rate");
            AssertThrows<ArgumentOutOfRangeException>(() => PoissonModel.Analyze(1, 8.1), "away rate above maximum");
            AssertThrows<ArgumentOutOfRangeException>(() => PoissonModel.Analyze(double.NaN, 1), "NaN rate");
            AssertThrows<ArgumentOutOfRangeException>(() => PoissonModel.Analyze(1, double.PositiveInfinity), "infinite rate");
        }

        private static void AssertProbability(double value, string name)
        {
            AssertTrue(!double.IsNaN(value) && !double.IsInfinity(value) && value >= 0 && value <= 1,
                name + " is finite and between zero and one");
        }

        private static void AssertNear(double expected, double actual, double tolerance, string name)
        {
            AssertTrue(Math.Abs(expected - actual) <= tolerance,
                name + " expected " + expected + " but got " + actual);
        }

        private static void AssertThrows<TException>(Action action, string name) where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                _assertions++;
                return;
            }

            throw new InvalidOperationException(name + " did not throw " + typeof(TException).Name);
        }

        private static void AssertTrue(bool condition, string name)
        {
            if (!condition)
                throw new InvalidOperationException("Assertion failed: " + name);
            _assertions++;
        }
    }
}
