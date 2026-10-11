using System;
using System.Globalization;
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
using KoaZa.Data;

namespace KoaZa.Services
{
    public sealed class ApiProviderConfig
    {
        public string Provider { get; set; }
        public string Key { get; set; }
        public bool Enabled { get; set; }
        public string BaseUrl { get; set; }
        public string HeaderName { get; set; }
        public string DisplayName
        {
            get
            {
                string label = Provider == "ApiFootball" || Provider == "ApiSports" ? "API-Football (API-SPORTS)" :
                    Provider == "FootballData" ? "Football-Data (football-data.org)" :
                    Provider == "OpenLigaDB" ? "OpenLigaDB (no key)" :
                    Provider == "OddsPapi" ? "OddsPapi" : Provider == "Floodlight" ? "Floodlight" :
                    Provider == "SportsOpenData" ? "Sports Open Data" : Provider == "SportsAPI360" ? "SportsAPI360" : Provider == "FiveDollarFootballAPI" ? "5DollarFootballAPI" : Provider == "FootballCharts" ? "Football-Charts" : Provider == "Sportmonks" ? "Sportmonks" : Provider == "TheOddsAPI" ? "The Odds API" : Provider == "TheRundown" ? "TheRundown.io" :
                    Provider == "TheSportsDB" ? "TheSportsDB" : Provider == "AllSportsApi" ? "AllSportsApi" : Provider == "FootballBettingOdds" ? "Football Betting Odds" : Provider == "Betfair" ? "Betfair API" : Provider == "Sportradar" ? "Sportradar" : Provider == "SportsDataIO" ? "SportsDataIO" : Provider == "ScoreBat" ? "ScoreBat" : Provider == "BallDontLie" ? "BALLDONTLIE" : Provider == "PremierLeagueStats" ? "Premier League Stats" : Provider;
                return label + (string.IsNullOrEmpty(Key) ? "" : " • " + Mask(Key)) +
                    (Enabled ? LocalizationService.T(" • enabled", " • فعال") : LocalizationService.T(" • disabled", " • غیرفعال"));
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
                    if (parts.Length != 3 && parts.Length != 5) continue;
                    try
                    {
                        result.Add(new ApiProviderConfig
                        {
                            Provider = Decode(parts[0]),
                            Key = Decode(parts[1]),
                            Enabled = Decode(parts[2]) == "1",
                            BaseUrl = parts.Length >= 5 ? Decode(parts[3]) : "",
                            HeaderName = parts.Length >= 5 ? Decode(parts[4]) : ""
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
            var lines = providers.Select(p => Encode(p.Provider) + "|" + Encode(p.Key ?? "") + "|" + Encode(p.Enabled ? "1" : "0") + "|" + Encode(p.BaseUrl ?? "") + "|" + Encode(p.HeaderName ?? ""));
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


    public static class CompetitionSelectionStore
    {
        private static readonly string Folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KoaZa");
        private static readonly string FilePath = Path.Combine(Folder, "hidden-competitions.txt");

        public static HashSet<string> LoadHidden()
        {
            try
            {
                if (!File.Exists(FilePath)) return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                return new HashSet<string>(File.ReadAllLines(FilePath).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()),
                    StringComparer.OrdinalIgnoreCase);
            }
            catch { return new HashSet<string>(StringComparer.OrdinalIgnoreCase); }
        }

        public static bool IsVisible(string competition)
        {
            return !LoadHidden().Contains((competition ?? "").Trim());
        }

        public static void SaveHidden(IEnumerable<string> hidden)
        {
            Directory.CreateDirectory(Folder);
            var values = (hidden ?? Enumerable.Empty<string>()).Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToArray();
            File.WriteAllLines(FilePath, values, Encoding.UTF8);
        }
    }

    public sealed class NewsItem
    {
        public string Title { get; set; }
        public string Url { get; set; }
        public string Summary { get; set; }
        public string Published { get; set; }
        public string Source { get; set; }
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
        public int? HomeCorners { get; set; }
        public int? AwayCorners { get; set; }
        public int? HomeShotsOnTarget { get; set; }
        public int? AwayShotsOnTarget { get; set; }
        public int? HomeYellowCards { get; set; }
        public int? AwayYellowCards { get; set; }
        public string Referee { get; set; }
        public int? VarReviews { get; set; }
    }

    public static class FootballDataService
    {

        // Bulk refresh: API-SPORTS date queries return fixtures across all competitions covered by the account.
        // The provider's coverage and quota still determine which competitions/data are available.
        public static List<FixtureRecord> FetchAllUpcoming(int season, DateTime startDate)
        {
            DateTime fromDate = startDate.Date;
            DateTime today = DateTime.UtcNow.Date;
            if (fromDate > today) throw new ArgumentOutOfRangeException("startDate", "Start date cannot be in the future.");
            DateTime toDate = today.AddDays(7);
            string from = fromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            string to = toDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            var combined = new List<FixtureRecord>();
            var errors = new List<string>();
            var providers = ApiProviderStore.Load().Where(p => p.Enabled).ToList();
            var apiKeys = providers.Where(p => (p.Provider == "ApiFootball" || p.Provider == "ApiSports") && !string.IsNullOrWhiteSpace(p.Key)).ToList();

            foreach (var config in apiKeys)
            {
                try
                {
                    // One bounded date-range request replaces dozens of daily calls. API-Football
                    // returns fixtures and completed scores across the competitions covered by this key.
                    combined.AddRange(FetchApiFootballPaged(
                        "/fixtures?from=" + from + "&to=" + to + "&timezone=UTC", config.Key));
                    if (combined.Count > 0) break;
                }
                catch (Exception ex) { errors.Add("API-SPORTS: " + ex.Message); }
            }

            if (providers.Any(p => p.Enabled && p.Provider == "FootballData" && !string.IsNullOrWhiteSpace(p.Key)))
            {
                try { combined.AddRange(FetchFootballDataBulk(season, fromDate)); }
                catch (Exception ex) { errors.Add("football-data.org: " + ex.Message); }
            }

            if (providers.Any(p => p.Enabled && p.Provider == "OpenLigaDB"))
            {
                try { combined.AddRange(FetchOpenLiga("bl1", season)); }
                catch (Exception ex) { errors.Add("OpenLigaDB: " + ex.Message); }
            }

            if (providers.Any(p => p.Enabled && p.Provider == "TheOddsAPI" && !string.IsNullOrWhiteSpace(p.Key)))
            {
                try { combined.AddRange(FetchTheOddsApi("soccer_epl")); }
                catch (Exception ex) { errors.Add("The Odds API: " + ex.Message); }
            }

            if (providers.Any(p => p.Enabled && p.Provider == "TheSportsDB"))
            {
                try { combined.AddRange(FetchTheSportsDb("4328", season)); }
                catch (Exception ex) { errors.Add("TheSportsDB: " + ex.Message); }
            }

            if (providers.Any(p => p.Enabled && p.Provider == "AllSportsApi" && !string.IsNullOrWhiteSpace(p.Key)))
            {
                try { combined.AddRange(FetchAllSportsApi("all")); }
                catch (Exception ex) { errors.Add("AllSportsApi: " + ex.Message); }
            }

            if (providers.Any(p => p.Enabled && p.Provider == "ScoreBat" && !string.IsNullOrWhiteSpace(p.Key)))
            {
                try { combined.AddRange(FetchScoreBat()); }
                catch (Exception ex) { errors.Add("ScoreBat: " + ex.Message); }
            }

            foreach (var config in providers.Where(p => p.Provider == "SportsAPI360" && !string.IsNullOrWhiteSpace(p.Key)))
            {
                try { combined.AddRange(FetchSportsApi360("all")); }
                catch (Exception ex) { errors.Add("SportsAPI360: " + ex.Message); }
            }

            if (providers.Any(p => p.Provider == "FiveDollarFootballAPI" && !string.IsNullOrWhiteSpace(p.Key)))
            {
                try { combined.AddRange(FetchFiveDollarRange("all", fromDate, toDate)); }
                catch (Exception ex) { errors.Add("5DollarFootballAPI: " + ex.Message); }
            }

            foreach (var config in providers.Where(p => !string.IsNullOrWhiteSpace(p.BaseUrl) &&
                p.Provider != "ApiFootball" && p.Provider != "ApiSports" && p.Provider != "FootballData" &&
                p.Provider != "OpenLigaDB" && p.Provider != "OddsPapi" && p.Provider != "SportsAPI360" &&
                p.Provider != "FiveDollarFootballAPI" && p.Provider != "FootballCharts" &&
                p.Provider != "TheOddsAPI" && p.Provider != "TheSportsDB" && p.Provider != "AllSportsApi" && p.Provider != "ScoreBat"))
            {
                try { combined.AddRange(FetchCustom(config)); }
                catch (Exception ex) { errors.Add(config.Provider + ": " + ex.Message); }
            }

            if (combined.Count == 0)
                throw new InvalidOperationException("No fixtures could be received from enabled providers. " + string.Join(" | ", errors));

            return FilterFixturesByDate(combined, fromDate, toDate)
                .GroupBy(FixtureDeduplicationKey)
                .Select(g => g.OrderByDescending(x => !string.IsNullOrWhiteSpace(x.League))
                    .ThenByDescending(x => !string.IsNullOrWhiteSpace(x.ExternalId)).First())
                .ToList();
        }

        // Date-based import entry point used by the fixture-import window.
        public static List<FixtureRecord> FetchFromDate(string provider, string league, int season, DateTime startDate)
        {
            return FetchFromDate(provider, league, season, startDate, DateTime.UtcNow.Date.AddDays(7));
        }

        public static List<FixtureRecord> FetchFromDate(string provider, string league, int season, DateTime startDate, DateTime endDate)
        {
            DateTime fromDate = startDate.Date;
            DateTime toDate = endDate.Date;
            DateTime today = DateTime.UtcNow.Date;
            if (fromDate > today) throw new ArgumentOutOfRangeException("startDate", "Start date cannot be in the future.");
            if (toDate < fromDate) throw new ArgumentOutOfRangeException("endDate", "End date must be on or after the start date.");
            if (toDate > today.AddDays(7)) throw new ArgumentOutOfRangeException("endDate", "End date cannot be more than 7 days in the future.");
            List<FixtureRecord> rows;
            if (provider == "Auto")
                rows = FetchAllUpcoming(season, fromDate);
            else if (provider == "ApiFootball" || provider == "ApiSports")
                rows = FetchApiFootballRange(league, fromDate, toDate);
            else if (provider == "FiveDollarFootballAPI")
                rows = FetchFiveDollarRange(league, fromDate, toDate);
            else if (provider == "FootballCharts")
                rows = FetchFootballCharts(league, season);
            else if (provider == "AllSportsApi")
                rows = FetchAllSportsApi(league, fromDate, toDate);
            else if (provider == "FootballData" && (string.IsNullOrWhiteSpace(league) || league.Trim().Equals("all", StringComparison.OrdinalIgnoreCase)))
                rows = FetchFootballDataBulk(season, fromDate);
            else
                rows = Fetch(provider, league, season);

            return FilterFixturesByDate(rows, fromDate, toDate)
                .GroupBy(FixtureDeduplicationKey)
                .Select(g => g.OrderByDescending(x => !string.IsNullOrWhiteSpace(x.ExternalId)).First())
                .ToList();
        }

        private static List<FixtureRecord> FilterFixturesByDate(IEnumerable<FixtureRecord> fixtures, DateTime fromDate, DateTime toDateExclusive)
        {
            return (fixtures ?? Enumerable.Empty<FixtureRecord>()).Where(f =>
            {
                DateTime kickoff;
                return DateTime.TryParse(f.KickoffUtc, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out kickoff)
                    && kickoff >= fromDate && kickoff < toDateExclusive.AddDays(1);
            }).ToList();
        }

        private static List<FixtureRecord> FetchApiFootballRange(string league, DateTime fromDate, DateTime toDate)
        {
            var configs = ApiProviderStore.Load().Where(p => p.Enabled &&
                (p.Provider == "ApiFootball" || p.Provider == "ApiSports") && !string.IsNullOrWhiteSpace(p.Key)).ToList();
            if (configs.Count == 0) throw new InvalidOperationException("No enabled API-Football key is saved. Open API Settings.");
            string path = "/fixtures?" +
                (string.IsNullOrWhiteSpace(league) || league.Trim().Equals("all", StringComparison.OrdinalIgnoreCase)
                    ? "" : "league=" + Uri.EscapeDataString(league.Trim()) + "&") +
                "from=" + fromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) +
                "&to=" + toDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "&timezone=UTC";
            Exception last = null;
            foreach (var config in configs)
            {
                try
                {
                    return FetchApiFootballPaged(path, config.Key);
                }
                catch (Exception ex) { last = ex; }
            }
            throw new InvalidOperationException("All enabled API-Football keys failed for the requested date range. Check league ID, key and quota.", last);
        }

        private static List<FixtureRecord> FetchApiFootballPaged(string path, string apiKey)
        {
            var output = new List<FixtureRecord>();
            int page = 1;
            int totalPages = 1;
            do
            {
                string separator = path.IndexOf('?') >= 0 ? "&" : "?";
                var root = Parse(Request("https://v3.football.api-sports.io" + path + separator + "page=" + page, "x-apisports-key", apiKey));
                if (HasErrors(root))
                {
                    object apiErrors;
                    root.TryGetValue("errors", out apiErrors);
                    string details = apiErrors == null ? "" : new JavaScriptSerializer().Serialize(apiErrors);
                    throw new InvalidOperationException("API-SPORTS rejected page " + page + (string.IsNullOrWhiteSpace(details) ? "." : ": " + details) +
                        " Check the API key, plan permissions, league ID and quota; narrow the date range if needed.");
                }
                output.AddRange(ParseApiFootballFixtures(root));
                var paging = GetMap(root, "paging");
                int parsedTotal;
                if (int.TryParse(GetString(paging, "total", "1"), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedTotal))
                    totalPages = Math.Max(1, parsedTotal);
                page++;
            }
            while (page <= totalPages);
            return output;
        }

        private static string FixtureDeduplicationKey(FixtureRecord fixture)
        {
            DateTime kickoff;
            string time = DateTime.TryParse(fixture.KickoffUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out kickoff)
                ? kickoff.ToUniversalTime().ToString("yyyyMMddHHmm", CultureInfo.InvariantCulture)
                : (fixture.KickoffUtc ?? "").Trim().ToLowerInvariant();
            string home = (fixture.HomeTeam ?? "").Trim().ToLowerInvariant();
            string away = (fixture.AwayTeam ?? "").Trim().ToLowerInvariant();
            if (time.Length == 0 && !string.IsNullOrWhiteSpace(fixture.ExternalId))
                return (fixture.Provider ?? "") + "|" + fixture.ExternalId;
            return time + "|" + home + "|" + away;
        }

        private static List<FixtureRecord> ParseApiFootballFixtures(Dictionary<string, object> root)
        {
            var output = new List<FixtureRecord>();
            foreach (var item in GetList(root, "response"))
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
                    League = GetString(leagueMap, "name", GetString(leagueMap, "country", "Competition")),
                    KickoffUtc = GetString(fixture, "date", ""),
                    HomeTeam = GetString(home, "name", "Home"),
                    AwayTeam = GetString(away, "name", "Away"),
                    HomeGoals = GetNullableInt(goals, "home"),
                    AwayGoals = GetNullableInt(goals, "away"),
                    Status = GetString(GetMap(fixture, "status"), "short", ""),
                    ExternalId = GetString(fixture, "id", ""),
                    Referee = GetString(fixture, "referee", "")
                });
            }
            return output;
        }

        public static string FetchFixtureDetails(FixtureRecord fixture)
        {
            if (fixture == null) throw new ArgumentNullException("fixture");
            if (fixture.Provider == "FiveDollarFootballAPI")
            {
                var fiveDollarKeys = ApiProviderStore.Load().Where(p => p.Enabled && p.Provider == "FiveDollarFootballAPI" && !string.IsNullOrWhiteSpace(p.Key)).ToList();
                if (fiveDollarKeys.Count == 0) throw new InvalidOperationException("Enable 5DollarFootballAPI and save its bearer key first.");
                Exception lastFiveDollarError = null;
                foreach (var config in fiveDollarKeys)
                {
                    try
                    {
                        string json = Request("https://api.5dollarfootballapi.com/v1/fixtures/" + Uri.EscapeDataString(fixture.ExternalId) + "?include=stats,events", "Authorization", "Bearer " + config.Key);
                        var root = Parse(json);
                        if (HasErrors(root)) throw new InvalidOperationException("5DollarFootballAPI returned an API/quota error.");
                        root["detailsVersion"] = 2;
                        return new JavaScriptSerializer().Serialize(root);
                    }
                    catch (Exception ex) { lastFiveDollarError = ex; }
                }
                throw new InvalidOperationException("Could not retrieve 5DollarFootballAPI fixture details.", lastFiveDollarError);
            }
            if (fixture.Provider != "API-Football" && fixture.Provider != "ApiFootball" && fixture.Provider != "ApiSports")
                throw new InvalidOperationException("Detailed statistics are currently available through the API-SPORTS/API-Football adapter. Other providers may not expose a compatible fixture-details endpoint.");
            int fixtureId;
            if (!int.TryParse(fixture.ExternalId, out fixtureId))
                throw new InvalidOperationException("This match does not have a numeric API-SPORTS fixture ID.");
            var configs = ApiProviderStore.Load().Where(p => p.Enabled && (p.Provider == "ApiFootball" || p.Provider == "ApiSports") && !string.IsNullOrWhiteSpace(p.Key)).ToList();
            if (configs.Count == 0) throw new InvalidOperationException("Enable an API-SPORTS provider to retrieve detailed match data.");
            Exception last = null;
            foreach (var config in configs)
            {
                try
                {
                    string json = Request("https://v3.football.api-sports.io/fixtures?id=" + fixtureId, "x-apisports-key", config.Key);
                    var root = Parse(json);
                    if (HasErrors(root)) throw new InvalidOperationException("API-SPORTS returned a quota/API error.");
                    root["detailsVersion"] = 2;

                    string status = (fixture.Status ?? "").Trim().ToUpperInvariant();
                    bool live = status == "LIVE" || status == "1H" || status == "HT" || status == "2H" || status == "ET" || status == "P" || status == "BT" || status == "INT";
                    bool completed = status == "FT" || status == "AET" || status == "PEN" || status == "FINISHED";

                    // Do not spend quota on event/stat endpoints for a match that has not started.
                    // For live or completed fixtures, these calls are best-effort by plan/competition.
                    if (live || completed)
                    {
                        try
                        {
                            var statsPayload = Parse(Request("https://v3.football.api-sports.io/fixtures/statistics?fixture=" + fixtureId, "x-apisports-key", config.Key));
                            if (!HasErrors(statsPayload)) root["statisticsPayload"] = statsPayload;
                        }
                        catch { }
                        try
                        {
                            var eventsPayload = Parse(Request("https://v3.football.api-sports.io/fixtures/events?fixture=" + fixtureId, "x-apisports-key", config.Key));
                            if (!HasErrors(eventsPayload)) root["eventsPayload"] = eventsPayload;
                        }
                        catch { }
                    }

                    // Injuries are a separate API resource; attach them when available without failing the fixture details request.
                    if (!live && !completed)
                    {
                        try
                        {
                            var injuryRoot = Parse(Request("https://v3.football.api-sports.io/injuries?fixture=" + fixtureId, "x-apisports-key", config.Key));
                            if (!HasErrors(injuryRoot)) root["injuriesPayload"] = injuryRoot;
                        }
                        catch { /* Some plans/competitions do not expose injury data. */ }
                    }
                    return new JavaScriptSerializer().Serialize(root);
                }
                catch (Exception ex) { last = ex; }
            }
            throw new InvalidOperationException("Could not retrieve fixture statistics/events/lineups. Check API key, quota and competition coverage.", last);
        }

        public static FixtureRecord ExtractFixtureStatistics(string json, FixtureRecord fixture)
        {
            if (fixture == null) return null;
            var result = new FixtureRecord { Provider = fixture.Provider, ExternalId = fixture.ExternalId };
            try
            {
                var root = Parse(json);
                var fixtureRows = GetList(root, "response");
                var match = fixtureRows.FirstOrDefault();
                var fixtureInfo = GetMap(match, "fixture");
                result.Referee = GetString(fixtureInfo, "referee", fixture.Referee ?? "");
                var statsPayload = GetMap(root, "statisticsPayload");
                var teamsStats = GetList(statsPayload, "response");
                foreach (var teamStats in teamsStats)
                {
                    string team = GetString(GetMap(teamStats, "team"), "name", "");
                    bool isHome = string.Equals(team, fixture.HomeTeam, StringComparison.OrdinalIgnoreCase);
                    bool isAway = string.Equals(team, fixture.AwayTeam, StringComparison.OrdinalIgnoreCase);
                    if (!isHome && !isAway) continue;
                    foreach (var stat in GetList(teamStats, "statistics"))
                    {
                        string type = GetString(stat, "type", "").Trim().ToLowerInvariant();
                        int? value = GetNullableInt(stat, "value");
                        if (!value.HasValue) continue;
                        if (type.Contains("corner")) { if (isHome) result.HomeCorners = value; else result.AwayCorners = value; }
                        else if (type.Contains("shots on goal") || type.Contains("shots on target")) { if (isHome) result.HomeShotsOnTarget = value; else result.AwayShotsOnTarget = value; }
                        else if (type.Contains("yellow card")) { if (isHome) result.HomeYellowCards = value; else result.AwayYellowCards = value; }
                    }
                }
                var eventsPayload = GetMap(root, "eventsPayload");
                var events = GetList(eventsPayload, "response");
                if (eventsPayload.Count > 0)
                    result.VarReviews = events.Count(ev =>
                    {
                        string type = GetString(ev, "type", "").ToLowerInvariant();
                        string detail = GetString(ev, "detail", "").ToLowerInvariant();
                        return type.Contains("var") || detail.Contains("var");
                    });
            }
            catch { return result; }
            return result;
        }

        public static string SummarizeFixtureDetails(string json)
        {
            var root = Parse(json);
            var item = GetList(root, "response").FirstOrDefault();
            if (item == null) return LocalizationService.T("No detailed statistics are available for this match yet.", "هنوز آمار تفصیلی برای این مسابقه منتشر نشده است.");
            var sb = new StringBuilder();
            var stats = GetList(item, "statistics");
            if (stats.Count == 0) stats = GetList(GetMap(root, "statisticsPayload"), "response");
            if (stats.Count > 0)
            {
                sb.AppendLine("MATCH STATISTICS");
                foreach (var teamStats in stats)
                {
                    string team = GetString(GetMap(teamStats, "team"), "name", "Team");
                    sb.AppendLine(team + ":");
                    foreach (var stat in GetList(teamStats, "statistics"))
                    {
                        string type = GetString(stat, "type", "");
                        string value = GetString(stat, "value", "—");
                        if (!string.IsNullOrWhiteSpace(type)) sb.AppendLine("  " + type + ": " + value);
                    }
                }
            }
            var events = GetList(item, "events");
            if (events.Count == 0) events = GetList(GetMap(root, "eventsPayload"), "response");
            if (events.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("MATCH EVENTS");
                foreach (var ev in events.Take(30))
                {
                    string time = GetString(GetMap(ev, "time"), "elapsed", "");
                    string team = GetString(GetMap(ev, "team"), "name", "");
                    string type = GetString(ev, "type", "");
                    string detail = GetString(ev, "detail", "");
                    string player = GetString(GetMap(ev, "player"), "name", "");
                    sb.AppendLine((time.Length > 0 ? time + "' " : "") + team + " — " + type + (detail.Length > 0 ? " / " + detail : "") + (player.Length > 0 ? " (" + player + ")" : ""));
                }
            }
            var lineups = GetList(item, "lineups");
            if (lineups.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("LINEUPS");
                foreach (var lineup in lineups)
                    sb.AppendLine(GetString(GetMap(lineup, "team"), "name", "Team") + ": " + GetString(lineup, "formation", "formation unavailable") + " — Coach: " + GetString(GetMap(lineup, "coach"), "name", "unknown"));
            }
            var players = GetList(item, "players");
            if (players.Count > 0) sb.AppendLine("Player-level data is included in the saved API response.");
            var injuryPayload = GetMap(root, "injuriesPayload");
            var injuries = GetList(injuryPayload, "response");
            if (injuries.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("INJURIES / AVAILABILITY");
                foreach (var injury in injuries.Take(30))
                {
                    string player = GetString(GetMap(injury, "player"), "name", "Player");
                    string team = GetString(GetMap(injury, "team"), "name", "");
                    string type = GetString(GetMap(injury, "player"), "type", "");
                    string reason = GetString(GetMap(injury, "player"), "reason", "");
                    sb.AppendLine(player + (team.Length > 0 ? " — " + team : "") +
                        (type.Length > 0 ? " — " + type : "") + (reason.Length > 0 ? " / " + reason : ""));
                }
            }
            if (stats.Count == 0 && events.Count == 0 && lineups.Count == 0 && injuries.Count == 0)
                sb.AppendLine("The provider returned the fixture but no statistics/events/lineups for this competition or match.");
            sb.AppendLine();
            sb.Append("Coverage depends on the competition and API plan; missing fields are not assumed to be zero.");
            string summary = sb.ToString();
            return LocalizationService.IsPersian ? TranslateFixtureSummary(summary) : summary;
        }

        public static string SummarizeFixtureDetails(string json, FixtureRecord fixture)
        {
            var root = Parse(json);
            if (GetList(root, "response").Count > 0) return SummarizeFixtureDetails(json);
            var s = MatchStatisticsParser.ParseFiveDollarOrGeneric(json, fixture);
            if (s == null) return LocalizationService.T("No detailed statistics are available for this match yet.", "هنوز آمار تفصیلی برای این مسابقه منتشر نشده است.");
            var sb = new StringBuilder();
            sb.AppendLine("MATCH STATISTICS");
            if (s.HomeShots.HasValue || s.AwayShots.HasValue) sb.AppendLine("Total shots: " + fixture.HomeTeam + " " + Value(s.HomeShots) + " — " + fixture.AwayTeam + " " + Value(s.AwayShots));
            if (s.HomeShotsOnTarget.HasValue || s.AwayShotsOnTarget.HasValue) sb.AppendLine("Shots on target: " + fixture.HomeTeam + " " + Value(s.HomeShotsOnTarget) + " — " + fixture.AwayTeam + " " + Value(s.AwayShotsOnTarget));
            if (s.HomeCorners.HasValue || s.AwayCorners.HasValue) sb.AppendLine("Corner kicks: " + fixture.HomeTeam + " " + Value(s.HomeCorners) + " — " + fixture.AwayTeam + " " + Value(s.AwayCorners));
            if (s.HomeYellowCards.HasValue || s.AwayYellowCards.HasValue) sb.AppendLine("Yellow cards: " + fixture.HomeTeam + " " + Value(s.HomeYellowCards) + " — " + fixture.AwayTeam + " " + Value(s.AwayYellowCards));
            if (s.HomeRedCards.HasValue || s.AwayRedCards.HasValue) sb.AppendLine("Red cards: " + fixture.HomeTeam + " " + Value(s.HomeRedCards) + " — " + fixture.AwayTeam + " " + Value(s.AwayRedCards));
            if (s.HomeFouls.HasValue || s.AwayFouls.HasValue) sb.AppendLine("Fouls: " + fixture.HomeTeam + " " + Value(s.HomeFouls) + " — " + fixture.AwayTeam + " " + Value(s.AwayFouls));
            if (!string.IsNullOrWhiteSpace(s.Referee)) sb.AppendLine("Referee: " + s.Referee);
            if (sb.ToString().Trim().Equals("MATCH STATISTICS", StringComparison.OrdinalIgnoreCase))
                return LocalizationService.T("The provider returned the match but no supported event-stat fields. Missing values are not assumed to be zero.", "سرویس مسابقه را برگرداند اما آمار رویدادی قابل‌استفاده‌ای نداشت؛ مقادیر ناموجود صفر فرض نمی‌شوند.");
            sb.AppendLine();
            sb.Append("Missing fields are not assumed to be zero.");
            string summary = sb.ToString();
            return LocalizationService.IsPersian ? TranslateFixtureSummary(summary) : summary;
        }

        private static string Value(int? value) { return value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "—"; }

        private static string TranslateFixtureSummary(string summary)
        {
            var phrases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                {"MATCH STATISTICS", "آمار مسابقه"}, {"MATCH EVENTS", "رویدادهای مسابقه"},
                {"LINEUPS", "ترکیب تیم‌ها"}, {"INJURIES / AVAILABILITY", "مصدومان و وضعیت حضور"},
                {"Player-level data is included in the saved API response.", "اطلاعات بازیکنان در دادهٔ ذخیره‌شدهٔ سرویس وجود دارد."},
                {"The provider returned the fixture but no statistics/events/lineups for this competition or match.", "سرویس، مسابقه را برگرداند اما برای این رقابت یا مسابقه آمار، رویداد یا ترکیب ارائه نکرد."},
                {"Coverage depends on the competition and API plan; missing fields are not assumed to be zero.", "پوشش اطلاعات به رقابت و سطح اشتراک سرویس بستگی دارد؛ فیلدهای خالی به معنی صفر نیستند."},
                {"Shots on Goal", "شوت در چارچوب"}, {"Shots off Goal", "شوت خارج از چارچوب"},
                {"Total Shots", "مجموع شوت‌ها"}, {"Blocked Shots", "شوت‌های بلوکه‌شده"},
                {"Shots insidebox", "شوت داخل محوطه"}, {"Shots inside Box", "شوت داخل محوطه"},
                {"Shots outsidebox", "شوت خارج محوطه"}, {"Shots outside Box", "شوت خارج محوطه"},
                {"Fouls", "خطاها"}, {"Corner Kicks", "ضربات کرنر"}, {"Offsides", "آفساید"},
                {"Ball Possession", "مالکیت توپ"}, {"Yellow Cards", "کارت زرد"}, {"Red Cards", "کارت قرمز"},
                {"Goalkeeper Saves", "مهار دروازه‌بان"}, {"Total passes", "مجموع پاس‌ها"},
                {"Passes accurate", "پاس صحیح"}, {"Passes %", "درصد پاس صحیح"}, {"Expected Goals", "گل مورد انتظار"},
                {"Goal", "گل"}, {"Card", "کارت"}, {"subst", "تعویض"}, {"Penalty", "پنالتی"},
                {"Missed Penalty", "پنالتی از دست‌رفته"}, {"Yellow Card", "کارت زرد"}, {"Red Card", "کارت قرمز"},
                {"Substitution", "تعویض"}, {"Var", "بازبینی VAR"}, {"Team", "تیم"},
                {"formation unavailable", "ترکیب‌بندی نامشخص"}, {"unknown", "نامشخص"}, {"Coach:", "سرمربی:"}
            };
            foreach (var phrase in phrases)
                summary = System.Text.RegularExpressions.Regex.Replace(summary,
                    System.Text.RegularExpressions.Regex.Escape(phrase.Key),
                    phrase.Value, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return summary;
        }

        public static List<NewsItem> FetchLatestNews(string competition)
        {
            var result = new List<NewsItem>();
            string target = (competition ?? "").Trim();
            if (target.Length == 0) return result;

            // Query each requested news source for the selected competition only. Do not mix
            // generic football headlines into a competition-specific news panel.
            string quoted = "\"" + target.Replace("\\", " ").Replace("\"", " ").Trim() + "\"";
            string locale = LocalizationService.IsPersian ? " football OR فوتبال" : " football";
            FetchSiteNews(result, "site:football360.ir " + quoted + locale, "Football360.ir / فوتبال ۳۶۰");
            FetchSiteNews(result, "site:fbtv3.ir " + quoted + locale, "FBTV3.ir / فوتبال برتر");
            FetchSiteNews(result, "site:varzesh3.com " + quoted + locale, "Varzesh3 / ورزش سه");
            FetchSiteNews(result, quoted + locale, "Google News");
            return result.OrderByDescending(n => ParseNewsDate(n.Published)).Take(18).ToList();
        }

        public static List<NewsItem> FetchLatestNews()
        {
            var result = new List<NewsItem>();
            // Use public RSS where available; for Football360 and FBTV3 use Google News RSS
            // domain queries so the app does not scrape article pages or bypass access controls.
            foreach (string feed in new[] { "https://www.varzesh3.com/rss/all", "https://www.varzesh3.com/rss/foreignFootball" })
            {
                try
                {
                    var doc = new System.Xml.XmlDocument();
                    doc.LoadXml(Request(feed, null, null));
                    var nodes = doc.SelectNodes("//item");
                    if (nodes == null) continue;
                    foreach (System.Xml.XmlNode node in nodes)
                    {
                        string title = node["title"] == null ? "" : System.Net.WebUtility.HtmlDecode(node["title"].InnerText);
                        string link = node["link"] == null ? "" : node["link"].InnerText;
                        string description = node["description"] == null ? "" : System.Net.WebUtility.HtmlDecode(node["description"].InnerText);
                        string pubDate = node["pubDate"] == null ? "" : node["pubDate"].InnerText;
                        if (title.Length == 0 || link.Length == 0 || result.Any(n => n.Url == link)) continue;
                        result.Add(new NewsItem { Title = title, Url = link, Summary = (string.IsNullOrWhiteSpace(pubDate) ? "" : pubDate + " — ") + StripHtml(description), Published = pubDate, Source = "Varzesh3 / ورزش سه" });
                        if (result.Count >= 6) break;
                    }
                }
                catch { }
                if (result.Count >= 6) break;
            }

            FetchSiteNews(result, "site:football360.ir football OR site:football360.ir فوتبال", "Football360.ir / فوتبال ۳۶۰");
            FetchSiteNews(result, "site:fbtv3.ir football OR site:fbtv3.ir فوتبال", "FBTV3.ir / فوتبال برتر");
            FetchSiteNews(result, LocalizationService.IsPersian ? "اخبار فوتبال ایران و جهان" : "football news today", "Google News");
            return result.OrderByDescending(n => ParseNewsDate(n.Published)).Take(18).ToList();
        }

        private static void FetchSiteNews(List<NewsItem> result, string query, string source)
        {
            if (result == null || result.Count >= 18) return;
            string locale = LocalizationService.IsPersian ? "&hl=fa&gl=IR&ceid=IR:fa" : "&hl=en-US&gl=US&ceid=US:en";
            string url = "https://news.google.com/rss/search?q=" + Uri.EscapeDataString(query) + locale;
            try
            {
                var doc = new System.Xml.XmlDocument();
                doc.LoadXml(Request(url, null, null));
                var nodes = doc.SelectNodes("//item");
                if (nodes == null) return;
                foreach (System.Xml.XmlNode node in nodes)
                {
                    string title = node["title"] == null ? "" : System.Net.WebUtility.HtmlDecode(node["title"].InnerText);
                    string link = node["link"] == null ? "" : node["link"].InnerText;
                    string description = node["description"] == null ? "" : System.Net.WebUtility.HtmlDecode(node["description"].InnerText);
                    string pubDate = node["pubDate"] == null ? "" : node["pubDate"].InnerText;
                    if (title.Length == 0 || link.Length == 0 || result.Any(n => n.Url == link)) continue;
                    result.Add(new NewsItem { Title = title, Url = link, Summary = (string.IsNullOrWhiteSpace(pubDate) ? "" : pubDate + " — ") + StripHtml(description), Published = pubDate, Source = source });
                    if (result.Count >= 18) break;
                }
            }
            catch { }
        }

        private static DateTime ParseNewsDate(string value)
        {
            DateTime date;
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out date) ? date : DateTime.MinValue;
        }

        public static List<NewsItem> FetchMatchNews(string homeTeam, string awayTeam, string league)
        {
            var result = new List<NewsItem>();
            // Varzesh3 is preferred for Persian football coverage; the public RSS feed is used
            // without scraping article pages or bypassing site controls.
            try { result.AddRange(FetchVarzesh3News(homeTeam, awayTeam, league)); } catch { }
            string query = "\"" + (homeTeam ?? "").Trim() + "\" OR \"" + (awayTeam ?? "").Trim() + "\" football";
            if (!string.IsNullOrWhiteSpace(league)) query += " \"" + league.Trim() + "\"";
            string locale = LocalizationService.IsPersian ? "&hl=fa&gl=IR&ceid=IR:fa" : "&hl=en-US&gl=US&ceid=US:en";
            string url = "https://news.google.com/rss/search?q=" + Uri.EscapeDataString(query) + locale;
            try
            {
                var doc = new System.Xml.XmlDocument();
                doc.LoadXml(Request(url, null, null));
                var nodes = doc.SelectNodes("//item");
                if (nodes != null)
                {
                    foreach (System.Xml.XmlNode node in nodes)
                    {
                        string title = node["title"] == null ? "" : System.Net.WebUtility.HtmlDecode(node["title"].InnerText);
                        string link = node["link"] == null ? "" : node["link"].InnerText;
                        string description = node["description"] == null ? "" : System.Net.WebUtility.HtmlDecode(node["description"].InnerText);
                        string pubDate = node["pubDate"] == null ? "" : node["pubDate"].InnerText;
                        if (title.Length == 0 || link.Length == 0 || result.Any(n => n.Url == link)) continue;
                        result.Add(new NewsItem { Title = title, Url = link, Summary = (string.IsNullOrWhiteSpace(pubDate) ? "" : pubDate + " — ") + StripHtml(description), Published = pubDate, Source = "Google News" });
                        if (result.Count >= 8) break;
                    }
                }
            }
            catch { if (result.Count == 0) throw; }
            return result.Take(8).ToList();
        }

        private static List<NewsItem> FetchVarzesh3News(string homeTeam, string awayTeam, string league)
        {
            var result = new List<NewsItem>();
            var terms = ((homeTeam ?? "") + " " + (awayTeam ?? "") + " " + (league ?? ""))
                .Split(new[] { ' ', '-', '_', '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length >= 3).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            foreach (string feed in new[] { "https://www.varzesh3.com/rss/all", "https://www.varzesh3.com/rss/foreignFootball" })
            {
                try
                {
                    var doc = new System.Xml.XmlDocument();
                    doc.LoadXml(Request(feed, null, null));
                    var nodes = doc.SelectNodes("//item");
                    if (nodes == null) continue;
                    foreach (System.Xml.XmlNode node in nodes)
                    {
                        string title = node["title"] == null ? "" : System.Net.WebUtility.HtmlDecode(node["title"].InnerText);
                        string link = node["link"] == null ? "" : node["link"].InnerText;
                        string description = node["description"] == null ? "" : System.Net.WebUtility.HtmlDecode(node["description"].InnerText);
                        string pubDate = node["pubDate"] == null ? "" : node["pubDate"].InnerText;
                        string haystack = (title + " " + StripHtml(description)).ToLowerInvariant();
                        bool relevant = terms.Any(term => haystack.Contains(term.ToLowerInvariant()));
                        if (!relevant || title.Length == 0 || link.Length == 0 || result.Any(n => n.Url == link)) continue;
                        result.Add(new NewsItem { Title = title, Url = link, Summary = (string.IsNullOrWhiteSpace(pubDate) ? "" : pubDate + " — ") + StripHtml(description), Published = pubDate, Source = "Varzesh3 / ورزش سه" });
                        if (result.Count >= 4) return result;
                    }
                }
                catch { }
            }
            return result;
        }

        private static string StripHtml(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            return System.Text.RegularExpressions.Regex.Replace(value, "<[^>]+>", " ").Replace("&nbsp;", " ").Trim();
        }
        public static List<FixtureRecord> Fetch(string provider, string league, int season)
        {
            if (provider == "Auto") return FetchAuto(league, season);
            if (provider == "ApiFootball" || provider == "ApiSports") return FetchApiFootball(league, season);
            if (provider == "FootballData") return FetchFootballData(league, season);
            if (provider == "OpenLigaDB") return FetchOpenLiga(league, season);
            if (provider == "OddsPapi") return FetchOddsPapi(league);
            if (provider == "TheOddsAPI") return FetchTheOddsApi(league);
            if (provider == "TheSportsDB") return FetchTheSportsDb(league, season);
            if (provider == "AllSportsApi") return FetchAllSportsApi(league);
            if (provider == "ScoreBat") return FetchScoreBat();
            if (provider == "SportsAPI360") return FetchSportsApi360(league);
            if (provider == "FiveDollarFootballAPI") return FetchFiveDollarRange(league, DateTime.UtcNow.Date.AddDays(-7), DateTime.UtcNow.Date.AddDays(7));
            if (provider == "FootballCharts") return FetchFootballCharts(league, season);
            var custom = ApiProviderStore.Load().FirstOrDefault(p => p.Enabled && p.Provider == provider && !string.IsNullOrWhiteSpace(p.BaseUrl));
            if (custom != null) return FetchCustom(custom);
            throw new InvalidOperationException("This provider is configured but does not yet have a built-in adapter. For Floodlight or Sports Open Data, enter the provider's HTTPS fixtures endpoint or use a custom provider with the documented normalized JSON format.");
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
                if (providerLeague == null)
                {
                    var customConfig = ApiProviderStore.Load().FirstOrDefault(p => p.Enabled && p.Provider == provider && !string.IsNullOrWhiteSpace(p.BaseUrl));
                    if (customConfig != null) providerLeague = canonical;
                    else if (provider == "SportsAPI360") providerLeague = "all";
                    else continue;
                }
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
                "For automatic fallback, use a known competition code (EPL, LALIGA, BUNDESLIGA, SERIEA, LIGUE1, IRAN), configure a normalized HTTPS endpoint, or use a provider-specific tournament ID. " +
                string.Join(" | ", errors));
        }

        private static string MapLeague(string provider, string competition)
        {
            if (provider == "ApiFootball" || provider == "ApiSports")
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
            if (provider == "FootballCharts" && (competition == "EPL" || competition == "PL")) return "premier";
            return null;
        }

        private static List<FixtureRecord> FetchFiveDollarRange(string league, DateTime fromDate, DateTime toDate)
        {
            var configs = ApiProviderStore.Load().Where(p => p.Enabled && p.Provider == "FiveDollarFootballAPI" && !string.IsNullOrWhiteSpace(p.Key)).ToList();
            if (configs.Count == 0) throw new InvalidOperationException("Add an enabled 5DollarFootballAPI key in API Settings.");
            string leagueValue = (league ?? "").Trim();
            bool numericLeague = long.TryParse(leagueValue, out _);
            if (!numericLeague && (toDate.Date - fromDate.Date).TotalDays > 31)
                throw new InvalidOperationException("For date ranges longer than 31 days, enter the numeric league ID so the API can use its season/league endpoint without one request per day. 'all' across long history may exhaust the daily quota.");

            var output = new List<FixtureRecord>();
            Exception last = null;
            foreach (var config in configs)
            {
                try
                {
                    if (numericLeague && (toDate.Date - fromDate.Date).TotalDays > 1)
                    {
                        long startTime = new DateTimeOffset(DateTime.SpecifyKind(fromDate.Date, DateTimeKind.Utc)).ToUnixTimeSeconds();
                        long endTime = new DateTimeOffset(DateTime.SpecifyKind(toDate.Date.AddDays(1), DateTimeKind.Utc)).ToUnixTimeSeconds();
                        int page = 1;
                        bool more;
                        do
                        {
                            string url = "https://api.5dollarfootballapi.com/v1/leagues/" + Uri.EscapeDataString(leagueValue) +
                                "/fixtures?start_time=" + startTime + "&end_time=" + endTime + "&include=stats,events&per_page=50&page=" + page;
                            var request = (HttpWebRequest)WebRequest.Create(url);
                            request.Method = "GET"; request.Accept = "application/json"; request.Timeout = 20000; request.ReadWriteTimeout = 20000;
                            request.Headers[HttpRequestHeader.Authorization] = "Bearer " + config.Key;
                            var root = Parse(ReadResponse(request));
                            if (HasErrors(root)) throw new InvalidOperationException("5DollarFootballAPI returned an API/quota error.");
                            output.AddRange(ParseFiveDollarFixtures(root, leagueValue));
                            var pagination = GetMap(root, "pagination");
                            more = GetString(pagination, "has_more", "false").Equals("true", StringComparison.OrdinalIgnoreCase);
                            page++;
                        } while (more && page <= 100);
                    }
                    else
                    {
                        for (DateTime day = fromDate.Date; day <= toDate.Date; day = day.AddDays(1))
                        {
                            long startTime = new DateTimeOffset(DateTime.SpecifyKind(day, DateTimeKind.Utc)).ToUnixTimeSeconds();
                            long endTime = new DateTimeOffset(DateTime.SpecifyKind(day.AddDays(1), DateTimeKind.Utc)).ToUnixTimeSeconds();
                            int page = 1;
                            bool more;
                            do
                            {
                                string url = "https://api.5dollarfootballapi.com/v1/fixtures?start_time=" + startTime +
                                    "&end_time=" + endTime + "&include=stats,events&per_page=50&page=" + page;
                                if (!string.IsNullOrWhiteSpace(leagueValue) && !leagueValue.Equals("all", StringComparison.OrdinalIgnoreCase))
                                    url += "&league=" + Uri.EscapeDataString(leagueValue);
                                var request = (HttpWebRequest)WebRequest.Create(url);
                                request.Method = "GET"; request.Accept = "application/json"; request.Timeout = 20000; request.ReadWriteTimeout = 20000;
                                request.Headers[HttpRequestHeader.Authorization] = "Bearer " + config.Key;
                                var root = Parse(ReadResponse(request));
                                if (HasErrors(root)) throw new InvalidOperationException("5DollarFootballAPI returned an API/quota error.");
                                output.AddRange(ParseFiveDollarFixtures(root, leagueValue));
                                var pagination = GetMap(root, "pagination");
                                more = GetString(pagination, "has_more", "false").Equals("true", StringComparison.OrdinalIgnoreCase);
                                page++;
                            } while (more && page <= 100);
                        }
                    }
                    return output;
                }
                catch (Exception ex) { last = ex; }
            }
            if (output.Count > 0) return output;
            throw new InvalidOperationException("5DollarFootballAPI fixture request failed. Check the bearer key, league ID, plan history limit and quota.", last);
        }

        private static List<FixtureRecord> ParseFiveDollarFixtures(Dictionary<string, object> root, string leagueValue)
        {
            var output = new List<FixtureRecord>();
            foreach (var item in GetList(root, "data"))
            {
                var leagueMap = GetMap(item, "league");
                var teams = GetMap(item, "teams");
                var goals = GetMap(item, "goals");
                var corners = GetMap(item, "corners");
                var cards = GetMap(item, "cards");
                var homeCards = GetMap(cards, "home");
                var awayCards = GetMap(cards, "away");
                var stats = GetMap(item, "stats");
                if (stats.Count == 0) stats = GetMap(item, "statistics");
                var events = GetList(item, "events");
                int varCount = events.Count(e =>
                {
                    string type = GetString(e, "type", "").ToLowerInvariant();
                    return type.Contains("var") || type.Contains("video_assistant");
                });
                output.Add(new FixtureRecord
                {
                    Provider = "FiveDollarFootballAPI",
                    League = GetString(leagueMap, "name", leagueValue.Length == 0 ? "Unknown competition" : leagueValue),
                    KickoffUtc = GetString(item, "kickoff_utc", GetString(item, "date", "")),
                    HomeTeam = GetString(GetMap(teams, "home"), "name", GetString(item, "home_team", "Home")),
                    AwayTeam = GetString(GetMap(teams, "away"), "name", GetString(item, "away_team", "Away")),
                    HomeGoals = GetNullableInt(goals, "home"),
                    AwayGoals = GetNullableInt(goals, "away"),
                    HomeCorners = GetNullableInt(corners, "home"),
                    AwayCorners = GetNullableInt(corners, "away"),
                    HomeYellowCards = GetNullableInt(homeCards, "yellow"),
                    AwayYellowCards = GetNullableInt(awayCards, "yellow"),
                    HomeShotsOnTarget = GetNullableInt(GetMap(stats, "shots_on_target"), "home"),
                    AwayShotsOnTarget = GetNullableInt(GetMap(stats, "shots_on_target"), "away"),
                    Referee = GetString(item, "referee", GetString(item, "referee_name", "")),
                    VarReviews = events.Count == 0 ? (int?)null : varCount,
                    Status = GetString(item, "status", ""),
                    ExternalId = GetString(item, "id", "")
                });
            }
            return output;
        }

        private static List<FixtureRecord> FetchFootballCharts(string league, int season)
        {
            string slug = (league ?? "").Trim();
            if (string.IsNullOrWhiteSpace(slug) || slug.Equals("all", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Football-Charts needs a league slug, e.g. 'premier' or 'germany3'.");
            string seasonValue = season + "-" + (season + 1);
            var configs = ApiProviderStore.Load().Where(p => p.Enabled && p.Provider == "FootballCharts").ToList();
            string key = configs.Select(p => p.Key).FirstOrDefault(k => !string.IsNullOrWhiteSpace(k));
            var output = new List<FixtureRecord>();
            Exception last = null;
            foreach (string kind in new[] { "results", "fixtures" })
            {
                try
                {
                    string url = "https://footballcharts-backend.onrender.com/api/v1/leagues/" + Uri.EscapeDataString(slug) + "/" + kind + "/";
                    if (kind == "results") url += "?season=" + Uri.EscapeDataString(seasonValue);
                    var request = (HttpWebRequest)WebRequest.Create(url);
                    request.Method = "GET"; request.Accept = "application/json"; request.Timeout = 25000; request.ReadWriteTimeout = 25000;
                    if (!string.IsNullOrWhiteSpace(key)) request.Headers[HttpRequestHeader.Authorization] = "Bearer " + key;
                    var parsed = new JavaScriptSerializer().DeserializeObject(ReadResponse(request));
                    var root = parsed as Dictionary<string, object>;
                    var rows = new List<object>();
                    if (root != null)
                    {
                        rows = GetList(root, "matches");
                        if (rows.Count == 0) rows = GetList(root, "results");
                        if (rows.Count == 0) rows = GetList(root, "fixtures");
                        if (rows.Count == 0) rows = GetList(root, "data");
                        if (rows.Count == 0) rows = GetList(root, "response");
                    }
                    else
                    {
                        var topLevel = parsed as System.Collections.ArrayList;
                        if (topLevel != null) rows = topLevel.Cast<object>().ToList();
                    }
                    foreach (var item in rows)
                    {
                        string home = GetString(item, "home_team", GetString(item, "homeTeam", GetString(item, "home_team_name", GetString(item, "home", ""))));
                        string away = GetString(item, "away_team", GetString(item, "awayTeam", GetString(item, "away_team_name", GetString(item, "away", ""))));
                        if (home.IndexOf("Dictionary", StringComparison.OrdinalIgnoreCase) >= 0)
                            home = GetString(GetMap(item, "home_team"), "name", GetString(GetMap(item, "homeTeam"), "name", GetString(GetMap(item, "home"), "name", "")));
                        if (away.IndexOf("Dictionary", StringComparison.OrdinalIgnoreCase) >= 0)
                            away = GetString(GetMap(item, "away_team"), "name", GetString(GetMap(item, "awayTeam"), "name", GetString(GetMap(item, "away"), "name", "")));
                        if (home.Length == 0) home = GetString(GetMap(item, "homeTeam"), "name", "");
                        if (away.Length == 0) away = GetString(GetMap(item, "awayTeam"), "name", "");
                        if (home.Length == 0 || away.Length == 0) continue;

                        var score = GetMap(item, "score");
                        int? hg = GetNullableInt(item, "homeGoals") ?? GetNullableInt(item, "home_goals") ?? GetNullableInt(item, "home_score") ?? GetNullableInt(score, "home");
                        int? ag = GetNullableInt(item, "awayGoals") ?? GetNullableInt(item, "away_goals") ?? GetNullableInt(item, "away_score") ?? GetNullableInt(score, "away");
                        string scoreText = GetString(item, "score", GetString(item, "ft_result", GetString(item, "result", "")));
                        if ((!hg.HasValue || !ag.HasValue) && scoreText.Contains(":"))
                        {
                            var parts = scoreText.Split(':'); int h, a;
                            if (parts.Length == 2 && int.TryParse(parts[0].Trim(), out h) && int.TryParse(parts[1].Trim(), out a)) { hg = h; ag = a; }
                        }
                        string leagueName = GetString(item, "league", "");
                        if (leagueName.IndexOf("Dictionary", StringComparison.OrdinalIgnoreCase) >= 0 || leagueName.Length == 0)
                            leagueName = GetString(GetMap(item, "league"), "name", GetString(item, "competition", slug));
                        output.Add(new FixtureRecord
                        {
                            Provider = "FootballCharts",
                            League = leagueName,
                            KickoffUtc = GetString(item, "kickoff_utc", GetString(item, "date", GetString(item, "kickoff", GetString(item, "utcDate", GetString(item, "match_date", GetString(item, "datetime", "")))))),
                            HomeTeam = home, AwayTeam = away, HomeGoals = hg, AwayGoals = ag,
                            Status = GetString(item, "status", kind == "results" ? "FINISHED" : "SCHEDULED"),
                            ExternalId = GetString(item, "slug", GetString(item, "id", GetString(item, "footballcharts_id", "")))
                        });
                    }
                }
                catch (Exception ex) { last = ex; }
            }
            if (output.Count > 0) return output;
            throw new InvalidOperationException("Football-Charts returned no compatible match rows. Check the league slug and season; this source provides results and its own model probabilities, not corner/card/shot statistics.", last);
        }

        private static string ReadResponse(HttpWebRequest request)
        {
            using (var response = (HttpWebResponse)request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8))
                return reader.ReadToEnd();
        }

        private static List<FixtureRecord> FetchOddsPapi(string tournamentId)
        {
            var configs = ApiProviderStore.Load().Where(p => p.Enabled && p.Provider == "OddsPapi" && !string.IsNullOrWhiteSpace(p.Key)).ToList();
            if (configs.Count == 0) throw new InvalidOperationException("Add an enabled OddsPapi API key in API Settings.");
            Exception last = null;
            foreach (var config in configs)
            {
                try
                {
                    string url = "https://api.oddspapi.io/v4/fixtures?tournamentId=" + Uri.EscapeDataString(tournamentId) +
                        "&language=" + (LocalizationService.IsPersian ? "fa" : "en") + "&apiKey=" + Uri.EscapeDataString(config.Key);
                    object root = new JavaScriptSerializer().DeserializeObject(Request(url, null, null));
                    var rows = root as System.Collections.ArrayList;
                    if (rows == null && root is object[]) rows = new System.Collections.ArrayList((object[])root);
                    if (rows == null) throw new InvalidOperationException("OddsPapi returned an unexpected fixtures response.");
                    var output = new List<FixtureRecord>();
                    foreach (var item in rows)
                    {
                        output.Add(new FixtureRecord {
                            Provider = "OddsPapi",
                            League = GetString(item, "tournamentName", tournamentId),
                            KickoffUtc = GetString(item, "startTime", ""),
                            HomeTeam = GetString(item, "participant1Name", "Home"),
                            AwayTeam = GetString(item, "participant2Name", "Away"),
                            Status = GetString(item, "statusName", ""),
                            ExternalId = GetString(item, "fixtureId", "")
                        });
                    }
                    return output;
                }
                catch (Exception ex) { last = ex; }
            }
            throw new InvalidOperationException("OddsPapi fixture request failed. Check tournament ID, API key and quota.", last);
        }

        private static List<FixtureRecord> FetchSportsApi360(string tournamentId)
        {
            var configs = ApiProviderStore.Load().Where(p => p.Enabled && p.Provider == "SportsAPI360" && !string.IsNullOrWhiteSpace(p.Key)).ToList();
            if (configs.Count == 0) throw new InvalidOperationException("Add an enabled SportsAPI360 API key in API Settings.");
            var output = new List<FixtureRecord>();
            Exception last = null;
            foreach (var config in configs)
            {
                try
                {
                    for (int day = 0; day < 7; day++)
                    {
                        string date = DateTime.UtcNow.Date.AddDays(day).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                        string url = "https://apiv3.sportsapi360.com/football/api/v1/matches/schedule?date=" + date;
                        object parsed = new JavaScriptSerializer().DeserializeObject(Request(url, "x-app-key", config.Key));
                        var root = parsed as Dictionary<string, object>;
                        if (root == null) continue;
                        foreach (var tournament in GetList(root, "response"))
                        {
                            string tid = GetString(tournament, "tournamentId", "");
                            string leagueName = GetString(GetMap(tournament, "tournament"), "name", tournamentId);
                            if (!string.IsNullOrWhiteSpace(tournamentId) && !tournamentId.Equals("all", StringComparison.OrdinalIgnoreCase) &&
                                !tournamentId.Equals(tid, StringComparison.OrdinalIgnoreCase) &&
                                !tournamentId.Equals(leagueName, StringComparison.OrdinalIgnoreCase)) continue;
                            foreach (var match in GetList(tournament, "matches"))
                            {
                                long stamp;
                                string kickoff = long.TryParse(GetString(match, "startTimestamp", ""), out stamp)
                                    ? DateTimeOffset.FromUnixTimeSeconds(stamp).UtcDateTime.ToString("o", CultureInfo.InvariantCulture) : "";
                                output.Add(new FixtureRecord {
                                    Provider = "SportsAPI360",
                                    League = GetString(GetMap(match, "tournament"), "name", leagueName),
                                    KickoffUtc = kickoff,
                                    HomeTeam = GetString(GetMap(match, "homeTeam"), "name", "Home"),
                                    AwayTeam = GetString(GetMap(match, "awayTeam"), "name", "Away"),
                                    HomeGoals = GetNullableInt(GetMap(match, "homeScore"), "current"),
                                    AwayGoals = GetNullableInt(GetMap(match, "awayScore"), "current"),
                                    Status = GetString(GetMap(match, "status"), "type", ""),
                                    ExternalId = GetString(match, "id", "")
                                });
                            }
                        }
                    }
                    return output;
                }
                catch (Exception ex) { last = ex; }
            }
            if (output.Count > 0) return output;
            throw new InvalidOperationException("SportsAPI360 schedule request failed. Check API key and tournament ID (or use 'all').", last);
        }

        // Generic endpoint adapter. Supports common REST envelopes and camel/snake-case fixture schemas.
        private static List<FixtureRecord> FetchCustom(ApiProviderConfig config)
        {
            if (string.IsNullOrWhiteSpace(config.BaseUrl))
                throw new InvalidOperationException(config.Provider + " requires its documented HTTPS fixtures/events endpoint in API Settings.");
            if (config.Provider == "Betfair" && config.BaseUrl.IndexOf("betfair.com", StringComparison.OrdinalIgnoreCase) >= 0)
                throw new InvalidOperationException("Betfair's native API uses session authentication and JSON-RPC POST. Configure an HTTPS proxy on your own endpoint that exposes a GET fixtures feed, or use a dedicated Betfair session adapter.");
            string endpoint = config.BaseUrl;
            string header = string.IsNullOrWhiteSpace(config.HeaderName) ? "x-api-key" : config.HeaderName;
            if (config.Provider == "Sportradar" && !string.IsNullOrWhiteSpace(config.Key) &&
                endpoint.IndexOf("api_key=", StringComparison.OrdinalIgnoreCase) < 0)
                endpoint += (endpoint.Contains("?") ? "&" : "?") + "api_key=" + Uri.EscapeDataString(config.Key);
            string json = Request(endpoint, config.Provider == "Sportradar" ? null : header, config.Provider == "Sportradar" ? null : config.Key);
            object parsed = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue }.DeserializeObject(json);
            var rootMap = parsed as Dictionary<string, object>;
            object rowsObject = parsed;
            if (rootMap != null)
            {
                string[] envelopeKeys = { "response", "matches", "fixtures", "events", "data", "result", "results", "items" };
                rowsObject = null;
                foreach (string key in envelopeKeys)
                    if (rootMap.ContainsKey(key) && rootMap[key] != null) { rowsObject = rootMap[key]; break; }
                if (rowsObject == null) rowsObject = parsed;
            }
            var rows = ToRows(rowsObject);
            if (rows.Count == 0)
                throw new InvalidOperationException(config.Provider + " response did not contain a fixture/event array. Check the endpoint path, API plan and JSON envelope; an API error object is not a fixtures response.");
            var output = new List<FixtureRecord>();
            foreach (var item in rows)
            {
                var teams = GetMap(item, "teams");
                var homeMap = GetMap(item, "home");
                var awayMap = GetMap(item, "away");
                if (homeMap.Count == 0) homeMap = GetMap(teams, "home");
                if (awayMap.Count == 0) awayMap = GetMap(teams, "away");
                string home = FirstString(item, "homeTeam", "home_team", "strHomeTeam", "event_home_team");
                string away = FirstString(item, "awayTeam", "away_team", "strAwayTeam", "event_away_team");
                if (home.IndexOf("Dictionary", StringComparison.OrdinalIgnoreCase) >= 0)
                    home = FirstString(GetMap(item, "homeTeam"), "name", "teamName", "displayName");
                if (away.IndexOf("Dictionary", StringComparison.OrdinalIgnoreCase) >= 0)
                    away = FirstString(GetMap(item, "awayTeam"), "name", "teamName", "displayName");
                if (home.Length == 0) home = FirstString(homeMap, "name", "teamName", "displayName");
                if (away.Length == 0) away = FirstString(awayMap, "name", "teamName", "displayName");
                if (home.Length == 0 || away.Length == 0) continue;
                string kickoff = FirstString(item, "date", "kickoff", "utcDate", "commence_time", "startTime", "matchDateTimeUTC", "event_date", "kickoff_utc", "dateEvent", "strTimestamp");
                string leagueName = FirstString(item, "league", "competition", "league_name", "strLeague", "tournamentName");
                output.Add(new FixtureRecord {
                    Provider = config.Provider,
                    League = leagueName.Length == 0 ? config.Provider : leagueName,
                    KickoffUtc = kickoff,
                    HomeTeam = home, AwayTeam = away,
                    HomeGoals = FirstNullableInt(item, "homeGoals", "home_goals", "intHomeScore"),
                    AwayGoals = FirstNullableInt(item, "awayGoals", "away_goals", "intAwayScore"),
                    Status = FirstString(item, "status", "event_status", "strStatus"),
                    ExternalId = FirstString(item, "id", "fixtureId", "event_key", "idEvent")
                });
            }
            if (output.Count == 0)
                throw new InvalidOperationException(config.Provider + " returned " + rows.Count + " rows but none had recognizable home/away team names. The response schema or endpoint may not be a fixture feed.");
            return output;
        }

        private static List<object> ToRows(object value)
        {
            var list = value as System.Collections.ArrayList;
            if (list != null) return list.Cast<object>().ToList();
            var array = value as object[];
            if (array != null) return array.ToList();
            var map = value as Dictionary<string, object>;
            if (map != null) return map.Values.Where(v => v != null).ToList();
            return new List<object>();
        }

        private static string FirstString(object source, params string[] keys)
        {
            foreach (string key in keys)
            {
                string value = GetString(source, key, "");
                if (value.Length > 0 && !value.Equals("null", StringComparison.OrdinalIgnoreCase)) return value;
            }
            return "";
        }

        private static int? FirstNullableInt(object source, params string[] keys)
        {
            foreach (string key in keys)
            {
                int? value = GetNullableInt(source, key);
                if (value.HasValue) return value;
            }
            return null;
        }

        private static List<FixtureRecord> FetchTheOddsApi(string sportKey)
        {
            var configs = ApiProviderStore.Load().Where(p => p.Enabled && p.Provider == "TheOddsAPI" && !string.IsNullOrWhiteSpace(p.Key)).ToList();
            if (configs.Count == 0) throw new InvalidOperationException("Add an enabled The Odds API key in API Settings.");
            string selectedSport = string.IsNullOrWhiteSpace(sportKey) || sportKey.Equals("all", StringComparison.OrdinalIgnoreCase)
                ? "soccer_epl" : sportKey.Trim();
            Exception last = null;
            foreach (var config in configs)
            {
                try
                {
                    string url = "https://api.the-odds-api.com/v4/sports/" + Uri.EscapeDataString(selectedSport) +
                        "/events?apiKey=" + Uri.EscapeDataString(config.Key);
                    object parsed = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue }.DeserializeObject(Request(url, null, null));
                    var error = parsed as Dictionary<string, object>;
                    if (error != null)
                    {
                        string message = FirstString(error, "message", "error", "detail");
                        throw new InvalidOperationException("The Odds API returned an error object" + (message.Length == 0 ? "." : ": " + message));
                    }
                    var rows = ToRows(parsed);
                    if (rows.Count == 0)
                        throw new InvalidOperationException("The Odds API returned no events for sport key '" + selectedSport + "'.");
                    var output = new List<FixtureRecord>();
                    foreach (var item in rows)
                    {
                        string home = FirstString(item, "home_team");
                        string away = FirstString(item, "away_team");
                        if (home.Length == 0 || away.Length == 0) continue;
                        output.Add(new FixtureRecord {
                            Provider = "TheOddsAPI", League = selectedSport,
                            KickoffUtc = FirstString(item, "commence_time"),
                            HomeTeam = home, AwayTeam = away,
                            Status = "scheduled", ExternalId = FirstString(item, "id")
                        });
                    }
                    if (output.Count == 0) throw new InvalidOperationException("The Odds API response did not contain home_team/away_team fields.");
                    return output;
                }
                catch (Exception ex) { last = ex; }
            }
            throw new InvalidOperationException("The Odds API event request failed. Use a valid sport key such as soccer_epl; this adapter imports events, not bookmaker odds.", last);
        }

        private static List<FixtureRecord> FetchTheSportsDb(string leagueId, int season)
        {
            string id = string.IsNullOrWhiteSpace(leagueId) || leagueId.Equals("all", StringComparison.OrdinalIgnoreCase) ? "4328" : leagueId.Trim();
            string seasonName = season.ToString(CultureInfo.InvariantCulture) + "-" + (season + 1).ToString(CultureInfo.InvariantCulture);
            string url = "https://www.thesportsdb.com/api/v1/json/123/eventsseason.php?id=" + Uri.EscapeDataString(id) +
                "&s=" + Uri.EscapeDataString(seasonName);
            object parsed = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue }.DeserializeObject(Request(url, null, null));
            var root = parsed as Dictionary<string, object>;
            var rows = root == null ? new List<object>() : GetList(root, "events");
            if (rows.Count == 0) throw new InvalidOperationException("TheSportsDB returned no events. Use a valid league ID; the default is English Premier League (4328).");
            var output = new List<FixtureRecord>();
            foreach (var item in rows)
            {
                string home = FirstString(item, "strHomeTeam"), away = FirstString(item, "strAwayTeam");
                if (home.Length == 0 || away.Length == 0) continue;
                output.Add(new FixtureRecord {
                    Provider = "TheSportsDB", League = FirstString(item, "strLeague"),
                    KickoffUtc = FirstString(item, "strTimestamp", "dateEvent", "dateEventLocal"),
                    HomeTeam = home, AwayTeam = away,
                    HomeGoals = FirstNullableInt(item, "intHomeScore"),
                    AwayGoals = FirstNullableInt(item, "intAwayScore"),
                    Status = FirstString(item, "strStatus"), ExternalId = FirstString(item, "idEvent")
                });
            }
            return output;
        }

        private static List<FixtureRecord> FetchAllSportsApi(string league, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var configs = ApiProviderStore.Load().Where(p => p.Enabled && p.Provider == "AllSportsApi" && !string.IsNullOrWhiteSpace(p.Key)).ToList();
            if (configs.Count == 0) throw new InvalidOperationException("Add an enabled AllSportsApi key in API Settings.");
            DateTime from = (fromDate ?? DateTime.UtcNow.Date.AddDays(-7)).Date;
            DateTime to = (toDate ?? DateTime.UtcNow.Date.AddDays(7)).Date;
            Exception last = null;
            foreach (var config in configs)
            {
                try
                {
                    string url = "https://apiv2.allsportsapi.com/football/?met=Fixtures&APIkey=" + Uri.EscapeDataString(config.Key) +
                        "&from=" + from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) +
                        "&to=" + to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "&timezone=UTC";
                    object parsed = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue }.DeserializeObject(Request(url, null, null));
                    var root = parsed as Dictionary<string, object>;
                    if (root == null || GetString(root, "success", "") != "1")
                        throw new InvalidOperationException("AllSportsApi returned an error or a response without success=1.");
                    var rows = root.ContainsKey("result") ? ToRows(root["result"]) : new List<object>();
                    var output = new List<FixtureRecord>();
                    foreach (var item in rows)
                    {
                        string home = FirstString(item, "event_home_team");
                        string away = FirstString(item, "event_away_team");
                        if (home.Length == 0 || away.Length == 0) continue;
                        string score = FirstString(item, "event_final_result");
                        int? hg = null, ag = null;
                        if (score.Contains("-"))
                        {
                            string[] parts = score.Split('-');
                            int h, a;
                            if (parts.Length == 2 && int.TryParse(parts[0].Trim(), out h) && int.TryParse(parts[1].Trim(), out a)) { hg = h; ag = a; }
                        }
                        output.Add(new FixtureRecord {
                            Provider = "AllSportsApi", League = FirstString(item, "league_name"),
                            KickoffUtc = FirstString(item, "event_date") + (FirstString(item, "event_time").Length > 0 ? "T" + FirstString(item, "event_time") + "Z" : ""),
                            HomeTeam = home, AwayTeam = away, HomeGoals = hg, AwayGoals = ag,
                            Status = FirstString(item, "event_status"), ExternalId = FirstString(item, "event_key")
                        });
                    }
                    if (output.Count == 0) throw new InvalidOperationException("AllSportsApi returned no recognizable fixtures in the selected date window.");
                    return output;
                }
                catch (Exception ex) { last = ex; }
            }
            throw new InvalidOperationException("AllSportsApi fixture request failed. Check key, free-plan coverage and quota.", last);
        }

        private static List<FixtureRecord> FetchScoreBat()
        {
            var configs = ApiProviderStore.Load().Where(p => p.Enabled && p.Provider == "ScoreBat" && !string.IsNullOrWhiteSpace(p.Key)).ToList();
            if (configs.Count == 0) throw new InvalidOperationException("Add a ScoreBat API token in API Settings.");
            Exception last = null;
            foreach (var config in configs)
            {
                try
                {
                    string url = "https://www.scorebat.com/video-api/v3/free-feed/?token=" + Uri.EscapeDataString(config.Key);
                    object parsed = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue }.DeserializeObject(Request(url, null, null));
                    var root = parsed as Dictionary<string, object>;
                    var rows = root == null ? new List<object>() : GetList(root, "response");
                    var output = new List<FixtureRecord>();
                    foreach (var item in rows)
                    {
                        string title = FirstString(item, "title");
                        string home = FirstString(GetMap(item, "homeTeam"), "name");
                        string away = FirstString(GetMap(item, "awayTeam"), "name");
                        if (home.Length == 0 || away.Length == 0)
                        {
                            string[] delimiters = { " vs ", " v ", " - " };
                            foreach (string delimiter in delimiters)
                            {
                                int split = title.IndexOf(delimiter, StringComparison.OrdinalIgnoreCase);
                                if (split <= 0) continue;
                                home = title.Substring(0, split).Trim();
                                away = title.Substring(split + delimiter.Length).Trim();
                                break;
                            }
                        }
                        if (home.Length == 0 || away.Length == 0) continue;
                        output.Add(new FixtureRecord {
                            Provider = "ScoreBat", League = FirstString(item, "competition"),
                            KickoffUtc = FirstString(item, "date"),
                            HomeTeam = home, AwayTeam = away, Status = "video available",
                            ExternalId = FirstString(item, "matchviewUrl")
                        });
                    }
                    if (output.Count > 0) return output;
                    throw new InvalidOperationException("ScoreBat returned no titles that could be parsed as 'Home vs Away'.");
                }
                catch (Exception ex) { last = ex; }
            }
            throw new InvalidOperationException("ScoreBat request failed. Check API token and free-plan access.", last);
        }

        private static List<FixtureRecord> FetchApiFootball(string league, int season)
        {
            string path = "/fixtures?league=" + Uri.EscapeDataString(league) + "&season=" + season;
            var configs = ApiProviderStore.Load().Where(p => p.Enabled && (p.Provider == "ApiFootball" || p.Provider == "ApiSports") && !string.IsNullOrWhiteSpace(p.Key)).ToList();
            if (configs.Count == 0) throw new InvalidOperationException("No enabled API-Football key is saved. Open API Settings.");
            Exception last = null;
            foreach (var config in configs)
            {
                try
                {
                    var root = Parse(Request("https://v3.football.api-sports.io" + path, "x-apisports-key", config.Key));
                    if (HasErrors(root)) { last = new InvalidOperationException("API-Football returned an API or quota error."); continue; }
                    var rows = ParseApiFootballFixtures(root);
                    if (rows.Count > 0) return rows;
                    last = new InvalidOperationException("No fixtures were returned for this league and season.");
                }
                catch (WebException ex)
                {
                    int status = ex.Response is HttpWebResponse ? (int)((HttpWebResponse)ex.Response).StatusCode : 0;
                    if (status == 401 || status == 403 || status == 429) { last = ex; continue; }
                    throw new InvalidOperationException("API-Football connection failed (HTTP " + status + ").", ex);
                }
            }
            throw new InvalidOperationException("All enabled API-Football keys failed or returned no fixtures. Check key validity, league ID and quota.", last);
        }

        private static List<FixtureRecord> FetchFootballDataBulk(int season, DateTime startDate)
        {
            var configs = ApiProviderStore.Load().Where(p => p.Enabled && p.Provider == "FootballData" && !string.IsNullOrWhiteSpace(p.Key)).ToList();
            if (configs.Count == 0) return new List<FixtureRecord>();
            string from = startDate.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            string to = DateTime.UtcNow.Date.AddDays(7).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            Exception last = null;
            foreach (var config in configs)
            {
                try
                {
                    string url = "https://api.football-data.org/v4/matches?dateFrom=" + from + "&dateTo=" + to;
                    var root = Parse(Request(url, "X-Auth-Token", config.Key));
                    if (HasErrors(root)) throw new InvalidOperationException("The account may not have access to all requested competitions.");
                    var output = new List<FixtureRecord>();
                    foreach (var item in GetList(root, "matches"))
                    {
                        var home = GetMap(item, "homeTeam");
                        var away = GetMap(item, "awayTeam");
                        var competition = GetMap(item, "competition");
                        var score = GetMap(item, "score");
                        var fullTime = GetMap(score, "fullTime");
                        output.Add(new FixtureRecord {
                            Provider = "football-data.org",
                            League = GetString(competition, "name", GetString(competition, "code", "Competition")),
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
                catch (Exception ex) { last = ex; }
            }
            throw new InvalidOperationException("Could not retrieve matches available to this football-data.org token for the requested date range. Check the token, plan coverage, and request quota.", last);
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
            if (!string.IsNullOrEmpty(header) && header.Equals("X-RapidAPI-Key", StringComparison.OrdinalIgnoreCase))
            {
                Uri uri;
                if (Uri.TryCreate(url, UriKind.Absolute, out uri) && uri.Host.EndsWith(".p.rapidapi.com", StringComparison.OrdinalIgnoreCase))
                    request.Headers["X-RapidAPI-Host"] = uri.Host;
            }
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
        private readonly TextBox _customNameBox;
        private readonly TextBox _baseUrlBox;
        private readonly TextBox _headerBox;
        private readonly ListBox _providersList;
        private readonly TextBlock _status;
        private StackPanel _competitionChecks;
        private TextBox _competitionSearchBox;
        private TextBlock _competitionStatus;
        private List<ApiProviderConfig> _providers;

        public ApiSettingsWindow()
        {
            Title = LocalizationService.T("KoaZa — Settings", "KoaZa — تنظیمات");
            Width = 1040; Height = 650; MinWidth = 850; MinHeight = 500;
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
            entry.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
            entry.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) });
            entry.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
            entry.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.7, GridUnitType.Star) });
            entry.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.0, GridUnitType.Star) });
            entry.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _providerBox = new ComboBox { Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(6), VerticalContentAlignment = VerticalAlignment.Center };
            _providerBox.Items.Add(new ProviderChoice("OddsPapi", "OddsPapi"));
            _providerBox.Items.Add(new ProviderChoice("Floodlight", "Floodlight"));
            _providerBox.Items.Add(new ProviderChoice("SportsOpenData", "Sports Open Data"));
            _providerBox.Items.Add(new ProviderChoice("OpenLigaDB", "OpenLigaDB (no key)"));
            _providerBox.Items.Add(new ProviderChoice("ApiSports", "API-SPORTS"));
            _providerBox.Items.Add(new ProviderChoice("SportsAPI360", "SportsAPI360"));
            _providerBox.Items.Add(new ProviderChoice("FiveDollarFootballAPI", "5DollarFootballAPI"));
            _providerBox.Items.Add(new ProviderChoice("FootballCharts", "Football-Charts"));
            _providerBox.Items.Add(new ProviderChoice("FootballData", "Football-Data (football-data.org)"));
            _providerBox.Items.Add(new ProviderChoice("Sportmonks", "Sportmonks"));
            _providerBox.Items.Add(new ProviderChoice("TheOddsAPI", "The Odds API"));
            _providerBox.Items.Add(new ProviderChoice("TheRundown", "TheRundown.io (custom endpoint)"));
            _providerBox.Items.Add(new ProviderChoice("TheSportsDB", "TheSportsDB"));
            _providerBox.Items.Add(new ProviderChoice("AllSportsApi", "AllSportsApi"));
            _providerBox.Items.Add(new ProviderChoice("FootballBettingOdds", "Football Betting Odds (RapidAPI/custom endpoint)"));
            _providerBox.Items.Add(new ProviderChoice("Betfair", "Betfair API (custom endpoint/session required)"));
            _providerBox.Items.Add(new ProviderChoice("Sportradar", "Sportradar (custom endpoint)"));
            _providerBox.Items.Add(new ProviderChoice("SportsDataIO", "SportsDataIO (custom endpoint)"));
            _providerBox.Items.Add(new ProviderChoice("ScoreBat", "ScoreBat Video API"));
            _providerBox.Items.Add(new ProviderChoice("BallDontLie", "BALLDONTLIE (custom endpoint)"));
            _providerBox.Items.Add(new ProviderChoice("PremierLeagueStats", "Premier League Stats (RapidAPI/custom endpoint)"));
            _providerBox.Items.Add(new ProviderChoice("Custom", LocalizationService.T("Add provider manually", "افزودن دستی")));
            _providerBox.SelectedIndex = 0;
            _providerBox.SelectionChanged += ProviderBox_SelectionChanged;
            Grid.SetColumn(_providerBox, 0); entry.Children.Add(_providerBox);
            _keyBox = new TextBox { Text = "API key / token", Padding = new Thickness(8), Margin = new Thickness(0, 0, 8, 0), VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "API key / token" };
            _keyBox.GotKeyboardFocus += (s, e) => { if (_keyBox.Text == "API key / token") _keyBox.Text = ""; };
            _keyBox.LostKeyboardFocus += (s, e) => { if (string.IsNullOrWhiteSpace(_keyBox.Text)) _keyBox.Text = "API key / token"; };
            Grid.SetColumn(_keyBox, 1); entry.Children.Add(_keyBox);
            _customNameBox = new TextBox { Text = "Custom name", Padding = new Thickness(8), Margin = new Thickness(0, 0, 8, 0), VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "Custom provider name" };
            _customNameBox.GotKeyboardFocus += (s, e) => { if (_customNameBox.Text == "Custom name") _customNameBox.Text = ""; };
            _customNameBox.LostKeyboardFocus += (s, e) => { if (string.IsNullOrWhiteSpace(_customNameBox.Text)) _customNameBox.Text = "Custom name"; };
            Grid.SetColumn(_customNameBox, 2); entry.Children.Add(_customNameBox);
            _baseUrlBox = new TextBox { Text = "HTTPS endpoint", Padding = new Thickness(8), Margin = new Thickness(0, 0, 8, 0), VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "Custom HTTPS endpoint" };
            _baseUrlBox.GotKeyboardFocus += (s, e) => { if (_baseUrlBox.Text == "HTTPS endpoint") _baseUrlBox.Text = ""; };
            _baseUrlBox.LostKeyboardFocus += (s, e) => { if (string.IsNullOrWhiteSpace(_baseUrlBox.Text)) _baseUrlBox.Text = "HTTPS endpoint"; };
            Grid.SetColumn(_baseUrlBox, 3); entry.Children.Add(_baseUrlBox);
            _headerBox = new TextBox { Text = "x-api-key", Padding = new Thickness(8), Margin = new Thickness(0, 0, 8, 0), VerticalContentAlignment = VerticalAlignment.Center, ToolTip = "Authentication header (custom providers)" };
            _headerBox.GotKeyboardFocus += (s, e) => { if (_headerBox.Text == "x-api-key") _headerBox.SelectAll(); };
            Grid.SetColumn(_headerBox, 4); entry.Children.Add(_headerBox);
            var add = new Button { Content = LocalizationService.T("Add", "افزودن"), Padding = new Thickness(12, 7, 12, 7), Background = new SolidColorBrush(Color.FromRgb(22, 134, 91)), Foreground = Brushes.White, BorderThickness = new Thickness(0) };
            add.Click += Add_Click; Grid.SetColumn(add, 5); entry.Children.Add(add);
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
            var tabs = new TabControl();
            tabs.Items.Add(new TabItem { Header = LocalizationService.T("API providers", "سرویس‌های API"), Content = root });
            tabs.Items.Add(new TabItem { Header = LocalizationService.T("Competitions to display", "رقابت‌های قابل نمایش"), Content = BuildCompetitionTab() });
            tabs.Items.Add(new TabItem { Header = LocalizationService.T("General settings", "تنظیمات عمومی"), Content = BuildGeneralTab() });
            Content = tabs;
            ProviderBox_SelectionChanged(null, null);
            RefreshList();
            LocalizationService.Apply(this);
            ThemeService.Apply(this);
        }

        private UIElement BuildGeneralTab()
        {
            var panel = new StackPanel { Margin = new Thickness(24), Orientation = Orientation.Vertical };
            panel.Children.Add(new TextBlock { Text = LocalizationService.T("Appearance", "ظاهر برنامه"), FontSize = 20, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 8) });
            panel.Children.Add(new TextBlock { Text = LocalizationService.T("Choose how the application should look.", "حالت نمایش برنامه را انتخاب کن."), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) });
            var themeBox = new ComboBox { Width = 240, HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(8), Margin = new Thickness(0, 0, 0, 20) };
            themeBox.Items.Add(new ComboBoxItem { Content = LocalizationService.T("Follow Windows", "مطابق ویندوز"), Tag = "System" });
            themeBox.Items.Add(new ComboBoxItem { Content = LocalizationService.T("Light", "روشن"), Tag = "Light" });
            themeBox.Items.Add(new ComboBoxItem { Content = LocalizationService.T("Dark", "تاریک"), Tag = "Dark" });
            themeBox.SelectedIndex = ThemeService.Mode == "Light" ? 1 : ThemeService.Mode == "Dark" ? 2 : 0;
            themeBox.SelectionChanged += (s, e) =>
            {
                var choice = themeBox.SelectedItem as ComboBoxItem;
                if (choice == null) return;
                ThemeService.SetMode(Convert.ToString(choice.Tag));
                ThemeService.Apply(this);
            };
            panel.Children.Add(themeBox);
            panel.Children.Add(new Separator { Margin = new Thickness(0, 4, 0, 14) });
            panel.Children.Add(new TextBlock { Text = LocalizationService.T("Database safety", "امنیت پایگاه داده"), FontSize = 20, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 8) });
            panel.Children.Add(new TextBlock
            {
                Text = LocalizationService.T("Automatic backups run daily and before importing refreshed fixtures. The 30 newest backups are retained. Completed results replaced by a provider update are archived in the database history table.", "پشتیبان‌گیری خودکار روزانه و پیش از دریافت مجدد مسابقات انجام می‌شود. ۳۰ نسخهٔ جدید نگه‌داری می‌شود. نتایج تمام‌شده‌ای که سرویس داده به‌روزرسانی کند، در جدول آرشیو ذخیره می‌شوند."),
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12)
            });
            panel.Children.Add(new TextBlock { Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KoaZa", "Backups"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) });
            var backup = new Button { Content = LocalizationService.T("Create backup now", "تهیهٔ پشتیبان اکنون"), Padding = new Thickness(14, 8, 14, 8), HorizontalAlignment = HorizontalAlignment.Left, Background = new SolidColorBrush(Color.FromRgb(22, 134, 91)), Foreground = Brushes.White, BorderThickness = new Thickness(0) };
            var backupStatus = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 10, 0, 0),
                Text = string.IsNullOrWhiteSpace(DatabaseService.LastBackupError)
                    ? LocalizationService.T("Automatic backup status: ready.", "وضعیت پشتیبان‌گیری خودکار: آماده.")
                    : LocalizationService.T("Automatic backup warning: ", "هشدار پشتیبان‌گیری خودکار: ") + DatabaseService.LastBackupError
            };
            backup.Click += (s, e) =>
            {
                try
                {
                    string path = new DatabaseService().CreateBackupNow();
                    backupStatus.Text = LocalizationService.T("Backup created: ", "پشتیبان ساخته شد: ") + path;
                }
                catch (Exception ex) { backupStatus.Text = LocalizationService.T("Backup failed: ", "تهیه پشتیبان ناموفق بود: ") + ex.Message; }
            };
            panel.Children.Add(backup);
            panel.Children.Add(backupStatus);
            return new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }

        private UIElement BuildCompetitionTab()
        {
            var panel = new Grid { Margin = new Thickness(18) };
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var instruction = new TextBlock
            {
                Text = LocalizationService.T(
                    "This catalog is built from competitions actually received and saved by your enabled API providers. Tick a competition to include it in the main-page filters; untick it to hide it. Use Refresh catalog after importing new fixtures.",
                    "این فهرست از لیگ‌ها و جام‌هایی ساخته می‌شود که سرویس‌های API فعال واقعاً دریافت و در پایگاه داده ذخیره کرده‌اند. برای نمایش یک رقابت در فیلترهای صفحه اصلی تیک بزن و برای مخفی کردن آن تیک را بردار. پس از دریافت مسابقات جدید، فهرست را نوسازی کن."),
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12), Foreground = new SolidColorBrush(Color.FromRgb(65, 84, 73))
            };
            panel.Children.Add(instruction); Grid.SetRow(instruction, 0);

            var toolbar = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _competitionSearchBox = new TextBox
            {
                Padding = new Thickness(9, 7, 9, 7), Margin = new Thickness(0, 0, 10, 0),
                ToolTip = LocalizationService.T("Search league or cup name", "جست‌وجوی نام لیگ یا جام"),
                Text = ""
            };
            _competitionSearchBox.TextChanged += (sender, args) => FilterCompetitionChecks();
            Grid.SetColumn(_competitionSearchBox, 0); toolbar.Children.Add(_competitionSearchBox);
            var refresh = new Button
            {
                Content = LocalizationService.T("Refresh catalog", "نوسازی فهرست"),
                Padding = new Thickness(12, 7, 12, 7),
                Background = new SolidColorBrush(Color.FromRgb(22, 134, 91)), Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };
            refresh.Click += (sender, args) => RefreshCompetitionCatalogAsync();
            Grid.SetColumn(refresh, 1); toolbar.Children.Add(refresh);
            panel.Children.Add(toolbar); Grid.SetRow(toolbar, 1);

            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            _competitionChecks = new StackPanel { Margin = new Thickness(2) };
            scroll.Content = _competitionChecks;
            panel.Children.Add(scroll); Grid.SetRow(scroll, 2);

            var bottom = new DockPanel { Margin = new Thickness(0, 12, 0, 0), LastChildFill = true };
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };
            var all = new Button { Content = LocalizationService.T("Select all", "انتخاب همه"), Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 0, 8, 0) };
            all.Click += (sender, args) => SetCompetitionChecks(true);
            var none = new Button { Content = LocalizationService.T("Clear all", "پاک کردن انتخاب‌ها"), Padding = new Thickness(12, 7, 12, 7), Margin = new Thickness(0, 0, 8, 0) };
            none.Click += (sender, args) => SetCompetitionChecks(false);
            var save = new Button { Content = LocalizationService.T("Save selection", "ذخیره انتخاب‌ها"), Padding = new Thickness(14, 7, 14, 7), Background = new SolidColorBrush(Color.FromRgb(22, 134, 91)), Foreground = Brushes.White, BorderThickness = new Thickness(0) };
            save.Click += SaveCompetitionSelection_Click;
            buttons.Children.Add(all); buttons.Children.Add(none); buttons.Children.Add(save);
            DockPanel.SetDock(buttons, Dock.Left); bottom.Children.Add(buttons);
            _competitionStatus = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(14, 4, 0, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush(Color.FromRgb(65, 84, 73)) };
            bottom.Children.Add(_competitionStatus);
            panel.Children.Add(bottom); Grid.SetRow(bottom, 3);
            LoadCompetitionChecks();
            return panel;
        }

        private async void RefreshCompetitionCatalogAsync()
        {
            if (_competitionStatus != null)
                _competitionStatus.Text = LocalizationService.T("Refreshing available competitions from enabled providers...", "در حال دریافت رقابت‌های در دسترس سرویس‌های فعال...");
            try
            {
                int season = DateTime.UtcNow.Month >= 7 ? DateTime.UtcNow.Year : DateTime.UtcNow.Year - 1;
                var fixtures = await System.Threading.Tasks.Task.Run(() =>
                    FootballDataService.FetchAllUpcoming(season, DateTime.UtcNow.Date));
                if (fixtures != null && fixtures.Count > 0)
                    new KoaZa.Data.DatabaseService().SaveFixtures(fixtures);
                LoadCompetitionChecks();
                _competitionStatus.Text = LocalizationService.T("Catalog refreshed from enabled providers. Competitions with fixtures in the current date window are now available for selection. ", "فهرست از سرویس‌های فعال نوسازی شد. رقابت‌هایی که در بازه فعلی مسابقه دارند اکنون قابل انتخاب‌اند. ") +
                    (fixtures == null ? 0 : fixtures.Count) + LocalizationService.T(" fixtures retrieved.", " مسابقه دریافت شد.");
            }
            catch (Exception ex)
            {
                LoadCompetitionChecks();
                _competitionStatus.Text = LocalizationService.T("Provider refresh failed; showing all competitions already saved locally. ", "دریافت از سرویس ناموفق بود؛ همه رقابت‌های ذخیره‌شده محلی نمایش داده می‌شوند. ") + ex.Message;
            }
        }

        private void LoadCompetitionChecks()
        {
            if (_competitionChecks == null) return;
            _competitionChecks.Children.Clear();
            try
            {
                var known = new KoaZa.Data.DatabaseService().GetKnownCompetitionCatalog();
                var hidden = CompetitionSelectionStore.LoadHidden();
                foreach (var entry in known)
                {
                    var rowContent = new Grid { Margin = new Thickness(4, 2, 4, 2) };
                    rowContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    rowContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    rowContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    var name = new TextBlock
                    {
                        Text = LocalizationService.League(entry.League), VerticalAlignment = VerticalAlignment.Center,
                        FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis
                    };
                    Grid.SetColumn(name, 0); rowContent.Children.Add(name);
                    var provider = new TextBlock
                    {
                        Text = entry.Providers ?? "Unknown", VerticalAlignment = VerticalAlignment.Center,
                        Foreground = new SolidColorBrush(Color.FromRgb(92, 111, 100)), FontSize = 11,
                        Margin = new Thickness(12, 0, 12, 0), ToolTip = entry.Providers
                    };
                    Grid.SetColumn(provider, 1); rowContent.Children.Add(provider);
                    var count = new TextBlock
                    {
                        Text = entry.FixtureCount.ToString(CultureInfo.InvariantCulture) + " " + LocalizationService.T("matches", "مسابقه"),
                        VerticalAlignment = VerticalAlignment.Center, Foreground = new SolidColorBrush(Color.FromRgb(22, 134, 91)), FontSize = 11
                    };
                    Grid.SetColumn(count, 2); rowContent.Children.Add(count);
                    var check = new CheckBox
                    {
                        Content = rowContent, Tag = entry.League, IsChecked = !hidden.Contains(entry.League),
                        Margin = new Thickness(2), Padding = new Thickness(8, 7, 8, 7),
                        Background = Brushes.White, ToolTip = LocalizationService.T("API source(s): ", "منبع API: ") + (entry.Providers ?? "Unknown")
                    };
                    _competitionChecks.Children.Add(check);
                }
                FilterCompetitionChecks();
                _competitionStatus.Text = known.Count == 0
                    ? LocalizationService.T("No competitions have been saved yet. Import fixtures from an enabled API first.", "هنوز رقابتی ذخیره نشده است. ابتدا از یک API فعال مسابقات دریافت کن.")
                    : LocalizationService.T("Competitions found: ", "تعداد رقابت‌های پیدا‌شده: ") + known.Count +
                      LocalizationService.T(" • All entries come from saved API fixtures.", " • همه موارد از مسابقات ذخیره‌شده توسط APIها هستند.");
            }
            catch (Exception ex)
            {
                _competitionStatus.Text = LocalizationService.T("Could not load competitions: ", "بارگذاری رقابت‌ها ناموفق بود: ") + ex.Message;
            }
        }

        private void FilterCompetitionChecks()
        {
            if (_competitionChecks == null) return;
            string query = _competitionSearchBox == null ? "" : (_competitionSearchBox.Text ?? "").Trim();
            foreach (var check in _competitionChecks.Children.OfType<CheckBox>())
            {
                string league = Convert.ToString(check.Tag) ?? "";
                check.Visibility = query.Length == 0 || league.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                    ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void SetCompetitionChecks(bool isChecked)
        {
            if (_competitionChecks == null) return;
            foreach (var check in _competitionChecks.Children.OfType<CheckBox>())
                check.IsChecked = isChecked;
        }

        private void SaveCompetitionSelection_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var hidden = _competitionChecks.Children.OfType<CheckBox>()
                    .Where(c => c.IsChecked != true).Select(c => Convert.ToString(c.Tag)).ToList();
                CompetitionSelectionStore.SaveHidden(hidden);
                _competitionStatus.Text = LocalizationService.T("Saved. Refresh the main page to apply the selection.", "ذخیره شد. برای اعمال انتخاب‌ها، فهرست صفحه اصلی را نوسازی کن.");
            }
            catch (Exception ex)
            {
                _competitionStatus.Text = LocalizationService.T("Save failed: ", "ذخیره ناموفق بود: ") + ex.Message;
            }
        }

        private void ProviderBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var choice = _providerBox.SelectedItem as ProviderChoice;
            if (choice == null) return;
            _keyBox.IsEnabled = choice.Id != "OpenLigaDB";
            _keyBox.Text = "API key / token";
            _customNameBox.Text = "Custom name";
            _baseUrlBox.Text = "HTTPS endpoint";
            _headerBox.Text = (choice.Id == "FootballBettingOdds" || choice.Id == "PremierLeagueStats" || choice.Id == "TheRundown")
                ? "X-RapidAPI-Key" : choice.Id == "BallDontLie" ? "Authorization" : "x-api-key";
            _customNameBox.IsEnabled = choice.Id == "Custom";
            _baseUrlBox.IsEnabled = choice.Id == "Custom" || choice.Id == "Floodlight" || choice.Id == "SportsOpenData" || choice.Id == "Sportmonks" || choice.Id == "TheRundown" || choice.Id == "FootballBettingOdds" || choice.Id == "Betfair" || choice.Id == "Sportradar" || choice.Id == "SportsDataIO" || choice.Id == "BallDontLie" || choice.Id == "PremierLeagueStats";
            _keyBox.ToolTip = choice.Id == "OpenLigaDB" ? "This provider does not require a key." : choice.Id == "FootballCharts" ? "Optional bearer key for the higher daily request limit; no key is required to start." : "Paste the token/key from this provider's official account.";
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var choice = _providerBox.SelectedItem as ProviderChoice;
            if (choice == null) return;
            string key = (_keyBox.Text == "API key / token" ? "" : (_keyBox.Text ?? "").Trim());
            string providerId = choice.Id;
            string baseUrl = (_baseUrlBox.Text == "HTTPS endpoint" ? "" : (_baseUrlBox.Text ?? "").Trim());
            string headerName = string.IsNullOrWhiteSpace(_headerBox.Text) ? "x-api-key" : _headerBox.Text.Trim();
            if (choice.Id == "Custom")
                providerId = (_customNameBox.Text == "Custom name" ? "" : (_customNameBox.Text ?? "").Trim());
            if (choice.Id == "Custom" || choice.Id == "Floodlight" || choice.Id == "SportsOpenData" || choice.Id == "Sportmonks" || choice.Id == "TheRundown" || choice.Id == "FootballBettingOdds" || choice.Id == "Betfair" || choice.Id == "Sportradar" || choice.Id == "SportsDataIO" || choice.Id == "BallDontLie" || choice.Id == "PremierLeagueStats")
            {
                if ((choice.Id == "Custom" && providerId.Length == 0) || baseUrl.Length == 0 || !baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    _status.Text = LocalizationService.T("Enter a provider name (for custom) and a valid HTTPS fixtures endpoint.", "برای سرویس دستی نام و برای این سرویس‌ها نشانی معتبر HTTPS وارد کن.");
                    return;
                }
            }
            if (choice.Id != "OpenLigaDB" && choice.Id != "FootballCharts" && choice.Id != "TheSportsDB" && key.Length == 0)
            {
                MessageBox.Show(this, LocalizationService.T("This provider requires an API key/token.", "این سرویس به کلید API یا توکن نیاز دارد."), LocalizationService.T("API settings", "تنظیمات API"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (_providers.Any(p => p.Provider == providerId && p.Key == key))
            {
                _status.Text = LocalizationService.T("That provider/key combination is already in the list.", "این سرویس و کلید از قبل وجود دارد.");
                return;
            }
            _providers.Add(new ApiProviderConfig { Provider = providerId, Key = key, Enabled = true, BaseUrl = baseUrl, HeaderName = headerName });
            RefreshList();
            _keyBox.Text = "";
            _customNameBox.Text = "";
            _baseUrlBox.Text = "";
            _status.Text = LocalizationService.T("Provider added. Click Save securely to persist it.", "سرویس اضافه شد؛ برای ذخیره، دکمه ذخیره امن را بزن.");
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
                _status.Text = LocalizationService.T("Saved ", "ذخیره شد: ") + _providers.Count + LocalizationService.T(" provider configuration(s), encrypted for this Windows user.", " سرویس؛ اطلاعات برای کاربر ویندوز رمزگذاری شد.");
            }
            catch (Exception ex) { _status.Text = LocalizationService.T("Save failed: ", "ذخیره ناموفق بود: ") + ex.Message; }
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
        private readonly TextBox _startDateBox;
        private readonly TextBox _endDateBox;
        private readonly DataGrid _grid;
        private readonly TextBlock _status;

        public FixturesWindow()
        {
            Title = LocalizationService.T("KoaZa — Fixture Import", "KoaZa — دریافت مسابقات");
            Width = 1000; Height = 650; MinWidth = 780; MinHeight = 480;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(244, 247, 245));
            var root = new Grid { Margin = new Thickness(18) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var top = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            top.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            top.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.25, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.9, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.55, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.85, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.85, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            string[] fieldLabels = {
                LocalizationService.T("Provider / سرویس", "سرویس دریافت"),
                LocalizationService.T("League or tournament ID", "شناسه لیگ یا تورنمنت"),
                LocalizationService.T("Season / فصل", "فصل"),
                LocalizationService.T("From date / از تاریخ", "از تاریخ"),
                LocalizationService.T("To date / تا تاریخ", "تا تاریخ"),
                LocalizationService.T("Action / اجرا", "اجرا")
            };
            for (int i = 0; i < fieldLabels.Length; i++)
            {
                var label = new TextBlock { Text = fieldLabels[i], FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(22, 59, 43)), Margin = new Thickness(0, 0, 8, 4) };
                Grid.SetColumn(label, i); Grid.SetRow(label, 0); top.Children.Add(label);
            }
            _providerBox = new ComboBox { Padding = new Thickness(6), Margin = new Thickness(0, 0, 8, 0) };
            _providerBox.Items.Add(new ProviderChoice("Auto", LocalizationService.T("Auto fallback (recommended)", "انتخاب خودکار سرویس")));
            _providerBox.Items.Add(new ProviderChoice("OddsPapi", "OddsPapi"));
            _providerBox.Items.Add(new ProviderChoice("Floodlight", "Floodlight"));
            _providerBox.Items.Add(new ProviderChoice("SportsOpenData", "Sports Open Data"));
            _providerBox.Items.Add(new ProviderChoice("OpenLigaDB", "OpenLigaDB"));
            _providerBox.Items.Add(new ProviderChoice("ApiSports", "API-SPORTS"));
            _providerBox.Items.Add(new ProviderChoice("SportsAPI360", "SportsAPI360"));
            _providerBox.Items.Add(new ProviderChoice("FiveDollarFootballAPI", "5DollarFootballAPI"));
            _providerBox.Items.Add(new ProviderChoice("FootballCharts", "Football-Charts"));
            _providerBox.Items.Add(new ProviderChoice("FootballData", "Football-Data (football-data.org)"));
            _providerBox.Items.Add(new ProviderChoice("Sportmonks", "Sportmonks"));
            _providerBox.Items.Add(new ProviderChoice("TheOddsAPI", "The Odds API"));
            _providerBox.Items.Add(new ProviderChoice("TheRundown", "TheRundown.io"));
            _providerBox.Items.Add(new ProviderChoice("TheSportsDB", "TheSportsDB"));
            _providerBox.Items.Add(new ProviderChoice("AllSportsApi", "AllSportsApi"));
            _providerBox.Items.Add(new ProviderChoice("FootballBettingOdds", "Football Betting Odds"));
            _providerBox.Items.Add(new ProviderChoice("Betfair", "Betfair API"));
            _providerBox.Items.Add(new ProviderChoice("Sportradar", "Sportradar"));
            _providerBox.Items.Add(new ProviderChoice("SportsDataIO", "SportsDataIO"));
            _providerBox.Items.Add(new ProviderChoice("ScoreBat", "ScoreBat"));
            _providerBox.Items.Add(new ProviderChoice("BallDontLie", "BALLDONTLIE"));
            _providerBox.Items.Add(new ProviderChoice("PremierLeagueStats", "Premier League Stats"));
            foreach (var configured in ApiProviderStore.Load().Where(p => p.Enabled && p.Provider != "ApiFootball" && p.Provider != "ApiSports" && p.Provider != "FootballData" && p.Provider != "OpenLigaDB" && p.Provider != "OddsPapi" && p.Provider != "Floodlight" && p.Provider != "SportsOpenData" && p.Provider != "SportsAPI360" && p.Provider != "FiveDollarFootballAPI" && p.Provider != "FootballCharts" && p.Provider != "Sportmonks" && p.Provider != "TheOddsAPI" && p.Provider != "TheRundown" && p.Provider != "TheSportsDB" && p.Provider != "AllSportsApi" && p.Provider != "FootballBettingOdds" && p.Provider != "Betfair" && p.Provider != "Sportradar" && p.Provider != "SportsDataIO" && p.Provider != "ScoreBat" && p.Provider != "BallDontLie" && p.Provider != "PremierLeagueStats"))
                _providerBox.Items.Add(new ProviderChoice(configured.Provider, configured.Provider));
            _providerBox.SelectedIndex = 0;
            _providerBox.SelectionChanged += ProviderChanged;
            Grid.SetColumn(_providerBox, 0); Grid.SetRow(_providerBox, 1); top.Children.Add(_providerBox);
            _leagueBox = new TextBox { Text = "all", Padding = new Thickness(8), Margin = new Thickness(0, 0, 8, 0), ToolTip = "API-SPORTS: enter 'all' for every competition covered by the key, or a numeric league ID; football-data.org: competition code such as PL; OpenLigaDB: shortcut such as bl1" };
            Grid.SetColumn(_leagueBox, 1); Grid.SetRow(_leagueBox, 1); top.Children.Add(_leagueBox);
            _seasonBox = new TextBox { Text = DateTime.UtcNow.Month >= 7 ? DateTime.UtcNow.Year.ToString() : (DateTime.UtcNow.Year - 1).ToString(), Padding = new Thickness(8), Margin = new Thickness(0, 0, 8, 0), ToolTip = "Season year (for providers that require it)" };
            Grid.SetColumn(_seasonBox, 2); Grid.SetRow(_seasonBox, 1); top.Children.Add(_seasonBox);
            _startDateBox = new TextBox { Text = DateTime.UtcNow.Date.AddDays(-7).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Padding = new Thickness(8), Margin = new Thickness(0, 0, 8, 0), ToolTip = "Start date, format yyyy-MM-dd." };
            Grid.SetColumn(_startDateBox, 3); Grid.SetRow(_startDateBox, 1); top.Children.Add(_startDateBox);
            _endDateBox = new TextBox { Text = DateTime.UtcNow.Date.AddDays(7).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Padding = new Thickness(8), Margin = new Thickness(0, 0, 8, 0), ToolTip = "End date, format yyyy-MM-dd. Maximum: 7 days in the future." };
            Grid.SetColumn(_endDateBox, 4); Grid.SetRow(_endDateBox, 1); top.Children.Add(_endDateBox);
            var fetch = new Button { Content = "Fetch & save from date", Padding = new Thickness(14, 8, 14, 8), Background = new SolidColorBrush(Color.FromRgb(22, 134, 91)), Foreground = Brushes.White, BorderThickness = new Thickness(0), ToolTip = "Fetch fixtures/results between the selected dates and save them locally." };
            fetch.Content = LocalizationService.T("Fetch & save", "دریافت و ذخیره");
            fetch.Click += Fetch_Click; Grid.SetColumn(fetch, 5); Grid.SetRow(fetch, 1); top.Children.Add(fetch);
            root.Children.Add(top); Grid.SetRow(top, 0);

            _grid = new DataGrid { IsReadOnly = true, AutoGenerateColumns = false, CanUserAddRows = false, Margin = new Thickness(0, 0, 0, 10) };
            _grid.Columns.Add(new System.Windows.Controls.DataGridTextColumn { Header = LocalizationService.T("Competition", "رقابت"), Binding = new System.Windows.Data.Binding("League"), Width = new DataGridLength(1.2, DataGridLengthUnitType.Star) });
            _grid.Columns.Add(new System.Windows.Controls.DataGridTextColumn { Header = LocalizationService.T("Kickoff", "زمان شروع"), Binding = new System.Windows.Data.Binding("Kickoff"), Width = new DataGridLength(1.3, DataGridLengthUnitType.Star) });
            _grid.Columns.Add(new System.Windows.Controls.DataGridTextColumn { Header = LocalizationService.T("Home team", "میزبان"), Binding = new System.Windows.Data.Binding("HomeTeam"), Width = new DataGridLength(1.2, DataGridLengthUnitType.Star) });
            _grid.Columns.Add(new System.Windows.Controls.DataGridTextColumn { Header = LocalizationService.T("Away team", "میهمان"), Binding = new System.Windows.Data.Binding("AwayTeam"), Width = new DataGridLength(1.2, DataGridLengthUnitType.Star) });
            _grid.Columns.Add(new System.Windows.Controls.DataGridTextColumn { Header = LocalizationService.T("Home goals", "گل میزبان"), Binding = new System.Windows.Data.Binding("HomeGoals"), Width = new DataGridLength(0.7, DataGridLengthUnitType.Star) });
            _grid.Columns.Add(new System.Windows.Controls.DataGridTextColumn { Header = LocalizationService.T("Away goals", "گل میهمان"), Binding = new System.Windows.Data.Binding("AwayGoals"), Width = new DataGridLength(0.7, DataGridLengthUnitType.Star) });
            root.Children.Add(_grid); Grid.SetRow(_grid, 1);
            _status = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = new SolidColorBrush(Color.FromRgb(65, 84, 73)) };
            root.Children.Add(_status); Grid.SetRow(_status, 2);
            Content = root;
            ProviderChanged(null, null);
            LocalizationService.Apply(this);
        }

        private void ProviderChanged(object sender, SelectionChangedEventArgs e)
        {
            var choice = _providerBox.SelectedItem as ProviderChoice;
            if (choice == null) return;
            _leagueBox.Text = choice.Id == "Auto" ? "all" : choice.Id == "ApiFootball" || choice.Id == "ApiSports" ? "39" : choice.Id == "FootballData" ? "PL" : choice.Id == "OpenLigaDB" ? "bl1" : choice.Id == "FootballCharts" ? "premier" : "all";
            _leagueBox.ToolTip = choice.Id == "Auto" ? "Bulk refresh: all competitions available from enabled compatible feeds for the next 7 days plus live matches. The field is informational in Auto mode." :
                choice.Id == "ApiFootball" || choice.Id == "ApiSports" ? "Enter an API-Sports numeric league ID (e.g. 39 for Premier League)." :
                choice.Id == "FootballData" ? "Enter competition code (e.g. PL, PD, BL1, SA, FL1)." :
                choice.Id == "OpenLigaDB" ? "Enter OpenLigaDB league shortcut (e.g. bl1)." :
                choice.Id == "OddsPapi" ? "Enter the OddsPapi tournamentId (not the API-Sports league ID)." :
                choice.Id == "SportsAPI360" ? "Enter tournamentId, tournament name, or 'all' for the next 7 days." :
                choice.Id == "FiveDollarFootballAPI" ? "Enter a numeric league ID or 'all'. Historical coverage depends on your plan." :
                choice.Id == "FootballCharts" ? "Enter a Football-Charts league slug (e.g. premier, germany3). No key is required." :
                choice.Id == "Sportmonks" || choice.Id == "TheOddsAPI" || choice.Id == "TheRundown" ? "Configure an HTTPS endpoint returning the normalized fixtures JSON format in API Settings; direct native API adapters are not yet built in." :
                "For Floodlight, Sports Open Data or custom providers, configure the HTTPS fixtures endpoint in API Settings.";
        }

        private async void Fetch_Click(object sender, RoutedEventArgs e)
        {
            var choice = _providerBox.SelectedItem as ProviderChoice;
            int season;
            DateTime startDate;
            DateTime endDate;
            if (choice == null || !int.TryParse(_seasonBox.Text.Trim(), out season) || season < 2000 || season > 2100)
            {
                MessageBox.Show(this, LocalizationService.T("Enter a valid season year.", "سال فصل معتبر وارد کن."), LocalizationService.T("Fixture import", "دریافت مسابقات"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (!DateTime.TryParseExact(_startDateBox.Text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out startDate) || startDate.Date > DateTime.UtcNow.Date)
            {
                MessageBox.Show(this, LocalizationService.T("Enter a valid From date in yyyy-MM-dd format; it cannot be in the future.", "تاریخ «از» را با قالب yyyy-MM-dd وارد کن؛ تاریخ شروع نباید در آینده باشد."), LocalizationService.T("Fixture import", "دریافت مسابقات"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (!DateTime.TryParseExact(_endDateBox.Text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out endDate) ||
                endDate.Date < startDate.Date || endDate.Date > DateTime.UtcNow.Date.AddDays(7))
            {
                MessageBox.Show(this, LocalizationService.T("Enter a valid To date on or after the From date and no more than 7 days in the future.", "تاریخ «تا» باید معتبر، هم‌زمان یا بعد از تاریخ شروع و حداکثر تا ۷ روز آینده باشد."), LocalizationService.T("Fixture import", "دریافت مسابقات"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var fetchButton = sender as Button;
            if (fetchButton != null) fetchButton.IsEnabled = false;
            string competition = _leagueBox.Text.Trim();
            try
            {
                _status.Text = LocalizationService.T("Requesting fixture data. Please wait...", "در حال دریافت مسابقات...");
                _status.Text = LocalizationService.IsPersian
                    ? "در حال دریافت مسابقات از " + startDate.ToString("yyyy-MM-dd") + " تا " + endDate.ToString("yyyy-MM-dd") + "..."
                    : "Fetching fixtures/results from " + startDate.ToString("yyyy-MM-dd") + " through " + endDate.ToString("yyyy-MM-dd") + "...";
                var records = await System.Threading.Tasks.Task.Run(() =>
                    FootballDataService.FetchFromDate(choice.Id, competition, season, startDate, endDate));
                DatabaseServiceProxy.SaveFixtures(records);
                _grid.ItemsSource = records.Select(r => new FixtureImportRow(r)).ToList();
                _status.Text = LocalizationService.T("Fixtures saved. Importing detailed stats for up to 10 completed matches...", "مسابقات ذخیره شد؛ در حال دریافت آمار تفصیلی حداکثر ۱۰ بازی تمام‌شده...");
                int statsImported = await System.Threading.Tasks.Task.Run(() => DatabaseServiceProxy.ImportHistoricalStatistics(records));
                _status.Text = LocalizationService.IsPersian
                    ? records.Count + " مسابقه از " + startDate.ToString("yyyy-MM-dd") + " تا " + endDate.ToString("yyyy-MM-dd") + " دریافت و ذخیره شد؛ آمار ساختاریافته برای " + statsImported + " مسابقه وارد شد. داده‌های قبلی حفظ می‌شوند؛ پوشش به سرویس و سهمیه وابسته است."
                    : "Received and saved " + records.Count + " fixtures/results from " + startDate.ToString("yyyy-MM-dd") + " through " + endDate.ToString("yyyy-MM-dd") + "; structured event stats imported for " + statsImported + " matches. Existing local data is retained; coverage depends on provider and quota.";
            }
            catch (Exception ex) { _status.Text = LocalizationService.T("Import failed: ", "دریافت ناموفق بود: ") + ex.Message; }
            finally { if (fetchButton != null) fetchButton.IsEnabled = true; }
        }

        private sealed class FixtureImportRow
        {
            public string League { get; private set; }
            public string Kickoff { get; private set; }
            public string HomeTeam { get; private set; }
            public string AwayTeam { get; private set; }
            public string HomeGoals { get; private set; }
            public string AwayGoals { get; private set; }
            public FixtureImportRow(FixtureRecord r)
            {
                League = LocalizationService.League(r.League);
                DateTime dt;
                Kickoff = DateTime.TryParse(r.KickoffUtc, out dt) ? dt.ToLocalTime().ToString(LocalizationService.IsPersian ? "yyyy/MM/dd HH:mm" : "dd MMM HH:mm", CultureInfo.CurrentCulture) : r.KickoffUtc;
                HomeTeam = LocalizationService.Team(r.HomeTeam);
                AwayTeam = LocalizationService.Team(r.AwayTeam);
                HomeGoals = r.HomeGoals.HasValue ? r.HomeGoals.Value.ToString() : "—";
                AwayGoals = r.AwayGoals.HasValue ? r.AwayGoals.Value.ToString() : "—";
            }
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

        public static int ImportHistoricalStatistics(List<FixtureRecord> fixtures)
        {
            if (fixtures == null) return 0;
            var completed = fixtures.Where(f => f.HomeGoals.HasValue && f.AwayGoals.HasValue &&
                new[] { "FT", "AET", "PEN", "FINISHED", "MATCH FINISHED", "FULL TIME", "AFTER EXTRA TIME", "AFTER PENALTIES" }.Contains((f.Status ?? "").Trim().ToUpperInvariant()) &&
                (f.Provider == "API-Football" || f.Provider == "ApiFootball" || f.Provider == "ApiSports" || f.Provider == "FiveDollarFootballAPI"))
                .OrderByDescending(f => f.KickoffUtc).ToList();
            var candidates = completed.Where(f => f.Provider == "FiveDollarFootballAPI").Take(10)
                .Concat(completed.Where(f => f.Provider != "FiveDollarFootballAPI").Take(5)).ToList();
            var db = new KoaZa.Data.DatabaseService();
            int imported = 0;
            foreach (var fixture in candidates)
            {
                try
                {
                    var baseline = new MatchStatisticsRecord
                    {
                        Provider = fixture.Provider, ExternalId = fixture.ExternalId, League = fixture.League,
                        KickoffUtc = fixture.KickoffUtc, Status = fixture.Status, HomeTeam = fixture.HomeTeam, AwayTeam = fixture.AwayTeam,
                        HomeGoals = fixture.HomeGoals, AwayGoals = fixture.AwayGoals, Referee = fixture.Referee,
                        HomeShotsOnTarget = fixture.HomeShotsOnTarget, AwayShotsOnTarget = fixture.AwayShotsOnTarget,
                        HomeCorners = fixture.HomeCorners, AwayCorners = fixture.AwayCorners,
                        HomeYellowCards = fixture.HomeYellowCards, AwayYellowCards = fixture.AwayYellowCards,
                        DataSource = fixture.Provider, UpdatedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
                    };
                    if (baseline.HomeShotsOnTarget.HasValue || baseline.AwayShotsOnTarget.HasValue ||
                        baseline.HomeCorners.HasValue || baseline.AwayCorners.HasValue ||
                        baseline.HomeYellowCards.HasValue || baseline.AwayYellowCards.HasValue ||
                        !string.IsNullOrWhiteSpace(baseline.Referee))
                        db.SaveMatchStatistics(baseline);
                    string json = FootballDataService.FetchFixtureDetails(fixture);
                    if (string.IsNullOrWhiteSpace(json)) continue;
                    db.SaveFixtureDetails(fixture.Provider, fixture.ExternalId, json);
                    var normalized = fixture.Provider == "FiveDollarFootballAPI"
                        ? MatchStatisticsParser.ParseFiveDollarOrGeneric(json, fixture)
                        : MatchStatisticsParser.ParseApiFootball(json, fixture);
                    if (normalized == null) continue;
                    bool hasStats = normalized.HomeShots.HasValue || normalized.AwayShots.HasValue ||
                        normalized.HomeShotsOnTarget.HasValue || normalized.AwayShotsOnTarget.HasValue ||
                        normalized.HomeCorners.HasValue || normalized.AwayCorners.HasValue ||
                        normalized.HomeYellowCards.HasValue || normalized.AwayYellowCards.HasValue ||
                        normalized.HomeRedCards.HasValue || normalized.AwayRedCards.HasValue ||
                        !string.IsNullOrWhiteSpace(normalized.Referee);
                    var events = MatchStatisticsParser.ParseEvents(json, fixture);
                    if (hasStats)
                    {
                        db.SaveMatchStatistics(normalized);
                        var legacy = FootballDataService.ExtractFixtureStatistics(json, fixture);
                        legacy.HomeCorners = normalized.HomeCorners; legacy.AwayCorners = normalized.AwayCorners;
                        legacy.HomeShotsOnTarget = normalized.HomeShotsOnTarget; legacy.AwayShotsOnTarget = normalized.AwayShotsOnTarget;
                        legacy.HomeYellowCards = normalized.HomeYellowCards; legacy.AwayYellowCards = normalized.AwayYellowCards;
                        legacy.Referee = normalized.Referee;
                        db.SaveNormalizedFixtureStatistics(legacy);
                    }
                    if (MatchStatisticsParser.HasEventPayload(json, fixture))
                        db.ReplaceMatchEvents(fixture.Provider, fixture.ExternalId, events);
                    if (hasStats || events.Count > 0) imported++;
                }
                catch { /* One unavailable match must not cancel the rest of the import. */ }
            }
            return imported;
        }
    }
}