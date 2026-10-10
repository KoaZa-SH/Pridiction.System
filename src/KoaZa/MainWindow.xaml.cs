using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using KoaZa.Data;
using KoaZa.Services;

namespace KoaZa
{
    public partial class MainWindow : Window
    {
        private DatabaseService _database;
        private List<FixtureRecord> _fixtures = new List<FixtureRecord>();
        private bool _loadingFilters;
        private bool _settingLanguage;

        public MainWindow()
        {
            LocalizationService.Load();
            InitializeComponent();
            _settingLanguage = true;
            LanguageBox.SelectedIndex = LocalizationService.IsPersian ? 0 : 1;
            _settingLanguage = false;
            TrySetIcon();
            try
            {
                _database = new DatabaseService();
                ConfigureDateFilters();
                LoadFixtureList();
                LocalizationService.Apply(this);
            }
            catch (Exception ex)
            {
                FixtureStatusText.Text = LocalizationService.T("Database initialization failed: ", "خطا در پایگاه داده: ") + ex.Message;
            }
        }

        private void TrySetIcon()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "koaza.ico");
                if (File.Exists(path)) Icon = BitmapFrame.Create(new Uri(path, UriKind.Absolute));
            }
            catch { }
        }

        private void ConfigureDateFilters()
        {
            _loadingFilters = true;
            DateFilterBox.Items.Clear();
            DateFilterBox.Items.Add(new DateFilterOption { Label = LocalizationService.T("All next 7 days", "تمام ۷ روز آینده"), Date = null });
            for (int i = 0; i < 7; i++)
            {
                DateTime day = DateTime.Today.AddDays(i);
                DateFilterBox.Items.Add(new DateFilterOption
                {
                    Label = LocalizationService.IsPersian ? day.ToString("yyyy/MM/dd", CultureInfo.GetCultureInfo("fa-IR")) : day.ToString("ddd, dd MMM", CultureInfo.GetCultureInfo("en-US")),
                    Date = day
                });
            }
            DateFilterBox.SelectedIndex = 0;
            _loadingFilters = false;
        }

        private void LoadFixtureList()
        {
            if (_database == null) return;
            try
            {
                DateTime start = DateTime.Today;
                DateTime end = start.AddDays(7);
                _fixtures = _database.GetUpcomingFixtures(start, end);
                var previousLeague = (LeagueFilterBox.SelectedItem as string);
                _loadingFilters = true;
                LeagueFilterBox.Items.Clear();
                LeagueFilterBox.Items.Add(LocalizationService.T("All competitions", "همه رقابت‌ها"));
                foreach (string league in _database.GetKnownLeagues())
                    LeagueFilterBox.Items.Add(LocalizationService.League(league));
                int selectedIndex = 0;
                if (!string.IsNullOrWhiteSpace(previousLeague))
                {
                    for (int i = 0; i < LeagueFilterBox.Items.Count; i++)
                        if (string.Equals(Convert.ToString(LeagueFilterBox.Items[i]), previousLeague, StringComparison.OrdinalIgnoreCase))
                            selectedIndex = i;
                }
                LeagueFilterBox.SelectedIndex = selectedIndex;
                _loadingFilters = false;
                ApplyFilters();
                FixtureStatusText.Text = _fixtures.Count == 0
                    ? LocalizationService.T("No upcoming fixtures in local database. Import fixtures first.", "مسابقه‌ای در پایگاه داده نیست؛ ابتدا مسابقات را دریافت کن.")
                    : LocalizationService.T("Using locally saved fixtures. Refresh after import.", "فهرست از داده‌های ذخیره‌شده استفاده می‌کند؛ پس از دریافت، نوسازی کن.");
            }
            catch (Exception ex)
            {
                FixtureStatusText.Text = LocalizationService.T("Could not load fixtures: ", "بارگذاری مسابقات ناموفق بود: ") + ex.Message;
            }
        }

        private void ApplyFilters()
        {
            if (_loadingFilters) return;
            var dateOption = DateFilterBox.SelectedItem as DateFilterOption;
            string league = Convert.ToString(LeagueFilterBox.SelectedItem);
            var filtered = _fixtures.AsEnumerable();
            if (dateOption != null && dateOption.Date.HasValue)
                filtered = filtered.Where(f => GetLocalKickoff(f).Date == dateOption.Date.Value.Date);
            if (!string.IsNullOrWhiteSpace(league) && !league.StartsWith("All competitions", StringComparison.OrdinalIgnoreCase))
                filtered = filtered.Where(f => string.Equals(LocalizationService.League(f.League), league, StringComparison.OrdinalIgnoreCase));
            var rows = filtered.OrderBy(GetLocalKickoff).Select(f => new FixtureRow(f)).ToList();
            FixturesGrid.ItemsSource = rows;
            FixtureCountText.Text = LocalizationService.IsPersian ? rows.Count + " مسابقه" : rows.Count + " matches";
            MarketsList.ItemsSource = null;
            MatchTitleText.Text = LocalizationService.T("Select a match from the list", "یک مسابقه را انتخاب کن");
            ModelStatusText.Text = LocalizationService.T("Predictions use saved completed matches. No manual xG entry is required.", "پیش‌بینی از نتایج تاریخی ذخیره‌شده محاسبه می‌شود؛ ورود دستی xG لازم نیست.");
        }

        private static DateTime GetLocalKickoff(FixtureRecord fixture)
        {
            DateTime parsed;
            if (!DateTime.TryParse(fixture.KickoffUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out parsed))
                return DateTime.MinValue;
            return parsed.ToLocalTime();
        }

        private void Refresh_Click(object sender, RoutedEventArgs e) { LoadFixtureList(); }
        private void Filters_Changed(object sender, SelectionChangedEventArgs e) { ApplyFilters(); }

        private void OpenApiSettings_Click(object sender, RoutedEventArgs e)
        {
            var window = new ApiSettingsWindow { Owner = this };
            window.ShowDialog();
        }

        private void LanguageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_settingLanguage || LanguageBox.SelectedItem == null) return;
            var item = LanguageBox.SelectedItem as ComboBoxItem;
            if (item == null) return;
            var selected = FixturesGrid.SelectedItem as FixtureRow;
            string selectedId = selected == null ? null : selected.Fixture.ExternalId;
            LocalizationService.SetLanguage(Convert.ToString(item.Tag));
            LocalizationService.Apply(this);
            ConfigureDateFilters();
            LoadFixtureList();
            if (!string.IsNullOrWhiteSpace(selectedId))
            {
                var rows = FixturesGrid.ItemsSource as IEnumerable<FixtureRow>;
                var restored = rows == null ? null : rows.FirstOrDefault(r => r.Fixture.ExternalId == selectedId);
                if (restored != null) FixturesGrid.SelectedItem = restored;
            }
        }

        private void OpenFixtures_Click(object sender, RoutedEventArgs e)
        {
            var window = new FixturesWindow { Owner = this };
            window.ShowDialog();
            LoadFixtureList();
        }

        private void FixturesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var row = FixturesGrid.SelectedItem as FixtureRow;
            if (row == null || _database == null) return;
            AnalyzeFixture(row.Fixture);
        }

        private void AnalyzeFixture(FixtureRecord fixture)
        {
            MatchTitleText.Text = LocalizationService.Team(fixture.HomeTeam) + " vs " + LocalizationService.Team(fixture.AwayTeam);
            HomeChartTeamText.Text = LocalizationService.T("HOME: ", "میزبان: ") + LocalizationService.Team(fixture.HomeTeam);
            AwayChartTeamText.Text = LocalizationService.T("AWAY: ", "میهمان: ") + LocalizationService.Team(fixture.AwayTeam);
            MarketsList.ItemsSource = null;
            try
            {
                var homeMatches = _database.GetCompletedMatches(fixture.League, fixture.HomeTeam, 12);
                var awayMatches = _database.GetCompletedMatches(fixture.League, fixture.AwayTeam, 12);
                if (homeMatches.Count < 3 || awayMatches.Count < 3)
                {
                    ModelStatusText.Text = LocalizationService.IsPersian
                        ? "داده تاریخی کافی نیست. تعداد بازی‌های میزبان: " + homeMatches.Count + "؛ میهمان: " + awayMatches.Count + ". ابتدا نتایج کامل‌شده را دریافت کن."
                        : "Not enough historical matches. Found " + homeMatches.Count + " for the home team and " + awayMatches.Count + " for the away team. Import completed results first.";
                    return;
                }

                double homeAttack = Average(homeMatches, fixture.HomeTeam, true, true);
                double homeDefence = Average(homeMatches, fixture.HomeTeam, false, true);
                double awayAttack = Average(awayMatches, fixture.AwayTeam, true, false);
                double awayDefence = Average(awayMatches, fixture.AwayTeam, false, false);
                double homeOverallAttack = Average(homeMatches, fixture.HomeTeam, true, null);
                double homeOverallDefence = Average(homeMatches, fixture.HomeTeam, false, null);
                double awayOverallAttack = Average(awayMatches, fixture.AwayTeam, true, null);
                double awayOverallDefence = Average(awayMatches, fixture.AwayTeam, false, null);

                if (double.IsNaN(homeAttack)) homeAttack = homeOverallAttack;
                if (double.IsNaN(homeDefence)) homeDefence = homeOverallDefence;
                if (double.IsNaN(awayAttack)) awayAttack = awayOverallAttack;
                if (double.IsNaN(awayDefence)) awayDefence = awayOverallDefence;

                double homeRate = Clamp((homeAttack + awayDefence) / 2.0);
                double awayRate = Clamp((awayAttack + homeDefence) / 2.0);
                var probabilities = PoissonModel.Analyze(homeRate, awayRate);
                MarketsList.ItemsSource = probabilities.Markets.Select(m => new MarketRow
                {
                    Label = LocalizationService.Market(m.Market, m.Outcome),
                    Percent = Math.Round(m.Probability * 100, 1),
                    PercentLabel = (m.Probability * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%"
                }).ToList();

                _database.SavePrediction(fixture.HomeTeam, fixture.AwayTeam, homeRate, awayRate, probabilities);
                ModelStatusText.Text = LocalizationService.IsPersian
                    ? "گل مورد انتظار (محاسبه خودکار): میزبان " + homeRate.ToString("0.00", CultureInfo.InvariantCulture) + "، میهمان " + awayRate.ToString("0.00", CultureInfo.InvariantCulture) +
                      ". بر پایه حداکثر ۱۲ بازی اخیر هر تیم (" + homeMatches.Count + " بازی میزبان، " + awayMatches.Count + " بازی میهمان). مدل پواسون پایه؛ احتمال‌ها تضمین‌شده یا کالیبره‌شده نیستند."
                    : "Expected goals (automatic): home " + homeRate.ToString("0.00", CultureInfo.InvariantCulture) + ", away " + awayRate.ToString("0.00", CultureInfo.InvariantCulture) +
                      ". Based on up to 12 recent completed matches per team (" + homeMatches.Count + " home-team matches; " + awayMatches.Count + " away-team matches). Baseline Poisson; not calibrated betting guarantees.";
            }
            catch (Exception ex)
            {
                ModelStatusText.Text = LocalizationService.T("Analysis failed: ", "خطا در تحلیل: ") + ex.Message;
            }
        }

        private static double Average(List<FixtureRecord> matches, string team, bool scored, bool? homeVenue)
        {
            var values = new List<int>();
            foreach (var m in matches)
            {
                bool isHome = string.Equals(m.HomeTeam, team, StringComparison.OrdinalIgnoreCase);
                if (homeVenue.HasValue && isHome != homeVenue.Value) continue;
                int value;
                if (scored) value = isHome ? m.HomeGoals.Value : m.AwayGoals.Value;
                else value = isHome ? m.AwayGoals.Value : m.HomeGoals.Value;
                values.Add(value);
            }
            return values.Count == 0 ? double.NaN : values.Average();
        }

        private static double Clamp(double rate) { return Math.Max(0.05, Math.Min(5.0, rate)); }

        private sealed class DateFilterOption
        {
            public string Label { get; set; }
            public DateTime? Date { get; set; }
            public override string ToString() { return Label; }
        }

        private sealed class FixtureRow
        {
            public FixtureRecord Fixture { get; private set; }
            public string League { get { return LocalizationService.League(Fixture.League); } }
            public string HomeTeam { get { return LocalizationService.Team(Fixture.HomeTeam); } }
            public string AwayTeam { get { return LocalizationService.Team(Fixture.AwayTeam); } }
            public string KickoffDisplay { get { return LocalizationService.IsPersian ? GetLocalKickoff(Fixture).ToString("yyyy/MM/dd HH:mm", CultureInfo.GetCultureInfo("fa-IR")) : GetLocalKickoff(Fixture).ToString("dd MMM HH:mm", CultureInfo.GetCultureInfo("en-US")); } }
            public FixtureRow(FixtureRecord fixture) { Fixture = fixture; }
        }

        private sealed class MarketRow
        {
            public string Label { get; set; }
            public double Percent { get; set; }
            public string PercentLabel { get; set; }
        }
    }
}
