using System;
using System.Linq;
using KoaZa.Services;

internal static class Program
{
    private static int Main()
    {
        try
        {
            Check(0, 0);
            Check(1.35, 1.10);
            Check(8, 8);
            Console.WriteLine("Poisson model smoke tests passed.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }

    private static void Check(double homeRate, double awayRate)
    {
        MatchProbabilities result = PoissonModel.Analyze(homeRate, awayRate);
        AssertNear(1.0, result.HomeWin + result.Draw + result.AwayWin, 1e-10, "1X2 sum");
        AssertNear(1.0, result.Over15 + result.Under15, 1e-10, "Over/Under 1.5 complement");
        AssertNear(1.0, result.Over25 + result.Under25, 1e-10, "Over/Under 2.5 complement");
        AssertNear(1.0, result.Over35 + result.Under35, 1e-10, "Over/Under 3.5 complement");
        AssertNear(1.0, result.BothTeamsScore + result.BothTeamsNoScore, 1e-10, "BTTS complement");
        AssertProbability(result.HomeOver05, "home over 0.5");
        AssertProbability(result.HomeOver15, "home over 1.5");
        AssertProbability(result.AwayOver05, "away over 0.5");
        AssertProbability(result.AwayOver15, "away over 1.5");
        if (result.Markets == null || result.Markets.Any(m => double.IsNaN(m.Probability) || m.Probability < 0 || m.Probability > 1))
            throw new InvalidOperationException("A market probability is outside [0,1] or is not a number.");
        if (result.TopScores == null || result.TopScores.Count != 10 || result.TopScores.Any(s => s.Value < 0 || s.Value > 1))
            throw new InvalidOperationException("Top-score probabilities are invalid.");
    }

    private static void AssertProbability(double value, string name)
    {
        if (double.IsNaN(value) || value < 0 || value > 1)
            throw new InvalidOperationException(name + " is outside [0,1].");
    }

    private static void AssertNear(double expected, double actual, double tolerance, string name)
    {
        if (double.IsNaN(actual) || Math.Abs(expected - actual) > tolerance)
            throw new InvalidOperationException(name + " expected " + expected + " but was " + actual + ".");
    }
}