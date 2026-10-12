using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
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
        private bool _suppressFixtureSelection;
        private string _newsCompetition;
        private DispatcherTimer _rssRefreshTimer;
        private DispatcherTimer _csvRefreshTimer;
        private bool _csvSyncInProgress;

        public MainWindow()
        {
            LocalizationService.Load();
            ThemeService.Load();
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
                ThemeService.Apply(this);
                LoadLatestNews();
                StartRssRefreshTimer();
                StartCsvRefreshTimer();
            }
            catch (Exception ex)
            {
                FixtureStatusText.Text = LocalizationService.IsPersian ? "راه‌اندازی پایگاه داده ناموفق بود. پوشهٔ داده‌های برنامه و دسترسی نوشتن را بررسی کن." : "Database initialization failed: " + ex.Message;
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
                var previousOption = LeagueFilterBox.SelectedItem as LeagueFilterOption;
                string previousLeagueKey = previousOption == null ? null : previousOption.Key;
                _loadingFilters = true;
                LeagueFilterBox.Items.Clear();
                LeagueFilterBox.Items.Add(new LeagueFilterOption(null, LocalizationService.T("All competitions", "همه رقابت‌ها"), true));
                foreach (string league in _database.GetKnownLeagues().Where(CompetitionSelectionStore.IsVisible))
                    LeagueFilterBox.Items.Add(new LeagueFilterOption(league, LocalizationService.League(league), false));
                int selectedIndex = 0;
                if (!string.IsNullOrWhiteSpace(previousLeagueKey))
                {
                    for (int i = 0; i < LeagueFilterBox.Items.Count; i++)
                    {
                        var option = LeagueFilterBox.Items[i] as LeagueFilterOption;
                        if (option != null && string.Equals(option.Key, previousLeagueKey, StringComparison.OrdinalIgnoreCase))
                            selectedIndex = i;
                    }
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
                FixtureStatusText.Text = LocalizationService.IsPersian ? "بارگذاری مسابقات ناموفق بود. پایگاه داده را بررسی و دوباره تلاش کن." : "Could not load fixtures: " + ex.Message;
            }
        }

        private void ApplyFilters()
        {
            if (_loadingFilters) return;
            var dateOption = DateFilterBox.SelectedItem as DateFilterOption;
            var leagueOption = LeagueFilterBox.SelectedItem as LeagueFilterOption;
            var hiddenCompetitions = CompetitionSelectionStore.LoadHidden();
            var filtered = _fixtures.Where(f => !hiddenCompetitions.Contains((f.League ?? "").Trim())).AsEnumerable();
            if (dateOption != null && dateOption.Date.HasValue)
                filtered = filtered.Where(f => IsLiveFixture(f.Status) || GetLocalKickoff(f).Date == dateOption.Date.Value.Date);
            if (leagueOption != null && !leagueOption.IsAll && !string.IsNullOrWhiteSpace(leagueOption.Key))
                filtered = filtered.Where(f => string.Equals((f.League ?? "").Trim(), leagueOption.Key, StringComparison.OrdinalIgnoreCase));
            var previous = FixturesGrid.SelectedItem as FixtureRow;
            string previousId = previous == null ? null : previous.Fixture.ExternalId;
            var rows = filtered.OrderByDescending(f => IsLiveFixture(f.Status)).ThenBy(GetLocalKickoff).Select(f => new FixtureRow(f)).ToList();
            var selectedRow = !string.IsNullOrWhiteSpace(previousId)
                ? rows.FirstOrDefault(r => string.Equals(r.Fixture.ExternalId, previousId, StringComparison.OrdinalIgnoreCase))
                : null;
            if (selectedRow == null && rows.Count > 0) selectedRow = rows[0];

            _suppressFixtureSelection = true;
            FixturesGrid.ItemsSource = rows;
            FixturesGrid.SelectedItem = selectedRow;
            _suppressFixtureSelection = false;
            FixtureCountText.Text = LocalizationService.IsPersian ? rows.Count + " مسابقه" : rows.Count + " matches";
            if (selectedRow != null)
            {
                AnalyzeFixture(selectedRow.Fixture);
                LoadLatestNews(selectedRow.Fixture.League);
            }
            else
            {
                MarketsList.ItemsSource = BuildUnavailableMarketRows(LocalizationService.T("No fixture matches the current filters.", "با فیلترهای فعلی مسابقه‌ای پیدا نشد."));
                SetMarketChartState(false, LocalizationService.T("No fixture matches the current filters.", "با فیلترهای فعلی مسابقه‌ای پیدا نشد."));
                MatchTitleText.Text = LocalizationService.T("Select a match from the list", "یک مسابقه انتخاب کن");
                ModelStatusText.Text = LocalizationService.T("No fixture matches the current filters.", "با فیلترهای فعلی مسابقه‌ای پیدا نشد.");
                LoadLatestNews(null);
            }
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

        private void RefreshNews_Click(object sender, RoutedEventArgs e)
        {
            var row = FixturesGrid.SelectedItem as FixtureRow;
            LoadLatestNews(row == null ? null : row.Fixture.League, true);
        }

        private void LoadLatestNews()
        {
            var row = FixturesGrid == null ? null : FixturesGrid.SelectedItem as FixtureRow;
            LoadLatestNews(row == null ? null : row.Fixture.League, false);
        }

        private async void LoadLatestNews(string competition, bool force = false)
        {
            string target = (competition ?? "").Trim();
            if (target.Length == 0)
            {
                _newsCompetition = null;
                NewsList.ItemsSource = new List<NewsItem>();
                NewsStatusText.Text = LocalizationService.T("Select a match to see news for its competition.", "برای دیدن اخبار مرتبط با رقابت، یک مسابقه را انتخاب کن.");
                return;
            }
            if (!force && string.Equals(_newsCompetition, target, StringComparison.OrdinalIgnoreCase)) return;
            _newsCompetition = target;
            NewsStatusText.Text = LocalizationService.T("Loading news for ", "در حال دریافت اخبار رقابت ") + LocalizationService.League(target) + "...";
            try
            {
                var news = await System.Threading.Tasks.Task.Run(() => FootballDataService.FetchLatestNews(target));
                if (!string.Equals(_newsCompetition, target, StringComparison.OrdinalIgnoreCase)) return;
                NewsList.ItemsSource = news;
                NewsStatusText.Text = news.Count == 0
                    ? LocalizationService.T("No news found for this competition. Try refreshing later.", "خبر مرتبطی برای این رقابت پیدا نشد؛ بعداً دوباره نوسازی کن.")
                    : LocalizationService.T("Competition-specific headlines for: ", "اخبار مرتبط با رقابت: ") + LocalizationService.League(target) + " (" + news.Count + ")";
            }
            catch (Exception ex)
            {
                if (!string.Equals(_newsCompetition, target, StringComparison.OrdinalIgnoreCase)) return;
                NewsStatusText.Text = LocalizationService.T("News feed unavailable: ", "خبرخوان در دسترس نیست: ") + ex.Message;
            }
        }

        private void OpenDataSources_Click(object sender, RoutedEventArgs e)
        {
            var window = new DataSourcesWindow(_database) { Owner = this };
            window.ShowDialog();
            LoadFixtureList();
        }

        private void StartRssRefreshTimer()
        {
            if (_rssRefreshTimer != null) return;
            _rssRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(2) };
            _rssRefreshTimer.Tick += async (sender, args) =>
            {
                try
                {
                    await System.Threading.Tasks.Task.Run(() => RssNewsService.FetchAll());
                    var selected = FixturesGrid.SelectedItem as FixtureRow;
                    if (selected != null) LoadLatestNews(selected.Fixture.League, true);
                }
                catch { }
            };
            _rssRefreshTimer.Start();
            System.Threading.Tasks.Task.Run(() => { try { RssNewsService.FetchAll(); } catch { } });
        }

        private void StartCsvRefreshTimer()
        {
            if (_csvRefreshTimer != null) { _csvRefreshTimer.Stop(); _csvRefreshTimer = null; }
            var settings = CsvAutoSyncService.LoadSettings();
            if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.Urls) || _database == null) return;
            _csvRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(Math.Max(5, settings.IntervalMinutes)) };
            _csvRefreshTimer.Tick += async (sender, args) => await RunScheduledCsvSyncAsync();
            _csvRefreshTimer.Start();
            _ = RunScheduledCsvSyncAsync();
        }

        private async System.Threading.Tasks.Task RunScheduledCsvSyncAsync()
        {
            if (_csvSyncInProgress || _database == null) return;
            var settings = CsvAutoSyncService.LoadSettings();
            if (!settings.Enabled || string.IsNullOrWhiteSpace(settings.Urls)) return;
            _csvSyncInProgress = true;
            try
            {
                var result = await System.Threading.Tasks.Task.Run(() => CsvAutoSyncService.SyncConfigured(_database));
                if (result.Imported > 0)
                    FixtureStatusText.Text = LocalizationService.T("Scheduled CSV sync saved historical rows: ", "همگام‌سازی زمان‌بندی‌شده CSV؛ ردیف‌های تاریخی ذخیره‌شده: ") + result.Imported;
            }
            catch (Exception ex)
            {
                FixtureStatusText.Text = LocalizationService.T("Scheduled CSV sync warning: ", "هشدار همگام‌سازی CSV: ") + ex.Message;
            }
            finally { _csvSyncInProgress = false; }
        }

        private void OpenApiSettings_Click(object sender, RoutedEventArgs e)
        {
            var window = new ApiSettingsWindow { Owner = this };
            window.ShowDialog();
            ThemeService.Apply(this);
            StartCsvRefreshTimer();
            LoadFixtureList();
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
            if (_suppressFixtureSelection) return;
            var row = FixturesGrid.SelectedItem as FixtureRow;
            if (row == null || _database == null) return;
            AnalyzeFixture(row.Fixture);
            LoadLatestNews(row.Fixture.League);
        }

        private void AnalyzeFixture(FixtureRecord fixture)
        {
            MatchTitleText.Text = LocalizationService.Team(fixture.HomeTeam) + LocalizationService.T(" vs ", " در برابر ") + LocalizationService.Team(fixture.AwayTeam);
            LoadFixtureInsights(fixture);
            HomeChartTeamText.Text = LocalizationService.T("HOME: ", "میزبان: ") + LocalizationService.Team(fixture.HomeTeam);
            AwayChartTeamText.Text = LocalizationService.T("AWAY: ", "میهمان: ") + LocalizationService.Team(fixture.AwayTeam);
            MarketsList.ItemsSource = BuildUnavailableMarketRows(LocalizationService.T("Calculating markets...", "در حال محاسبه بازارها..."));
            SetMarketChartState(false, LocalizationService.T("Calculating market probabilities...", "در حال محاسبه احتمال بازارها..."));
            try
            {
                var homeLeagueMatches = _database.GetCompletedMatches(fixture.League, fixture.HomeTeam, 12);
                var awayLeagueMatches = _database.GetCompletedMatches(fixture.League, fixture.AwayTeam, 12);
                var homeMatches = homeLeagueMatches;
                var awayMatches = awayLeagueMatches;

                // If a team has too little history in this competition, use its recent completed
                // matches from any competition instead of requiring head-to-head history.
                if (homeMatches.Count < 3)
                {
                    var broader = _database.GetCompletedMatches(null, fixture.HomeTeam, 12);
                    if (broader.Count > homeMatches.Count) homeMatches = broader;
                }
                if (awayMatches.Count < 3)
                {
                    var broader = _database.GetCompletedMatches(null, fixture.AwayTeam, 12);
                    if (broader.Count > awayMatches.Count) awayMatches = broader;
                }

                var baselineMatches = _database.GetRecentCompletedMatches(fixture.League, 100);
                if (baselineMatches.Count < 10) baselineMatches = _database.GetRecentCompletedMatches(null, 100);
                bool hasReliableHistory = homeLeagueMatches.Count >= 3 && awayLeagueMatches.Count >= 3 && baselineMatches.Count >= 10;
                string dataWarning = "";
                double homeRate;
                double awayRate;
                if (homeMatches.Count == 0 && awayMatches.Count == 0 && baselineMatches.Count == 0)
                {
                    // Keep the chart visible, but use neutral prior rates and clearly mark them as
                    // placeholders rather than pretending that team-specific data was available.
                    homeRate = 1.35;
                    awayRate = 1.10;
                    dataWarning = LocalizationService.T(
                        "No historical results are saved. Orange chart: neutral baseline only, not a team-specific prediction. Import past results to improve it.",
                        "نتیجه تاریخی ذخیره نشده است. نمودار نارنجی: فقط میانگین پایه و نه پیش‌بینی اختصاصی این دو تیم. برای بهبود، نتایج بازی‌های گذشته را دریافت و ذخیره کن.");
                    var priorProbabilities = PoissonModel.Analyze(homeRate, awayRate);
                    SetMarketRows(priorProbabilities, false, dataWarning);
                    ModelStatusText.Text = dataWarning;
                    UpdateMultiMarketPredictions(fixture);
                    return;
                }

                if (homeMatches.Count > 0 && awayMatches.Count > 0)
                {
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
                    homeRate = Clamp((homeAttack + awayDefence) / 2.0);
                    awayRate = Clamp((awayAttack + homeDefence) / 2.0);
                }
                else
                {
                    // With no saved matches for one team, use the available competition/global
                    // home/away scoring baseline rather than returning an empty probability panel.
                    homeRate = homeMatches.Count > 0
                        ? Clamp(Average(homeMatches, fixture.HomeTeam, true, null))
                        : Clamp(MeanHomeGoals(baselineMatches));
                    awayRate = awayMatches.Count > 0
                        ? Clamp(Average(awayMatches, fixture.AwayTeam, true, null))
                        : Clamp(MeanAwayGoals(baselineMatches));
                }
                // Head-to-head is a capped supporting signal, not a replacement for recent form.
                var headToHead = _database.GetRecentCompletedMatches(fixture.League, 100)
                    .Where(m => (string.Equals(m.HomeTeam, fixture.HomeTeam, StringComparison.OrdinalIgnoreCase) && string.Equals(m.AwayTeam, fixture.AwayTeam, StringComparison.OrdinalIgnoreCase)) ||
                                (string.Equals(m.HomeTeam, fixture.AwayTeam, StringComparison.OrdinalIgnoreCase) && string.Equals(m.AwayTeam, fixture.HomeTeam, StringComparison.OrdinalIgnoreCase)))
                    .Take(8).ToList();
                if (headToHead.Count >= 3)
                {
                    double h2hHome = headToHead.Average(m => string.Equals(m.HomeTeam, fixture.HomeTeam, StringComparison.OrdinalIgnoreCase) ? m.HomeGoals.Value : m.AwayGoals.Value);
                    double h2hAway = headToHead.Average(m => string.Equals(m.HomeTeam, fixture.AwayTeam, StringComparison.OrdinalIgnoreCase) ? m.HomeGoals.Value : m.AwayGoals.Value);
                    homeRate = Clamp(homeRate * 0.80 + h2hHome * 0.20);
                    awayRate = Clamp(awayRate * 0.80 + h2hAway * 0.20);
                }
                // Shot-on-target history can refine expected goals when actual samples are available.
                double leagueShots = AverageTotalShotsOnTarget(baselineMatches);
                double homeShots = AverageTeamShotsOnTarget(homeMatches, fixture.HomeTeam);
                double awayShots = AverageTeamShotsOnTarget(awayMatches, fixture.AwayTeam);
                if (leagueShots > 0 && homeShots > 0)
                {
                    double factor = Math.Max(0.65, Math.Min(1.35, homeShots / (leagueShots / 2.0)));
                    homeRate = Clamp(homeRate * (0.90 + 0.10 * factor));
                }
                if (leagueShots > 0 && awayShots > 0)
                {
                    double factor = Math.Max(0.65, Math.Min(1.35, awayShots / (leagueShots / 2.0)));
                    awayRate = Clamp(awayRate * (0.90 + 0.10 * factor));
                }
                // Corners are a weaker attack-pressure signal, so they receive only a small 5% influence.
                double leagueCorners = AverageTotalCorners(baselineMatches);
                double homeCornersAverage = TeamCornerSamples(homeMatches, fixture.HomeTeam).DefaultIfEmpty(0).Average();
                double awayCornersAverage = TeamCornerSamples(awayMatches, fixture.AwayTeam).DefaultIfEmpty(0).Average();
                if (leagueCorners > 0 && homeCornersAverage > 0)
                {
                    double factor = Math.Max(0.70, Math.Min(1.30, homeCornersAverage / (leagueCorners / 2.0)));
                    homeRate = Clamp(homeRate * (0.95 + 0.05 * factor));
                }
                if (leagueCorners > 0 && awayCornersAverage > 0)
                {
                    double factor = Math.Max(0.70, Math.Min(1.30, awayCornersAverage / (leagueCorners / 2.0)));
                    awayRate = Clamp(awayRate * (0.95 + 0.05 * factor));
                }

                var probabilities = PoissonModel.Analyze(homeRate, awayRate);
                AddHistoricalStatMarkets(probabilities, baselineMatches, homeMatches, awayMatches, fixture);
                if (!hasReliableHistory)
                    dataWarning = LocalizationService.T(
                        "Limited historical data. Orange chart: estimates use available team results and/or competition averages; confidence is low.",
                        "داده تاریخی محدود است. نمودار نارنجی: تخمین‌ها از نتایج موجود تیم‌ها و/یا میانگین رقابت استفاده می‌کنند؛ اطمینان پایین است.");
                // Do not force the entire chart orange merely because corner/card data is absent.
                // The goal model is shown normally when team and competition history is sufficient;
                // unsupported corner/card rows remain individually orange and explicitly unavailable.
                SetMarketRows(probabilities, hasReliableHistory, dataWarning);

                _database.SavePrediction(fixture.HomeTeam, fixture.AwayTeam, homeRate, awayRate, probabilities);
                bool usedFallback = homeLeagueMatches.Count < 3 || awayLeagueMatches.Count < 3;
                ModelStatusText.Text = LocalizationService.IsPersian
                    ? "گل مورد انتظار: میزبان " + homeRate.ToString("0.00", CultureInfo.InvariantCulture) + "، میهمان " + awayRate.ToString("0.00", CultureInfo.InvariantCulture) +
                      ". داده: " + homeMatches.Count + " بازی میزبان و " + awayMatches.Count + " بازی میهمان. " +
                      (usedFallback ? "به‌دلیل کمبود داده در همین رقابت، سابقهٔ سایر رقابت‌ها یا میانگین گل‌های موجود هم استفاده شده است. " : "از سابقهٔ همین رقابت استفاده شده است. ") +
                      "مدل پواسون با سابقه رودررو و شوت در چارچوب، در صورت وجود داده، اصلاح می‌شود؛ بازارهای آماری بر پایه فراوانی تاریخی‌اند. احتمال‌ها تخمینی هستند و تضمین نتیجه نیستند."
                    : "Expected goals: home " + homeRate.ToString("0.00", CultureInfo.InvariantCulture) + ", away " + awayRate.ToString("0.00", CultureInfo.InvariantCulture) +
                      ". Data: " + homeMatches.Count + " home-team matches and " + awayMatches.Count + " away-team matches. " +
                      (usedFallback ? "Because same-competition history is limited, other competitions or available scoring averages were used. " : "Same-competition history was used. ") +
                      "Poisson baseline refined by head-to-head and shots-on-target when available; statistical markets use historical frequencies. Estimates, not guarantees.";
                if (!string.IsNullOrEmpty(dataWarning))
                    ModelStatusText.Text += " " + dataWarning;
                UpdateMultiMarketPredictions(fixture);
            }
            catch (Exception ex)
            {
                string reason = LocalizationService.IsPersian
                    ? "محاسبه بازارها ناموفق بود: " + ex.Message + "؛ نمودار نارنجی است چون داده کافی یا قابل‌خواندن در دسترس نیست."
                    : "Market calculation failed: " + ex.Message + ". Orange chart means data is missing or could not be read.";
                MarketsList.ItemsSource = BuildUnavailableMarketRows(reason);
                SetMarketChartState(false, reason);
                ModelStatusText.Text = reason;
            }
        }

        private static string SummarizeStoredFixtureStatistics(FixtureRecord fixture)
        {
            var lines = new List<string>();
            string home = fixture.HomeTeam ?? LocalizationService.T("Home", "میزبان");
            string away = fixture.AwayTeam ?? LocalizationService.T("Away", "میهمان");
            if (fixture.HomeCorners.HasValue || fixture.AwayCorners.HasValue)
                lines.Add(LocalizationService.T("Corners", "کرنرها") + ": " + home + " " + (fixture.HomeCorners.HasValue ? fixture.HomeCorners.Value.ToString() : "—") +
                    " | " + away + " " + (fixture.AwayCorners.HasValue ? fixture.AwayCorners.Value.ToString() : "—"));
            if (fixture.HomeShotsOnTarget.HasValue || fixture.AwayShotsOnTarget.HasValue)
                lines.Add(LocalizationService.T("Shots on target", "شوت در چارچوب") + ": " + home + " " + (fixture.HomeShotsOnTarget.HasValue ? fixture.HomeShotsOnTarget.Value.ToString() : "—") +
                    " | " + away + " " + (fixture.AwayShotsOnTarget.HasValue ? fixture.AwayShotsOnTarget.Value.ToString() : "—"));
            if (fixture.HomeYellowCards.HasValue || fixture.AwayYellowCards.HasValue)
                lines.Add(LocalizationService.T("Yellow cards", "کارت زرد") + ": " + home + " " + (fixture.HomeYellowCards.HasValue ? fixture.HomeYellowCards.Value.ToString() : "—") +
                    " | " + away + " " + (fixture.AwayYellowCards.HasValue ? fixture.AwayYellowCards.Value.ToString() : "—"));
            if (!string.IsNullOrWhiteSpace(fixture.Referee))
                lines.Add(LocalizationService.T("Referee", "داور") + ": " + fixture.Referee);
            if (fixture.VarReviews.HasValue)
                lines.Add(LocalizationService.T("VAR reviews", "بررسی‌های VAR") + ": " + fixture.VarReviews.Value);
            if (lines.Count == 0)
                return LocalizationService.T("This provider has not returned detailed statistics for this fixture. Missing statistics are not treated as zero.",
                    "این سرویس برای این مسابقه آمار تفصیلی برنگردانده است؛ آمار ناموجود صفر فرض نمی‌شود.");
            return string.Join(Environment.NewLine, lines);
        }

        private async void LoadFixtureInsights(FixtureRecord fixture)
        {
            string fixtureId = fixture.ExternalId;
            StatsText.Text = LocalizationService.T("Loading match statistics...", "در حال دریافت آمار مسابقه...");
            bool apiSports = fixture.Provider == "API-Football" || fixture.Provider == "ApiFootball" || fixture.Provider == "ApiSports";
            bool fiveDollar = fixture.Provider == "FiveDollarFootballAPI";
            if (!apiSports && !fiveDollar)
            {
                StatsText.Text = SummarizeStoredFixtureStatistics(fixture);
                return;
            }
            try
            {
                bool live = IsLiveFixture(fixture.Status);
                string status = (fixture.Status ?? "").Trim().ToUpperInvariant();
                bool completed = status == "FT" || status == "AET" || status == "PEN" || status == "FINISHED";
                string json = live ? null : _database.GetFixtureDetails(fixture.Provider, fixture.ExternalId);
                // Re-fetch legacy cached detail JSON once for completed matches so newly added stats/events can be normalized.
                if (completed && !string.IsNullOrWhiteSpace(json) && json.IndexOf("\"detailsVersion\"", StringComparison.OrdinalIgnoreCase) < 0)
                    json = null;
                if (string.IsNullOrWhiteSpace(json))
                {
                    json = await System.Threading.Tasks.Task.Run(() => FootballDataService.FetchFixtureDetails(fixture));
                    _database.SaveFixtureDetails(fixture.Provider, fixture.ExternalId, json);
                }
                // Normalize provider-specific details into legacy columns and the complete historical table.
                var legacyStats = FootballDataService.ExtractFixtureStatistics(json, fixture);
                MatchStatisticsRecord normalized = fiveDollar
                    ? MatchStatisticsParser.ParseFiveDollarOrGeneric(json, fixture)
                    : MatchStatisticsParser.ParseApiFootball(json, fixture);
                if (normalized != null)
                {
                    legacyStats.HomeCorners = normalized.HomeCorners; legacyStats.AwayCorners = normalized.AwayCorners;
                    legacyStats.HomeShotsOnTarget = normalized.HomeShotsOnTarget; legacyStats.AwayShotsOnTarget = normalized.AwayShotsOnTarget;
                    legacyStats.HomeYellowCards = normalized.HomeYellowCards; legacyStats.AwayYellowCards = normalized.AwayYellowCards;
                    legacyStats.Referee = normalized.Referee;
                    _database.SaveMatchStatistics(normalized);
                    if (MatchStatisticsParser.HasEventPayload(json, fixture))
                        _database.ReplaceMatchEvents(fixture.Provider, fixture.ExternalId, MatchStatisticsParser.ParseEvents(json, fixture));
                }
                _database.SaveNormalizedFixtureStatistics(legacyStats);
                if (!IsCurrentFixture(fixtureId)) return;
                StatsText.Text = FootballDataService.SummarizeFixtureDetails(json, fixture);
                UpdateMultiMarketPredictions(fixture);
            }
            catch (Exception ex)
            {
                if (!IsCurrentFixture(fixtureId)) return;
                bool hasStoredStats = fixture.HomeCorners.HasValue || fixture.AwayCorners.HasValue ||
                    fixture.HomeShotsOnTarget.HasValue || fixture.AwayShotsOnTarget.HasValue ||
                    fixture.HomeYellowCards.HasValue || fixture.AwayYellowCards.HasValue ||
                    fixture.VarReviews.HasValue || !string.IsNullOrWhiteSpace(fixture.Referee);
                StatsText.Text = hasStoredStats
                    ? SummarizeStoredFixtureStatistics(fixture) + Environment.NewLine + LocalizationService.T("Live detail refresh failed; showing saved statistics.", "نوسازی آمار ناموفق بود؛ آمار ذخیره‌شده نمایش داده می‌شود.")
                    : (LocalizationService.IsPersian
                        ? "آمار تفصیلی این مسابقه در دسترس نیست؛ ممکن است سرویس داده، رقابت یا سطح اشتراک از آن پشتیبانی نکند."
                        : "Detailed stats are unavailable for this provider/match, or the API plan does not cover them: " + ex.Message);
            }

        }

        private static bool IsLiveFixture(string status)
        {
            string value = (status ?? "").Trim().ToUpperInvariant();
            return value == "LIVE" || value == "1H" || value == "HT" || value == "2H" ||
                   value == "ET" || value == "P" || value == "BT" || value == "INT" ||
                   value == "INPLAY" || value == "IN_PLAY" || value == "IN PROGRESS" || value == "INPROGRESS" ||
                   value == "PAUSED" || value == "HALFTIME" || value == "FIRST_HALF" || value == "SECOND_HALF" ||
                   value == "EXTRA_TIME" || value == "PENALTY_SHOOTOUT";
        }

        private bool IsCurrentFixture(string externalId)
        {
            var row = FixturesGrid.SelectedItem as FixtureRow;
            return row != null && string.Equals(row.Fixture.ExternalId, externalId, StringComparison.OrdinalIgnoreCase);
        }

        private void NewsItem_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            string url = button == null ? null : Convert.ToString(button.Tag);
            if (string.IsNullOrWhiteSpace(url) || !(url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))) return;
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show(this, LocalizationService.IsPersian ? "باز کردن پیوند خبر ناموفق بود. اتصال اینترنت و مرورگر پیش‌فرض را بررسی کن." : ex.Message, LocalizationService.T("Could not open news link", "باز کردن خبر ناموفق بود"), MessageBoxButton.OK, MessageBoxImage.Information); }
        }


        private void UpdateMultiMarketPredictions(FixtureRecord fixture)
        {
            try
            {
                var stats = _database.GetRecentMatchStatistics(fixture.League, 500);
                if (stats.Count < 10) stats = _database.GetRecentMatchStatistics(null, 1000);
                var eventPrediction = MultiMarketPredictionService.AnalyzeEventMarkets(fixture, stats);
                try { _database.SaveMarketPredictions(fixture, eventPrediction); }
                catch (Exception saveError) { ModelStatusText.Text += LocalizationService.T(" Event prediction save warning: ", " هشدار ذخیره بازارهای آماری: ") + saveError.Message; }

                var rows = (MarketsList.ItemsSource as IEnumerable<MarketRow> ?? Enumerable.Empty<MarketRow>()).ToList();
                foreach (var m in eventPrediction.Markets)
                {
                    string label = LocalizationService.Market(m.Market, m.Outcome);
                    var row = new MarketRow
                    {
                        Label = label,
                        Percent = m.HasSufficientData ? Math.Round(m.Probability * 100, 1) : 0,
                        PercentLabel = m.HasSufficientData
                            ? (m.Probability * 100).ToString("0.0", CultureInfo.InvariantCulture) + "% • n=" + m.SampleSize
                            : LocalizationService.T("Stats unavailable", "آمار کافی نیست") + " • n=" + m.SampleSize,
                        HasData = m.HasSufficientData,
                        BarBrush = m.HasSufficientData ? System.Windows.Media.Brushes.SeaGreen : System.Windows.Media.Brushes.Gray,
                        Reason = m.Explanation
                    };
                    int index = rows.FindIndex(existing => string.Equals(existing.Label, label, StringComparison.OrdinalIgnoreCase));
                    if (index >= 0) rows[index] = row; else rows.Add(row);
                }
                MarketsList.ItemsSource = rows;
            }
            catch (Exception ex)
            {
                ModelStatusText.Text += LocalizationService.T(" Event-market layer unavailable: ", " لایه بازارهای آماری در دسترس نیست: ") + ex.Message;
            }
        }

        private void SetMarketRows(MatchProbabilities probabilities, bool hasSufficientData, string reason)
        {
            var rows = probabilities.Markets.Select(m => new MarketRow
            {
                Label = LocalizationService.Market(m.Market, m.Outcome),
                Percent = Math.Round(m.Probability * 100, 1),
                PercentLabel = m.Market == "Both teams to score" && m.Outcome == "Yes"
                    ? (LocalizationService.IsPersian
                        ? "بله: " + (m.Probability * 100).ToString("0.0", CultureInfo.InvariantCulture) + "% • خیر: " + ((1 - m.Probability) * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%"
                        : "Yes: " + (m.Probability * 100).ToString("0.0", CultureInfo.InvariantCulture) + "% • No: " + ((1 - m.Probability) * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%")
                    : hasSufficientData ? (m.Probability * 100).ToString("0.0", CultureInfo.InvariantCulture) + "%" :
                        (m.Probability * 100).ToString("0.0", CultureInfo.InvariantCulture) + "% • " + LocalizationService.T("low confidence", "اطمینان پایین"),
                HasData = hasSufficientData,
                BarBrush = m.Market == "Match result" && m.Outcome == "Draw"
                    ? System.Windows.Media.Brushes.DarkOrange : System.Windows.Media.Brushes.SeaGreen,
                Reason = reason
            }).ToList();
            string unavailable = LocalizationService.T("Not calculated: this app has no reliable historical event statistics for this market yet.", "محاسبه نشده: هنوز آمار تاریخی قابل‌اعتماد برای این بازار در برنامه موجود نیست.");
            string[] unavailableMarkets =
            {
                "Corners|Total over 8.5", "Corners|Total under 8.5",
                "Home team corners|Over 4.5", "Home team corners|Under 4.5",
                "Away team corners|Over 4.5", "Away team corners|Under 4.5",
                "Yellow cards|Total over 3.5", "Yellow cards|Total under 3.5",
                "Shots on target|Total over 8.5", "Shots on target|Total under 8.5",
                "VAR review|Yes"
            };
            foreach (string item in unavailableMarkets)
            {
                string[] parts = item.Split('|');
                if (probabilities.Markets.Any(m => string.Equals(m.Market, parts[0], StringComparison.OrdinalIgnoreCase) && string.Equals(m.Outcome, parts[1], StringComparison.OrdinalIgnoreCase))) continue;
                rows.Add(new MarketRow { Label = LocalizationService.Market(parts[0], parts[1]), Percent = 0,
                    PercentLabel = LocalizationService.T("Stats unavailable", "آمار موجود نیست"), HasData = false,
                    BarBrush = System.Windows.Media.Brushes.Gray, Reason = unavailable });
            }
            MarketsList.ItemsSource = rows;
            if (!hasSufficientData) reason = string.IsNullOrWhiteSpace(reason) ? unavailable : reason + " " + unavailable;
            SetMarketChartState(hasSufficientData, reason);
        }

        private void SetMarketChartState(bool hasSufficientData, string reason)
        {
            MarketChartFrame.Background = new System.Windows.Media.SolidColorBrush(
                hasSufficientData ? System.Windows.Media.Color.FromRgb(255, 255, 255) : System.Windows.Media.Color.FromRgb(221, 119, 42));
            MarketChartReasonText.Text = hasSufficientData ? "" : reason;
            MarketChartReasonText.Visibility = hasSufficientData ? Visibility.Collapsed : Visibility.Visible;
            MarketChartReasonText.Foreground = System.Windows.Media.Brushes.White;
        }

        private List<MarketRow> BuildUnavailableMarketRows(string reason)
        {
            var labels = new[]
            {
                LocalizationService.Market("Match result", "Home win"), LocalizationService.Market("Match result", "Draw"),
                LocalizationService.Market("Match result", "Away win"), LocalizationService.Market("Double chance", "1X"),
                LocalizationService.Market("Double chance", "X2"), LocalizationService.Market("Double chance", "12"),
                LocalizationService.Market("Both teams to score", "Yes"),
                LocalizationService.Market("Total goals", "Over 0.5"), LocalizationService.Market("Total goals", "Under 0.5"),
                LocalizationService.Market("Total goals", "Over 1.5"), LocalizationService.Market("Total goals", "Under 1.5"),
                LocalizationService.Market("Total goals", "Over 2.5"), LocalizationService.Market("Total goals", "Under 2.5"),
                LocalizationService.Market("Total goals", "Over 3.5"), LocalizationService.Market("Total goals", "Under 3.5"),
                LocalizationService.Market("Corners", "Total over 8.5"), LocalizationService.Market("Corners", "Total under 8.5"),
                LocalizationService.Market("Home team corners", "Over 4.5"), LocalizationService.Market("Home team corners", "Under 4.5"),
                LocalizationService.Market("Away team corners", "Over 4.5"), LocalizationService.Market("Away team corners", "Under 4.5"),
                LocalizationService.Market("Yellow cards", "Total over 3.5"), LocalizationService.Market("Yellow cards", "Total under 3.5"),
                LocalizationService.Market("Shots on target", "Total over 8.5"), LocalizationService.Market("Shots on target", "Total under 8.5"),
                LocalizationService.Market("VAR review", "Yes")
            };
            SetMarketChartState(false, reason);
            return labels.Select(label => new MarketRow { Label = label, Percent = 0, PercentLabel = "—", HasData = false, BarBrush = System.Windows.Media.Brushes.Gray, Reason = reason }).ToList();
        }

        private static void AddHistoricalStatMarkets(MatchProbabilities probabilities, List<FixtureRecord> baseline, List<FixtureRecord> homeMatches, List<FixtureRecord> awayMatches, FixtureRecord fixture)
        {
            var all = (baseline ?? new List<FixtureRecord>()).Where(m => m.HomeGoals.HasValue && m.AwayGoals.HasValue).ToList();
            var corners = all.Where(m => m.HomeCorners.HasValue && m.AwayCorners.HasValue).Select(m => (double)(m.HomeCorners.Value + m.AwayCorners.Value)).ToList();
            var homeCorners = TeamCornerSamples(homeMatches, fixture.HomeTeam);
            var awayCorners = TeamCornerSamples(awayMatches, fixture.AwayTeam);
            var cards = all.Where(m => m.HomeYellowCards.HasValue && m.AwayYellowCards.HasValue).ToList();
            if (!string.IsNullOrWhiteSpace(fixture.Referee))
            {
                var refereeMatches = cards.Where(m => string.Equals(m.Referee, fixture.Referee, StringComparison.OrdinalIgnoreCase)).ToList();
                if (refereeMatches.Count >= 5) cards = refereeMatches;
            }
            var yellow = cards.Select(m => (double)(m.HomeYellowCards.Value + m.AwayYellowCards.Value)).ToList();
            var shots = all.Where(m => m.HomeShotsOnTarget.HasValue && m.AwayShotsOnTarget.HasValue)
                .Select(m => (double)(m.HomeShotsOnTarget.Value + m.AwayShotsOnTarget.Value)).ToList();
            var varSamples = all.Where(m => m.VarReviews.HasValue).Select(m => (double)m.VarReviews.Value).ToList();
            AddThresholdMarkets(probabilities, "Corners", "Total over 8.5", "Total under 8.5", corners, 8.5);
            AddThresholdMarkets(probabilities, "Home team corners", "Over 4.5", "Under 4.5", homeCorners, 4.5);
            AddThresholdMarkets(probabilities, "Away team corners", "Over 4.5", "Under 4.5", awayCorners, 4.5);
            AddThresholdMarkets(probabilities, "Yellow cards", "Total over 3.5", "Total under 3.5", yellow, 3.5);
            AddThresholdMarkets(probabilities, "Shots on target", "Total over 8.5", "Total under 8.5", shots, 8.5);
            if (varSamples.Count >= 5)
                probabilities.Markets.Add(new MarketProbability { Market = "VAR review", Outcome = "Yes", Probability = (varSamples.Count(v => v > 0) + 1.0) / (varSamples.Count + 2.0) });
        }

        private static void AddThresholdMarkets(MatchProbabilities probabilities, string market, string overOutcome, string underOutcome, List<double> samples, double threshold)
        {
            // Require real historical samples; missing values are never treated as zero.
            if (samples == null || samples.Count < 5) return;
            double over = (samples.Count(v => v > threshold) + 1.0) / (samples.Count + 2.0); // Light Laplace smoothing avoids false certainty from small samples.
            probabilities.Markets.Add(new MarketProbability { Market = market, Outcome = overOutcome, Probability = over });
            probabilities.Markets.Add(new MarketProbability { Market = market, Outcome = underOutcome, Probability = 1.0 - over });
        }

        private static List<double> TeamCornerSamples(List<FixtureRecord> matches, string team)
        {
            var result = new List<double>();
            foreach (var m in matches ?? new List<FixtureRecord>())
            {
                bool home = string.Equals(m.HomeTeam, team, StringComparison.OrdinalIgnoreCase);
                int? value = home ? m.HomeCorners : m.AwayCorners;
                if (value.HasValue) result.Add(value.Value);
            }
            return result;
        }

        private static double AverageTotalCorners(List<FixtureRecord> matches)
        {
            var values = (matches ?? new List<FixtureRecord>()).Where(m => m.HomeCorners.HasValue && m.AwayCorners.HasValue)
                .Select(m => (double)(m.HomeCorners.Value + m.AwayCorners.Value)).ToList();
            return values.Count == 0 ? 0 : values.Average();
        }

        private static double AverageTotalShotsOnTarget(List<FixtureRecord> matches)
        {
            var values = (matches ?? new List<FixtureRecord>()).Where(m => m.HomeShotsOnTarget.HasValue && m.AwayShotsOnTarget.HasValue)
                .Select(m => (double)(m.HomeShotsOnTarget.Value + m.AwayShotsOnTarget.Value)).ToList();
            return values.Count == 0 ? 0 : values.Average();
        }

        private static double AverageTeamShotsOnTarget(List<FixtureRecord> matches, string team)
        {
            var values = new List<int>();
            foreach (var m in matches ?? new List<FixtureRecord>())
            {
                bool home = string.Equals(m.HomeTeam, team, StringComparison.OrdinalIgnoreCase);
                int? value = home ? m.HomeShotsOnTarget : m.AwayShotsOnTarget;
                if (value.HasValue) values.Add(value.Value);
            }
            return values.Count == 0 ? 0 : values.Average();
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

        private static double MeanHomeGoals(List<FixtureRecord> matches)
        {
            var values = matches.Where(m => m.HomeGoals.HasValue).Select(m => (double)m.HomeGoals.Value).ToList();
            return values.Count == 0 ? 1.25 : values.Average();
        }

        private static double MeanAwayGoals(List<FixtureRecord> matches)
        {
            var values = matches.Where(m => m.AwayGoals.HasValue).Select(m => (double)m.AwayGoals.Value).ToList();
            return values.Count == 0 ? 1.0 : values.Average();
        }

        private static double Clamp(double rate) { return Math.Max(0.05, Math.Min(5.0, rate)); }

        private sealed class DateFilterOption
        {
            public string Label { get; set; }
            public DateTime? Date { get; set; }
            public override string ToString() { return Label; }
        }

        private sealed class LeagueFilterOption
        {
            public string Key { get; private set; }
            public string Label { get; private set; }
            public bool IsAll { get; private set; }
            public LeagueFilterOption(string key, string label, bool isAll) { Key = key; Label = label; IsAll = isAll; }
            public override string ToString() { return Label; }
        }

        private sealed class FixtureRow
        {
            public FixtureRecord Fixture { get; private set; }
            public bool IsLive { get { return IsLiveFixture(Fixture.Status); } }
            public string League { get { return (IsLive ? "● LIVE / زنده • " : "") + LocalizationService.League(Fixture.League); } }
            public string HomeTeam { get { return LocalizationService.Team(Fixture.HomeTeam); } }
            public string AwayTeam { get { return LocalizationService.Team(Fixture.AwayTeam); } }
            public string ScoreDisplay
            {
                get
                {
                    if (Fixture.HomeGoals.HasValue && Fixture.AwayGoals.HasValue)
                        return Fixture.HomeGoals.Value + " - " + Fixture.AwayGoals.Value;
                    return IsLive ? "LIVE" : "—";
                }
            }
            public string KickoffDisplay { get { return LocalizationService.IsPersian ? GetLocalKickoff(Fixture).ToString("yyyy/MM/dd HH:mm", CultureInfo.GetCultureInfo("fa-IR")) : GetLocalKickoff(Fixture).ToString("dd MMM HH:mm", CultureInfo.GetCultureInfo("en-US")); } }
            public FixtureRow(FixtureRecord fixture) { Fixture = fixture; }
        }

        private sealed class MarketRow
        {
            public string Label { get; set; }
            public double Percent { get; set; }
            public string PercentLabel { get; set; }
            public bool HasData { get; set; }
            public string Reason { get; set; }
            public System.Windows.Media.Brush BarBrush { get; set; }
        }
    }
}
