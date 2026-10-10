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
                string label = Provider == "ApiFootball" || Provider == "ApiSports" ? "API-SPORTS" :
                    Provider == "FootballData" ? "football-data.org" :
                    Provider == "OpenLigaDB" ? "OpenLigaDB (no key)" :
                    Provider == "OddsPapi" ? "OddsPapi" : Provider == "Floodlight" ? "Floodlight" :
                    Provider == "SportsOpenData" ? "Sports Open Data" : Provider == "SportsAPI360" ? "SportsAPI360" : Provider;
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

    public sealed class NewsItem
    {
        public string Title { get; set; }
        public string Url { get; set; }
        public string Summary { get; set; }
        public string Published { get; set; }
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

        // Bulk refresh: API-SPORTS date queries return fixtures across all competitions covered by the account.
        // The provider's coverage and quota still determine which competitions/data are available.
        public static List<FixtureRecord> FetchAllUpcoming(int season)
        {
            var combined = new List<FixtureRecord>();
            var errors = new List<string>();
            var providers = ApiProviderStore.Load().Where(p => p.Enabled).ToList();
            var apiKeys = providers.Where(p => (p.Provider == "ApiFootball" || p.Provider == "ApiSports") && !string.IsNullOrWhiteSpace(p.Key)).ToList();

            foreach (var config in apiKeys)
            {
                try
                {
                    for (int day = 0; day < 7; day++)
                    {
                        string date = DateTime.UtcNow.Date.AddDays(day).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                        string json = Request("https://v3.football.api-sports.io/fixtures?date=" + date + "&timezone=UTC", "x-apisports-key", config.Key);
                        var root = Parse(json);
                        if (HasErrors(root)) throw new InvalidOperationException("API-SPORTS returned an API or quota error for " + date + ".");
                        combined.AddRange(ParseApiFootballFixtures(root));
                    }
                    string liveJson = Request("https://v3.football.api-sports.io/fixtures?live=all", "x-apisports-key", config.Key);
                    var liveRoot = Parse(liveJson);
                    if (!HasErrors(liveRoot)) combined.AddRange(ParseApiFootballFixtures(liveRoot));
                    if (combined.Count > 0) break;
                }
                catch (Exception ex) { errors.Add("API-SPORTS: " + ex.Message); }
            }

            // football-data.org bulk endpoint supports multiple competition codes in one request (coverage is plan-dependent).
            if (providers.Any(p => p.Enabled && p.Provider == "FootballData" && !string.IsNullOrWhiteSpace(p.Key)))
            {
                try { combined.AddRange(FetchFootballDataBulk(season)); }
                catch (Exception ex) { errors.Add("football-data.org: " + ex.Message); }
            }

            // OpenLigaDB is a no-key fallback for the German Bundesliga when it is enabled.
            if (providers.Any(p => p.Enabled && p.Provider == "OpenLigaDB"))
            {
                try { combined.AddRange(FetchOpenLiga("bl1", season)); }
                catch (Exception ex) { errors.Add("OpenLigaDB: " + ex.Message); }
            }

            // Merge any other enabled global schedule feed when configured; competition IDs are provider-specific.
            foreach (var config in providers.Where(p => p.Provider == "SportsAPI360" && !string.IsNullOrWhiteSpace(p.Key)))
            {
                try { combined.AddRange(FetchSportsApi360("all")); }
                catch (Exception ex) { errors.Add("SportsAPI360: " + ex.Message); }
            }

            // Custom endpoints are treated as provider-defined normalized fixture feeds.
            foreach (var config in providers.Where(p => !string.IsNullOrWhiteSpace(p.BaseUrl)))
            {
                try { combined.AddRange(FetchCustom(config)); }
                catch (Exception ex) { errors.Add(config.Provider + ": " + ex.Message); }
            }

            if (combined.Count == 0)
                throw new InvalidOperationException("No fixtures could be received from enabled providers. For global competitions, configure an API-SPORTS key or a working SportsAPI360/custom feed. " + string.Join(" | ", errors));

            DateTime from = DateTime.UtcNow.Date.AddDays(-1);
            DateTime to = DateTime.UtcNow.Date.AddDays(8);
            return combined.Where(f => {
                    DateTime kickoff;
                    return !DateTime.TryParse(f.KickoffUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out kickoff)
                        || (kickoff >= from && kickoff < to);
                })
                .GroupBy(FixtureDeduplicationKey)
                .Select(g => g.OrderByDescending(x => !string.IsNullOrWhiteSpace(x.League))
                    .ThenByDescending(x => !string.IsNullOrWhiteSpace(x.ExternalId)).First())
                .ToList();
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
                    ExternalId = GetString(fixture, "id", "")
                });
            }
            return output;
        }

        public static string FetchFixtureDetails(FixtureRecord fixture)
        {
            if (fixture == null) throw new ArgumentNullException("fixture");
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

                    // Injuries are a separate API resource; attach them when available without failing the fixture details request.
                    string status = (fixture.Status ?? "").Trim().ToUpperInvariant();
                    bool live = status == "LIVE" || status == "1H" || status == "HT" || status == "2H" || status == "ET" || status == "P" || status == "BT" || status == "INT";
                    if (!live)
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

        public static string SummarizeFixtureDetails(string json)
        {
            var root = Parse(json);
            var item = GetList(root, "response").FirstOrDefault();
            if (item == null) return "No detailed statistics are available for this match yet.";
            var sb = new StringBuilder();
            var stats = GetList(item, "statistics");
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
            return sb.ToString();
        }

        public static List<NewsItem> FetchMatchNews(string homeTeam, string awayTeam, string league)
        {
            string query = "\"" + (homeTeam ?? "").Trim() + "\" OR \"" + (awayTeam ?? "").Trim() + "\" football";
            if (!string.IsNullOrWhiteSpace(league)) query += " \"" + league.Trim() + "\"";
            string url = "https://news.google.com/rss/search?q=" + Uri.EscapeDataString(query) + "&hl=en-US&gl=US&ceid=US:en";
            var doc = new System.Xml.XmlDocument();
            doc.LoadXml(Request(url, null, null));
            var result = new List<NewsItem>();
            var nodes = doc.SelectNodes("//item");
            if (nodes == null) return result;
            foreach (System.Xml.XmlNode node in nodes)
            {
                string title = node["title"] == null ? "" : System.Net.WebUtility.HtmlDecode(node["title"].InnerText);
                string link = node["link"] == null ? "" : node["link"].InnerText;
                string description = node["description"] == null ? "" : System.Net.WebUtility.HtmlDecode(node["description"].InnerText);
                string pubDate = node["pubDate"] == null ? "" : node["pubDate"].InnerText;
                if (title.Length == 0 || link.Length == 0) continue;
                result.Add(new NewsItem { Title = title, Url = link, Summary = (string.IsNullOrWhiteSpace(pubDate) ? "" : pubDate + " — ") + StripHtml(description), Published = pubDate });
                if (result.Count >= 8) break;
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
            if (provider == "SportsAPI360") return FetchSportsApi360(league);
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
            return null;
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

        // Custom endpoints should return a JSON array, or an object containing response/matches/fixtures/data.
        // Each row should provide homeTeam, awayTeam, date (or kickoff/date fields), and optional league/homeGoals/awayGoals.
        private static List<FixtureRecord> FetchCustom(ApiProviderConfig config)
        {
            string json = Request(config.BaseUrl, string.IsNullOrWhiteSpace(config.HeaderName) ? "x-api-key" : config.HeaderName, config.Key);
            object parsed = new JavaScriptSerializer().DeserializeObject(json);
            var rootMap = parsed as Dictionary<string, object>;
            object rowsObject = parsed;
            if (rootMap != null)
            {
                rowsObject = rootMap.ContainsKey("response") ? rootMap["response"] :
                    rootMap.ContainsKey("matches") ? rootMap["matches"] :
                    rootMap.ContainsKey("fixtures") ? rootMap["fixtures"] :
                    rootMap.ContainsKey("data") ? rootMap["data"] : parsed;
            }
            var rows = rowsObject as System.Collections.ArrayList;
            if (rows == null && rowsObject is object[]) rows = new System.Collections.ArrayList((object[])rowsObject);
            if (rows == null) throw new InvalidOperationException("Custom provider response must be a JSON array or contain response/matches/fixtures/data array.");
            var output = new List<FixtureRecord>();
            foreach (var item in rows)
            {
                string home = GetString(item, "homeTeam", "");
                string away = GetString(item, "awayTeam", "");
                if (string.IsNullOrWhiteSpace(home) || home.IndexOf("Dictionary", StringComparison.OrdinalIgnoreCase) >= 0) home = GetString(GetMap(item, "homeTeam"), "name", "");
                if (string.IsNullOrWhiteSpace(away) || away.IndexOf("Dictionary", StringComparison.OrdinalIgnoreCase) >= 0) away = GetString(GetMap(item, "awayTeam"), "name", "");
                if (string.IsNullOrWhiteSpace(home) || string.IsNullOrWhiteSpace(away)) continue;
                output.Add(new FixtureRecord {
                    Provider = config.Provider,
                    League = GetString(item, "league", GetString(item, "competition", config.Provider)),
                    KickoffUtc = GetString(item, "date", GetString(item, "kickoff", GetString(item, "utcDate", ""))),
                    HomeTeam = home, AwayTeam = away,
                    HomeGoals = GetNullableInt(item, "homeGoals"),
                    AwayGoals = GetNullableInt(item, "awayGoals"),
                    Status = GetString(item, "status", ""),
                    ExternalId = GetString(item, "id", "")
                });
            }
            return output;
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

        private static List<FixtureRecord> FetchFootballDataBulk(int season)
        {
            var configs = ApiProviderStore.Load().Where(p => p.Enabled && p.Provider == "FootballData" && !string.IsNullOrWhiteSpace(p.Key)).ToList();
            if (configs.Count == 0) return new List<FixtureRecord>();
            string from = DateTime.UtcNow.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            string to = DateTime.UtcNow.Date.AddDays(7).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            string competitions = "PL,PD,BL1,SA,FL1,CL,EL,WC,EC";
            Exception last = null;
            foreach (var config in configs)
            {
                try
                {
                    string url = "https://api.football-data.org/v4/matches?competitions=" + Uri.EscapeDataString(competitions) +
                        "&dateFrom=" + from + "&dateTo=" + to;
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
            throw new InvalidOperationException("Could not retrieve the configured major competitions.", last);
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
        private readonly TextBox _customNameBox;
        private readonly TextBox _baseUrlBox;
        private readonly TextBox _headerBox;
        private readonly ListBox _providersList;
        private readonly TextBlock _status;
        private List<ApiProviderConfig> _providers;

        public ApiSettingsWindow()
        {
            Title = LocalizationService.T("KoaZa — API Provider Settings", "KoaZa — تنظیمات سرویس‌های API");
            Width = 1040; Height = 570; MinWidth = 850; MinHeight = 460;
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
            _providerBox.Items.Add(new ProviderChoice("FootballData", "football-data.org"));
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
            Content = root;
            ProviderBox_SelectionChanged(null, null);
            RefreshList();
            LocalizationService.Apply(this);
        }

        private void ProviderBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var choice = _providerBox.SelectedItem as ProviderChoice;
            if (choice == null) return;
            _keyBox.IsEnabled = choice.Id != "OpenLigaDB";
            _keyBox.Text = "API key / token";
            _customNameBox.Text = "Custom name";
            _baseUrlBox.Text = "HTTPS endpoint";
            _headerBox.Text = "x-api-key";
            _customNameBox.IsEnabled = choice.Id == "Custom";
            _baseUrlBox.IsEnabled = choice.Id == "Custom" || choice.Id == "Floodlight" || choice.Id == "SportsOpenData";
            _keyBox.ToolTip = choice.Id == "OpenLigaDB" ? "This provider does not require a key." : "Paste the token/key from this provider's official account.";
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
            if (choice.Id == "Custom" || choice.Id == "Floodlight" || choice.Id == "SportsOpenData")
            {
                if ((choice.Id == "Custom" && providerId.Length == 0) || baseUrl.Length == 0 || !baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    _status.Text = LocalizationService.T("Enter a provider name (for custom) and a valid HTTPS fixtures endpoint.", "برای سرویس دستی نام و برای این سرویس‌ها نشانی معتبر HTTPS وارد کن.");
                    return;
                }
            }
            if (choice.Id != "OpenLigaDB" && key.Length == 0)
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
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.5, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.7, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _providerBox = new ComboBox { Padding = new Thickness(6), Margin = new Thickness(0, 0, 8, 0) };
            _providerBox.Items.Add(new ProviderChoice("Auto", LocalizationService.T("Auto fallback (recommended)", "انتخاب خودکار سرویس")));
            _providerBox.Items.Add(new ProviderChoice("OddsPapi", "OddsPapi"));
            _providerBox.Items.Add(new ProviderChoice("Floodlight", "Floodlight"));
            _providerBox.Items.Add(new ProviderChoice("SportsOpenData", "Sports Open Data"));
            _providerBox.Items.Add(new ProviderChoice("OpenLigaDB", "OpenLigaDB"));
            _providerBox.Items.Add(new ProviderChoice("ApiSports", "API-SPORTS"));
            _providerBox.Items.Add(new ProviderChoice("SportsAPI360", "SportsAPI360"));
            _providerBox.Items.Add(new ProviderChoice("FootballData", "football-data.org"));
            foreach (var configured in ApiProviderStore.Load().Where(p => p.Enabled && p.Provider != "ApiFootball" && p.Provider != "ApiSports" && p.Provider != "FootballData" && p.Provider != "OpenLigaDB" && p.Provider != "OddsPapi" && p.Provider != "Floodlight" && p.Provider != "SportsOpenData" && p.Provider != "SportsAPI360"))
                _providerBox.Items.Add(new ProviderChoice(configured.Provider, configured.Provider));
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
            _leagueBox.Text = choice.Id == "Auto" ? "all" : choice.Id == "ApiFootball" || choice.Id == "ApiSports" ? "39" : choice.Id == "FootballData" ? "PL" : choice.Id == "OpenLigaDB" ? "bl1" : "all";
            _leagueBox.ToolTip = choice.Id == "Auto" ? "Bulk refresh: all competitions available from enabled compatible feeds for the next 7 days plus live matches. The field is informational in Auto mode." :
                choice.Id == "ApiFootball" || choice.Id == "ApiSports" ? "Enter an API-Sports numeric league ID (e.g. 39 for Premier League)." :
                choice.Id == "FootballData" ? "Enter competition code (e.g. PL, PD, BL1, SA, FL1)." :
                choice.Id == "OpenLigaDB" ? "Enter OpenLigaDB league shortcut (e.g. bl1)." :
                choice.Id == "OddsPapi" ? "Enter the OddsPapi tournamentId (not the API-Sports league ID)." :
                choice.Id == "SportsAPI360" ? "Enter tournamentId, tournament name, or 'all' for the next 7 days." :
                "For Floodlight, Sports Open Data or custom providers, configure the HTTPS fixtures endpoint in API Settings.";
        }

        private void Fetch_Click(object sender, RoutedEventArgs e)
        {
            var choice = _providerBox.SelectedItem as ProviderChoice;
            int season;
            if (choice == null || !int.TryParse(_seasonBox.Text.Trim(), out season) || season < 2000 || season > 2100)
            {
                MessageBox.Show(this, LocalizationService.T("Enter a valid season year.", "سال فصل معتبر وارد کن."), LocalizationService.T("Fixture import", "دریافت مسابقات"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            try
            {
                _status.Text = LocalizationService.T("Requesting fixture data. Please wait...", "در حال دریافت مسابقات...");
                var records = choice.Id == "Auto"
                    ? FootballDataService.FetchAllUpcoming(season)
                    : FootballDataService.Fetch(choice.Id, _leagueBox.Text.Trim(), season);
                DatabaseServiceProxy.SaveFixtures(records);
                _grid.ItemsSource = records.Select(r => new FixtureImportRow(r)).ToList();
                _status.Text = LocalizationService.IsPersian
                    ? records.Count + " مسابقه از رقابت‌های تحت پوشش سرویس‌های فعال دریافت و ذخیره شد. برای پوشش کامل‌تر، سرویس و سهمیه مناسب لازم است."
                    : "Received and saved " + records.Count + " fixtures across competitions covered by the enabled providers. Coverage depends on provider support and quota.";
            }
            catch (Exception ex) { _status.Text = LocalizationService.T("Import failed: ", "دریافت ناموفق بود: ") + ex.Message; }
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
    }
}