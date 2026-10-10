using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KoaZa.Services
{
    public sealed class ApiProviderConfig
    {
        public string Provider { get; set; }
        public string Key { get; set; }
        public bool Enabled { get; set; }
        public string DisplayName
        {
            get
            {
                string label = Provider == "ApiFootball" ? "API-Football" :
                    Provider == "FootballData" ? "football-data.org" :
                    Provider == "OpenLigaDB" ? "OpenLigaDB (no key)" : Provider;
                return label + (string.IsNullOrEmpty(Key) ? "" : " • " + Mask(Key)) +
                    (Enabled ? " • enabled" : " • disabled");
            }
        }
        private static string Mask(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Length <= 6 ? "••••" : value.Substring(0, 3) + "••••" + value.Substring(value.Length - 3);
        }
    }

    public static class ApiProviderStore
    {
        private static readonly string Folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KoaZa");
        private static readonly string FilePath = Path.Combine(Folder, "api-providers.dat");

        public static List<ApiProviderConfig> Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return new List<ApiProviderConfig>();
                byte[] protectedBytes = File.ReadAllBytes(FilePath);
                byte[] clearBytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
                string[] lines = Encoding.UTF8.GetString(clearBytes).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var result = new List<ApiProviderConfig>();
                foreach (string line in lines)
                {
                    string[] parts = line.Split('|');
                    if (parts.Length != 3) continue;
                    try
                    {
                        result.Add(new ApiProviderConfig
                        {
                            Provider = Decode(parts[0]),
                            Key = Decode(parts[1]),
                            Enabled = Decode(parts[2]) == "1"
                        });
                    }
                    catch { }
                }
                return result;
            }
            catch { return new List<ApiProviderConfig>(); }
        }

        public static void Save(IEnumerable<ApiProviderConfig> providers)
        {
            var lines = providers.Select(p => Encode(p.Provider) + "|" + Encode(p.Key ?? "") + "|" + Encode(p.Enabled ? "1" : "0"));
            Directory.CreateDirectory(Folder);
            byte[] clearBytes = Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, lines));
            byte[] protectedBytes = ProtectedData.Protect(clearBytes, null, DataProtectionScope.CurrentUser);
            string temp = FilePath + ".tmp";
            File.WriteAllBytes(temp, protectedBytes);
            if (File.Exists(FilePath)) File.Delete(FilePath);
            File.Move(temp, FilePath);
        }

        private static string Encode(string value) { return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? "")); }
        private static string Decode(string value) { return Encoding.UTF8.GetString(Convert.FromBase64String(value)); }
    }

    public sealed class FixtureRecord
    {
        public string Provider { get; set; }
        public string League { get; set; }
        public string KickoffUtc { get; set; }
        public string HomeTeam { get; set; }
        public string AwayTeam { get; set; }
        public int? HomeGoals { get; set; }
        public int? AwayGoals { get; set; }
        public string Status { get; set; }
        public string ExternalId { get; set; }
    }

    public static class FootballDataService
    {
        public static List<FixtureRecord> Fetch(string provider, string league, int season)
        {
            if (provider == "Auto") return FetchAuto(league, season);
            if (provider == "ApiFootball") return FetchApiFootball(league, season);
            if (provider == "FootballData") return FetchFootballData(league, season);
            if (provider == "OpenLigaDB") return FetchOpenLiga(league, season);
            throw new InvalidOperationException("Select a supported data provider.");
        }


        public static List<FixtureRecord> FetchAuto(string competition, int season)
        {
            string canonical = (competition ?? "").Trim().ToUpperInvariant();
            var configured = ApiProviderStore.Load().Where(p => p.Enabled)
                .Select(p => p.Provider).Distinct().ToList();
            if (configured.Count == 0)
                throw new InvalidOperationException("No enabled data providers are saved. Open API Settings and enable at least one provider.");

            var errors = new List<string>();
            foreach (string provider in configured)
            {
                string providerLeague = MapLeague(provider, canonical);
                if (providerLeague == null) continue;
                try
                {
                    List<FixtureRecord> rows = Fetch(provider, providerLeague, season);
                    if (rows != null && rows.Count > 0) return rows;
                    errors.Add(provider + ": returned no fixtures for this competition/season.");
                }
                catch (Exception ex)
                {
                    errors.Add(provider + ": " + ex.Message);
                }
            }
            throw new InvalidOperationException("No enabled provider could return fixtures for " + canonical + ". " +
                "For automatic fallback, use one of these codes: EPL, LALIGA, BUNDESLIGA, SERIEA, LIGUE1, IRAN. " +
                string.Join(" | ", errors));
        }

        private static string MapLeague(string provider, string competition)
        {
            if (provider == "ApiFootball")
            {
                if (competition == "EPL" || competition == "PL") return "39";
                if (competition == "LALIGA" || competition == "PD") return "140";
                if (competition == "BUNDESLIGA" || competition == "BL1") return "78";
                if (competition == "SERIEA" || competition == "SA") return "135";
                if (competition == "LIGUE1" || competition == "FL1") return "61";
                if (competition == "IRAN" || competition == "IRANPL") return "290";
            }
            if (provider == "FootballData")
            {
                if (competition == "EPL" || competition == "PL") return "PL";
                if (competition == "LALIGA" || competition == "PD") return "PD";
                if (competition == "BUNDESLIGA" || competition == "BL1") return "BL1";
                if (competition == "SERIEA" || competition == "SA") return "SA";
                if (competition == "LIGUE1" || competition == "FL1") return "FL1";
            }
            if (provider == "OpenLigaDB" && (competition == "BUNDESLIGA" || competition == "BL1")) return "bl1";
            return null;
        }

        private static List<FixtureRecord> FetchApiFootball(string league, int season)
        {
            string path = "/fixtures?league=" + Uri.EscapeDataString(league) + "&season=" + season;
            var configs = ApiProviderStore.Load().Where(p => p.Enabled && p.Provider == "ApiFootball" && !string.IsNullOrWhiteSpace(p.Key)).ToList();
            if (configs.Count == 0) throw new InvalidOperationException("No enabled API-Football key is saved. Open API Settings.");
            Exception last = null;
            foreach (var config in configs)
            {
                try
                {
                    string json = Request("https://v3.football.api-sports.io" + path, "x-apisports-key", config.Key);
                    var root = Parse(json);
                    if (HasErrors(root)) { last = new InvalidOperationException("API-Football returned an API or quota error."); continue; }
                    var response = GetList(root, "response");
                    var output = new List<FixtureRecord>();
                    foreach (var item in response)
                    {
                        var fixture = GetMap(item, "fixture");
                        var teams = GetMap(item, "teams");
                        var home = GetMap(teams, "home");
                        var away = GetMap(teams, "away");
                        var goals = GetMap(item, "goals");
                        var leagueMap = GetMap(item, "league");
                        output.Add(new FixtureRecord
                        {
                            Provider = "API-Football",
                            League = GetString(leagueMap, "name", league),
                            KickoffUtc = GetString(fixture, "date", ""),
                            HomeTeam = GetString(home, "name", "Home"),
                            AwayTeam = GetString(away, "name", "Away"),
                            HomeGoals = GetNullableInt(goals, "home"),
                            AwayGoals = GetNullableInt(goals, "away"),
                            Status = GetString(GetMap(fixture, "status"), "short", ""),
                            ExternalId = GetString(fixture, "id", "")
                        });
                    }
                    return output;
                }
                catch (WebException ex)
                {
                    int status = ex.Response is HttpWebResponse ? (int)((HttpWebResponse)ex.Response).StatusCode : 0;
                    if (status == 401 || status == 403 || status == 429) { last = ex; continue; }
                    throw new InvalidOperationException("API-Football connection failed (HTTP " + status + ").", ex);
                }
            }
            throw new InvalidOperationException("All enabled API-Football keys failed. Check key validity and quota.", last);
        }

        private static List<FixtureRecord> FetchFootballData(string league, int season)
        {
            var configs = ApiProviderStore.Load().Where(p => p.Enabled && p.Provider == "FootballData" && !string.IsNullOrWhiteSpace(p.Key)).ToList();
            if (configs.Count == 0) throw new InvalidOperationException("Add an enabled football-data.org token in API Settings.");
            string path = "/v4/competitions/" + Uri.EscapeDataString(league) + "/matches?season=" + season;
            Exception last = null;
            foreach (var config in configs)
            {
                try
                {
                    var root = Parse(Request("https://api.football-data.org" + path, "X-Auth-Token", config.Key));
                    if (HasErrors(root)) { last = new InvalidOperationException("football-data.org returned an API error."); continue; }
                    var output = new List<FixtureRecord>();
                    foreach (var item in GetList(root, "matches"))
                    {
                        var home = GetMap(item, "homeTeam");
                        var away = GetMap(item, "awayTeam");
                        var competition = GetMap(item, "competition");
                        var score = GetMap(item, "score");
                        var fullTime = GetMap(score, "fullTime");
                        output.Add(new FixtureRecord
                        {
                            Provider = "football-data.org",
                            League = GetString(competition, "name", league),
                            KickoffUtc = GetString(item, "utcDate", ""),
                            HomeTeam = GetString(home, "name", "Home"),
                            AwayTeam = GetString(away, "name", "Away"),
                            HomeGoals = GetNullableInt(fullTime, "home"),
                            AwayGoals = GetNullableInt(fullTime, "away"),
                            Status = GetString(item, "status", ""),
                            ExternalId = GetString(item, "id", "")
                        });
                    }
                    return output;
                }
                catch (WebException ex)
                {
                    int status = ex.Response is HttpWebResponse ? (int)((HttpWebResponse)ex.Response).StatusCode : 0;
                    if (status == 401 || status == 403 || status == 429) { last = ex; continue; }
                    throw new InvalidOperationException("football-data.org connection failed (HTTP " + status + ").", ex);
                }
            }
            throw new InvalidOperationException("All enabled football-data.org tokens failed. Check token and free-plan coverage.", last);
        }

        private static List<FixtureRecord> FetchOpenLiga(string league, int season)
        {
            string path = "/getmatchdata/" + Uri.EscapeDataString(league) + "/" + season;
            string json = Request("https://api.openligadb.de" + path, null, null);
            object root = new JavaScriptSerializer().DeserializeObject(json);
            var rows = root as object[];
            if (rows == null) throw new InvalidOperationException("OpenLigaDB returned an unexpected response.");
            var output = new List<FixtureRecord>();
            foreach (var item in rows)
            {
                var match = item as Dictionary<string, object>;
                if (match == null) continue;
                var home = GetMap(match, "team1");
                var away = GetMap(match, "team2");
                int? hg = null, ag = null;
                var results = GetList(match, "matchResults");
                foreach (var result in results)
                {
                    var r = result as Dictionary<string, object>;
                    if (r != null && GetString(r, "resultTypeID", "") == "2")
                    {
                        hg = GetNullableInt(r, "pointsTeam1");
                        ag = GetNullableInt(r, "pointsTeam2");
                    }
                }
                output.Add(new FixtureRecord
                {
                    Provider = "OpenLigaDB",
                    League = GetString(match, "leagueName", league),
                    KickoffUtc = GetString(match, "matchDateTimeUTC", ""),
                    HomeTeam = GetString(home, "teamName", "Home"),
                    AwayTeam = GetString(away, "teamName", "Away"),
                    HomeGoals = hg,
                    AwayGoals = ag,
                    Status = GetString(match, "matchIsFinished", "").ToLowerInvariant() == "true" ? "FINISHED" : "SCHEDULED",
                    ExternalId = GetString(match, "matchID", "")
                });
            }
            return output;
        }

        private static string Request(string url, string header, string key)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.Accept = "application/json";
            request.Timeout = 20000;
            request.ReadWriteTimeout = 20000;
            if (!string.IsNullOrEmpty(header) && !string.IsNullOrEmpty(key)) request.Headers[header] = key;
            using (var response = (HttpWebResponse)request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8))
                return reader.ReadToEnd();
        }

        private static Dictionary<string, object> Parse(string json)
        {
            return new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
        }
        private static bool HasErrors(Dictionary<string, object> root)
        {
            object errors;
            if (!root.TryGetValue("errors", out errors) || errors == null) return false;
            string value = errors as string;
            if (value != null) return value.Length > 0;
            var map = errors as Dictionary<string, object>;
            if (map != null) return map.Count > 0;
            var array = errors as object[];
            if (array != null) return array.Length > 0;
            var list = errors as System.Collections.ArrayList;
            return list != null && list.Count > 0;
        }
        private static Dictionary<string, object> GetMap(object source, string key)
        {
            var map = source as Dictionary<string, object>;
            object value;
            return map != null && map.TryGetValue(key, out value) ? value as Dictionary<string, object> : new Dictionary<string, object>();
        }
        private static List<object> GetList(object source, string key)
        {
            var map = source as Dictionary<string, object>;
            object value;
            if (map != null && map.TryGetValue(key, out value))
            {
                var list = value as System.Collections.ArrayList;
                if (list != null) return list.Cast<object>().ToList();
                var array = value as object[];
                if (array != null) return array.ToList();
            }
            var direct = source as System.Collections.ArrayList;
            return direct == null ? new List<object>() : direct.Cast<object>().ToList();
        }
        private static string GetString(object source, string key, string fallback)
        {
            var map = source as Dictionary<string, object>;
            object value;
            return map != null && map.TryGetValue(key, out value) && value != null ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) : fallback;
        }
        private static int? GetNullableInt(object source, string key)
        {
            var map = source as Dictionary<string, object>;
            object value;
            if (map == null || !map.TryGetValue(key, out value) || value == null) return null;
            int result;
            return int.TryParse(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture), out result) ? (int?)result : null;
        }
    }

    public sealed class ApiSettingsWindow : Window
    {
        private readonly ComboBox _providerBox;
        private readonly TextBox _keyBox;
        private readonly ListBox _providersList;
        private readonly TextBlock _status;
        private List<ApiProviderConfig> _providers;

        public ApiSettingsWindow()
        {
            Title = "KoaZa — API Provider Settings";
            Width = 760; Height = 570; MinWidth = 600; MinHeight = 460;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(244, 247, 245));
            _providers = ApiProviderStore.Load();

            var root = new Grid { Margin = new Thickness(20) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var title = new TextBlock { Text = "API Providers / مدیریت سرویس‌های داده", FontSize = 22, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(Color.FromRgb(22, 59, 43)), Margin = new Thickness(0, 0, 0, 12) };
            root.Children.Add(title); Grid.SetRow(title, 0);

            var entry = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            entry.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.3, GridUnitType.Star) });
            entry.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            entry.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _providerBox = new ComboBox { Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(6), VerticalContentAlignment = VerticalAlignment.Center };
            _providerBox.Items.Add(new ProviderChoice("ApiFootball", "API-Football (API key required)"));
            _providerBox.Items.Add(new ProviderChoice("FootballData", "football-data.org (token required)"));
            _providerBox.Items.Add(new ProviderChoice("OpenLigaDB", "OpenLigaDB (no key; German leagues)"));
            _providerBox.SelectedIndex = 0;
            _providerBox.SelectionChanged += ProviderBox_SelectionChanged;
            Grid.SetColumn(_providerBox, 0); entry.Children.Add(_providerBox);
            _keyBox = new TextBox { Padding = new Thickness(8), Margin = new Thickness(0, 0, 8, 0), VerticalContentAlignment = VerticalAlignment.Center };
            Grid.SetColumn(_keyBox, 1); entry.Children.Add(_keyBox);
            var add = new Button { Content = "Add provider", Padding = new Thickness(12, 7, 12, 7), Background = new SolidColorBrush(Color.FromRgb(22, 134, 91)), Foreground = Brushes.White, BorderThickness = new Thickness(0) };
            add.Click += Add_Click; Grid.SetColumn(add, 2); entry.Children.Add(add);
            root.Children.Add(entry); Grid.SetRow(entry, 1);

            _providersList = new ListBox { Margin = new Thickness(0, 0, 0, 10), Padding = new Thickness(8) };
            root.Children.Add(_providersList); Grid.SetRow(_providersList, 2);

            _status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 12), Foreground = new SolidColorBrush(Color.FromRgb(65, 84, 73)) };
            root.Children.Add(_status); Grid.SetRow(_status, 3);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var toggle = new Button { Content = "Enable / disable", Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 0, 8, 0) };
            toggle.Click += Toggle_Click;
            var remove = new Button { Content = "Remove selected", Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 0, 8, 0) };
            remove.Click += Remove_Click;
            var save = new Button { Content = "Save securely", Padding = new Thickness(16, 7, 16, 7), Background = new SolidColorBrush(Color.FromRgb(22, 134, 91)), Foreground = Brushes.White, BorderThickness = new Thickness(0) };
            save.Click += Save_Click;
            buttons.Children.Add(toggle); buttons.Children.Add(remove); buttons.Children.Add(save);
            root.Children.Add(buttons); Grid.SetRow(buttons, 4);
            Content = root;
            ProviderBox_SelectionChanged(null, null);
            RefreshList();
        }

        private void ProviderBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var choice = _providerBox.SelectedItem as ProviderChoice;
            if (choice == null) return;
            _keyBox.IsEnabled = choice.Id != "OpenLigaDB";
            _keyBox.Text = "";
            _keyBox.ToolTip = choice.Id == "OpenLigaDB" ? "This provider does not require a key." : "Paste the token/key from this provider's official account.";
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var choice = _providerBox.SelectedItem as ProviderChoice;
            if (choice == null) return;
            string key = (_keyBox.Text ?? "").Trim();
            if (choice.Id != "OpenLigaDB" && key.Length == 0)
            {
                MessageBox.Show(this, "This provider requires an API key/token.", "API settings", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (_providers.Any(p => p.Provider == choice.Id && p.Key == key))
            {
                _status.Text = "That provider/key combination is already in the list.";
                return;
            }
            _providers.Add(new ApiProviderConfig { Provider = choice.Id, Key = key, Enabled = true });
            RefreshList();
            _keyBox.Text = "";
            _status.Text = "Provider added in memory. Click Save securely to persist it.";
        }

        private void Toggle_Click(object sender, RoutedEventArgs e)
        {
            var selected = _providersList.SelectedItem as ApiProviderConfig;
            if (selected == null) return;
            selected.Enabled = !selected.Enabled;
            RefreshList();
        }
        private void Remove_Click(object sender, RoutedEventArgs e)
        {
            var selected = _providersList.SelectedItem as ApiProviderConfig;
            if (selected == null) return;
            _providers.Remove(selected);
            RefreshList();
        }
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ApiProviderStore.Save(_providers);
                _status.Text = "Saved " + _providers.Count + " provider configuration(s), encrypted for this Windows user.";
            }
            catch (Exception ex) { _status.Text = "Save failed: " + ex.Message; }
        }
        private void RefreshList()
        {
            _providersList.ItemsSource = null;
            _providersList.ItemsSource = _providers.ToList();
            _providersList.DisplayMemberPath = "DisplayName";
        }

        private sealed class ProviderChoice
        {
            public string Id { get; private set; }
            public string Label { get; private set; }
            public ProviderChoice(string id, string label) { Id = id; Label = label; }
            public override string ToString() { return Label; }
        }
    }

    public sealed class FixturesWindow : Window
    {
        private readonly ComboBox _providerBox;
        private readonly TextBox _leagueBox;
        private readonly TextBox _seasonBox;
        private readonly DataGrid _grid;
        private readonly TextBlock _status;

        public FixturesWindow()
        {
            Title = "KoaZa — Fixture Import";
            Width = 1000; Height = 650; MinWidth = 780; MinHeight = 480;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(244, 247, 245));
            var root = new Grid { Margin = new Thickness(18) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var top = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.7, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _providerBox = new ComboBox { Padding = new Thickness(6), Margin = new Thickness(0, 0, 8, 0) };
            _providerBox.Items.Add(new ProviderChoice("Auto", "Auto fallback (recommended)"));
            _providerBox.Items.Add(new ProviderChoice("ApiFootball", "API-Football"));
            _providerBox.Items.Add(new ProviderChoice("FootballData", "football-data.org"));
            _providerBox.Items.Add(new ProviderChoice("OpenLigaDB", "OpenLigaDB"));
            _providerBox.SelectedIndex = 0;
            _providerBox.SelectionChanged += ProviderChanged;
            Grid.SetColumn(_providerBox, 0); top.Children.Add(_providerBox);
            _leagueBox = new TextBox { Text = "39", Padding = new Thickness(8), Margin = new Thickness(0, 0, 8, 0), ToolTip = "API-Football: numeric league ID; football-data.org: competition code such as PL; OpenLigaDB: league shortcut such as bl1" };
            Grid.SetColumn(_leagueBox, 1); top.Children.Add(_leagueBox);
            _seasonBox = new TextBox { Text = DateTime.UtcNow.Month >= 7 ? DateTime.UtcNow.Year.ToString() : (DateTime.UtcNow.Year - 1).ToString(), Padding = new Thickness(8), Margin = new Thickness(0, 0, 8, 0) };
            Grid.SetColumn(_seasonBox, 2); top.Children.Add(_seasonBox);
            var fetch = new Button { Content = "Fetch & save fixtures", Padding = new Thickness(14, 8, 14, 8), Background = new SolidColorBrush(Color.FromRgb(22, 134, 91)), Foreground = Brushes.White, BorderThickness = new Thickness(0) };
            fetch.Click += Fetch_Click; Grid.SetColumn(fetch, 3); top.Children.Add(fetch);
            root.Children.Add(top); Grid.SetRow(top, 0);

            _grid = new DataGrid { IsReadOnly = true, AutoGenerateColumns = true, CanUserAddRows = false, Margin = new Thickness(0, 0, 0, 10) };
            root.Children.Add(_grid); Grid.SetRow(_grid, 1);
            _status = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(65, 84, 73)) };
            root.Children.Add(_status); Grid.SetRow(_status, 2);
            Content = root;
            ProviderChanged(null, null);
        }

        private void ProviderChanged(object sender, SelectionChangedEventArgs e)
        {
            var choice = _providerBox.SelectedItem as ProviderChoice;
            if (choice == null) return;
            _leagueBox.Text = choice.Id == "Auto" ? "EPL" : choice.Id == "ApiFootball" ? "39" : choice.Id == "FootballData" ? "PL" : "bl1";
            _leagueBox.ToolTip = choice.Id == "Auto" ? "Use standard code: EPL, LALIGA, BUNDESLIGA, SERIEA, LIGUE1, or IRAN. The app tries enabled providers in order." :
                choice.Id == "ApiFootball" ? "Enter a numeric API-Football league ID (e.g. 39 Premier League; Iran coverage depends on your plan)." :
                choice.Id == "FootballData" ? "Enter competition code (e.g. PL, PD, BL1, SA, FL1). Free plan has limited competitions." :
                "Enter OpenLigaDB league shortcut (e.g. bl1 for Bundesliga, bl2 for 2. Bundesliga).";
        }

        private void Fetch_Click(object sender, RoutedEventArgs e)
        {
            var choice = _providerBox.SelectedItem as ProviderChoice;
            int season;
            if (choice == null || !int.TryParse(_seasonBox.Text.Trim(), out season) || season < 2000 || season > 2100)
            {
                MessageBox.Show(this, "Enter a valid season year.", "Fixture import", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                _status.Text = "Requesting fixture data. Please wait...";
                var records = FootballDataService.Fetch(choice.Id, _leagueBox.Text.Trim(), season);
                DatabaseServiceProxy.SaveFixtures(records);
                _grid.ItemsSource = records;
                _status.Text = "Received " + records.Count + " fixture(s) from " + choice.Label + " and saved them to the local SQLite database. Empty scores mean the match has not finished or the provider does not expose a score.";
            }
            catch (Exception ex) { _status.Text = "Import failed: " + ex.Message; }
        }

        private sealed class ProviderChoice
        {
            public string Id { get; private set; }
            public string Label { get; private set; }
            public ProviderChoice(string id, string label) { Id = id; Label = label; }
            public override string ToString() { return Label; }
        }
    }

    internal static class DatabaseServiceProxy
    {
        public static void SaveFixtures(List<FixtureRecord> fixtures)
        {
            new KoaZa.Data.DatabaseService().SaveFixtures(fixtures);
        }
    }
}