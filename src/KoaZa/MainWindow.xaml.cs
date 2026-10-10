using System;
using System.Globalization;
using System.Windows;
using KoaZa.Data;
using KoaZa.Services;
namespace KoaZa
{
    public partial class MainWindow : Window
    {
        private readonly DatabaseService _database;
        public MainWindow()
        {
            InitializeComponent();
            try { _database = new DatabaseService(); }
            catch (Exception ex) { MessageBox.Show("Could not initialize the local database.\n" + ex.Message, "Database error", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }
        private void OpenApiSettings_Click(object sender, RoutedEventArgs e)
        {
            var window = new ApiSettingsWindow();
            window.Owner = this;
            window.ShowDialog();
        }

        private void AnalyzeButton_Click(object sender, RoutedEventArgs e)
        {
            double homeRate, awayRate;
            if (string.IsNullOrWhiteSpace(HomeTeamBox.Text) || string.IsNullOrWhiteSpace(AwayTeamBox.Text) ||
                !double.TryParse(HomeRateBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out homeRate) ||
                !double.TryParse(AwayRateBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out awayRate) || homeRate < 0 || awayRate < 0 || homeRate > 8 || awayRate > 8)
            { MessageBox.Show("Enter both team names and valid expected-goal rates from 0 to 8 (use a dot for decimals).", "Check inputs", MessageBoxButton.OK, MessageBoxImage.Information); return; }
            try
            {
                var p = PoissonModel.Analyze(homeRate, awayRate);
                MatchTitle.Text = HomeTeamBox.Text.Trim() + " vs " + AwayTeamBox.Text.Trim();
                HomeWinText.Text = "Home win: " + Percent(p.HomeWin); DrawText.Text = "Draw: " + Percent(p.Draw); AwayWinText.Text = "Away win: " + Percent(p.AwayWin);
                OverText.Text = "Over 2.5 goals: " + Percent(p.Over25); UnderText.Text = "Under 2.5 goals: " + Percent(p.Under25); BttsText.Text = "Both teams to score: " + Percent(p.BothTeamsScore);
                ScoresGrid.ItemsSource = p.TopScores;
                if (_database != null) { _database.SavePrediction(HomeTeamBox.Text.Trim(), AwayTeamBox.Text.Trim(), homeRate, awayRate, p); QualityText.Text = "Saved to local SQLite • Rates are user-entered, not verified data."; }
                else QualityText.Text = "Not saved: database initialization failed. Rates are user-entered only.";
            }
            catch (Exception ex) { MessageBox.Show("Analysis failed:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }
        private static string Percent(double value) { return (value * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%"; }
    }
}
