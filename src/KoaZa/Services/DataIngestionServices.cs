using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using KoaZa.Data;
using KoaZa.Services;

namespace KoaZa.Services
{
    public sealed class ImportedCsvResult
    {
        public int Imported { get; set; }
        public int Skipped { get; set; }
        public List<string> Warnings { get; private set; } = new List<string>();
    }

    public sealed class RssHeadline
    {
        public string Source { get; set; }
        public string Title { get; set; }
        public string Link { get; set; }
        public string Summary { get; set; }
        public DateTime? PublishedUtc { get; set; }
    }

    public static class HistoricalCsvImporter
    {
        public static ImportedCsvResult Import(string path, DatabaseService database)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) throw new FileNotFoundException("CSV file not found.", path);
            if (database == null) throw new ArgumentNullException("database");
            var result = new ImportedCsvResult();
            var lines = File.ReadAllLines(path, Encoding.UTF8);
            if (lines.Length < 2) throw new InvalidDataException("CSV contains no data rows.");
            char delimiter = lines[0].Count(c => c == ';') > lines[0].Count(c => c == ',') ? ';' : ',';
            var headers = ParseLine(lines[0], delimiter).Select((h, i) => new { Key = h.Trim().Trim('\uFEFF').ToUpperInvariant(), Index = i })
                .GroupBy(x => x.Key).ToDictionary(g => g.Key, g => g.First().Index);
            if (!headers.ContainsKey("HOMETEAM") || !headers.ContainsKey("AWAYTEAM"))
                throw new InvalidDataException("Required columns HomeTeam and AwayTeam were not found.");
            string sourceName = Path.GetFileName(path);
            string provider = "CSV:" + Path.GetFileNameWithoutExtension(path);
            for (int lineNo = 1; lineNo < lines.Length; lineNo++)
            {
                if (string.IsNullOrWhiteSpace(lines[lineNo])) continue;
                try
                {
                    var cells = ParseLine(lines[lineNo], delimiter);
                    string home = Get(cells, headers, "HOMETEAM");
                    string away = Get(cells, headers, "AWAYTEAM");
                    int hg, ag;
                    if (home.Length == 0 || away.Length == 0 ||
                        !TryInt(Get(cells, headers, "FTHG"), out hg) ||
                        !TryInt(Get(cells, headers, "FTAG"), out ag))
                    { result.Skipped++; continue; }

                    DateTime date;
                    string rawDate = Get(cells, headers, "DATE");
                    if (!DateTime.TryParseExact(rawDate, new[] { "dd/MM/yyyy", "d/MM/yyyy", "dd/M/yyyy", "d/M/yyyy", "yyyy-MM-dd", "yyyy/MM/dd", "MM/dd/yyyy" },
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out date) &&
                        !DateTime.TryParse(rawDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                    { result.Skipped++; result.Warnings.Add("Line " + (lineNo + 1) + ": invalid date."); continue; }

                    string league = Get(cells, headers, "DIV");
                    if (league.Length == 0) league = Path.GetFileNameWithoutExtension(path);
                    string kickoff = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc).ToString("o", CultureInfo.InvariantCulture);
                    string id = StableId(league, kickoff, home, away);
                    var record = new MatchStatisticsRecord
                    {
                        Provider = provider, ExternalId = id, League = league, KickoffUtc = kickoff,
                        Status = "FINISHED", HomeTeam = home, AwayTeam = away, HomeGoals = hg, AwayGoals = ag,
                        HomeShots = NullableInt(Get(cells, headers, "HS")),
                        AwayShots = NullableInt(Get(cells, headers, "AS")),
                        HomeShotsOnTarget = NullableInt(Get(cells, headers, "HST")),
                        AwayShotsOnTarget = NullableInt(Get(cells, headers, "AST")),
                        HomeCorners = NullableInt(Get(cells, headers, "HC")),
                        AwayCorners = NullableInt(Get(cells, headers, "AC")),
                        HomeYellowCards = NullableInt(Get(cells, headers, "HY")),
                        AwayYellowCards = NullableInt(Get(cells, headers, "AY")),
                        HomeRedCards = NullableInt(Get(cells, headers, "HR")),
                        AwayRedCards = NullableInt(Get(cells, headers, "AR")),
                        Referee = EmptyToNull(Get(cells, headers, "REFEREE")),
                        DataSource = sourceName, UpdatedUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
                    };
                    database.SaveMatchStatistics(record);
                    result.Imported++;
                }
                catch (Exception ex)
                {
                    result.Skipped++;
                    if (result.Warnings.Count < 30) result.Warnings.Add("Line " + (lineNo + 1) + ": " + ex.Message);
                }
            }
            return result;
        }

        private static List<string> ParseLine(string line, char delimiter)
        {
            var cells = new List<string>();
            var value = new StringBuilder();
            bool quoted = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (quoted && i + 1 < line.Length && line[i + 1] == '"') { value.Append('"'); i++; }
                    else quoted = !quoted;
                }
                else if (c == delimiter && !quoted) { cells.Add(value.ToString().Trim()); value.Clear(); }
                else value.Append(c);
            }
            cells.Add(value.ToString().Trim());
            return cells;
        }
        private static string Get(List<string> cells, Dictionary<string, int> headers, string name)
        {
            int i;
            return headers.TryGetValue(name, out i) && i < cells.Count ? (cells[i] ?? "").Trim() : "";
        }
        private static bool TryInt(string text, out int value) { return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value); }
        private static int? NullableInt(string text) { int value; return TryInt(text, out value) ? (int?)value : null; }
        private static string EmptyToNull(string text) { return string.IsNullOrWhiteSpace(text) ? null : text.Trim(); }
        private static string StableId(string league, string date, string home, string away)
        {
            string raw = (league + "|" + date + "|" + home + "|" + away).ToUpperInvariant();
            using (var sha = SHA256.Create())
                return "csv-" + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(raw))).Replace("-", "").Substring(0, 24).ToLowerInvariant();
        }
    }

    public sealed class CsvAutoSyncSettings
    {
        public bool Enabled { get; set; }
        public int IntervalMinutes { get; set; }
        public string Urls { get; set; }
    }

    public static class CsvAutoSyncService
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KoaZa", "csv-sync.txt");

        public static CsvAutoSyncSettings LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var lines = File.ReadAllLines(SettingsPath, Encoding.UTF8);
                    int interval;
                    return new CsvAutoSyncSettings {
                        Enabled = lines.Length > 0 && lines[0].Trim() == "1",
                        IntervalMinutes = lines.Length > 1 && int.TryParse(lines[1], out interval) ? Math.Max(5, Math.Min(10080, interval)) : 60,
                        Urls = lines.Length > 2 ? string.Join(Environment.NewLine, lines.Skip(2)) : ""
                    };
                }
            }
            catch { }
            return new CsvAutoSyncSettings { Enabled = false, IntervalMinutes = 60, Urls = "" };
        }

        public static void SaveSettings(bool enabled, int intervalMinutes, string urls)
        {
            intervalMinutes = Math.Max(5, Math.Min(10080, intervalMinutes));
            var cleanUrls = (urls ?? "").Split(new[] { '\\r', '\\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            foreach (string url in cleanUrls)
            {
                Uri uri;
                if (!Uri.TryCreate(url, UriKind.Absolute, out uri) ||
                    (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
                    throw new InvalidOperationException("CSV sources must be valid HTTP or HTTPS URLs.");
            }
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            File.WriteAllLines(SettingsPath, new[] { enabled ? "1" : "0", intervalMinutes.ToString(CultureInfo.InvariantCulture) }.Concat(cleanUrls), Encoding.UTF8);
        }

        public static ImportedCsvResult SyncConfigured(DatabaseService database)
        {
            if (database == null) throw new ArgumentNullException("database");
            var settings = LoadSettings();
            var urls = (settings.Urls ?? "").Split(new[] { '\\r', '\\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (urls.Count == 0) throw new InvalidOperationException("Add at least one public CSV URL in Settings > CSV import.");
            var total = new ImportedCsvResult();
            foreach (string url in urls)
            {
                string tempPath = Path.Combine(Path.GetTempPath(), "koaza-csv-" + Guid.NewGuid().ToString("N") + ".csv");
                try
                {
                    var request = (HttpWebRequest)WebRequest.Create(url);
                    request.UserAgent = "KoaZaFootballAnalytics/1.0";
                    request.Timeout = 30000; request.ReadWriteTimeout = 30000;
                    request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                    using (var response = (HttpWebResponse)request.GetResponse())
                    using (var stream = response.GetResponseStream())
                    using (var output = File.Create(tempPath)) stream.CopyTo(output);
                    var result = HistoricalCsvImporter.Import(tempPath, database);
                    total.Imported += result.Imported; total.Skipped += result.Skipped;
                    total.Warnings.Add("Source " + new Uri(url).Host + ": imported " + result.Imported + ", skipped " + result.Skipped + ".");
                    total.Warnings.AddRange(result.Warnings.Take(Math.Max(0, 30 - total.Warnings.Count)));
                }
                catch (Exception ex) { total.Warnings.Add("CSV source " + url + ": " + ex.Message); }
                finally { try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { } }
            }
            if (total.Imported == 0 && total.Warnings.Count == 0) throw new InvalidOperationException("No CSV rows were imported.");
            return total;
        }
    }

    public static class RssNewsService
    {
        public static readonly string[] DefaultFeeds = {
            "https://www.varzesh3.com/rss/all",
            "https://parsfootball.com/feed",
            "https://www.khabarvarzeshi.com/rss"
        };
        private static readonly string CacheFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KoaZa", "NewsCache");

        public static List<RssHeadline> FetchAll(int perFeed = 30)
        {
            Directory.CreateDirectory(CacheFolder);
            var all = new List<RssHeadline>();
            foreach (string url in DefaultFeeds)
            {
                try
                {
                    string xml = Download(url);
                    string cache = Path.Combine(CacheFolder, UriSafeName(url) + ".xml");
                    File.WriteAllText(cache, xml, Encoding.UTF8);
                    all.AddRange(Parse(xml, url).Take(perFeed));
                }
                catch
                {
                    string cache = Path.Combine(CacheFolder, UriSafeName(url) + ".xml");
                    try { if (File.Exists(cache)) all.AddRange(Parse(File.ReadAllText(cache, Encoding.UTF8), url).Take(perFeed)); } catch { }
                }
            }
            return all.GroupBy(x => string.IsNullOrWhiteSpace(x.Link) ? x.Title : x.Link, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(x => x.PublishedUtc ?? DateTime.MinValue).First())
                .OrderByDescending(x => x.PublishedUtc ?? DateTime.MinValue).ToList();
        }

        public static List<RssHeadline> ReadCache()
        {
            var result = new List<RssHeadline>();
            if (!Directory.Exists(CacheFolder)) return result;
            foreach (string file in Directory.GetFiles(CacheFolder, "*.xml"))
                try { result.AddRange(Parse(File.ReadAllText(file, Encoding.UTF8), Path.GetFileNameWithoutExtension(file))); } catch { }
            return result.GroupBy(x => string.IsNullOrWhiteSpace(x.Link) ? x.Title : x.Link, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First()).OrderByDescending(x => x.PublishedUtc ?? DateTime.MinValue).ToList();
        }

        private static string Download(string url)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.UserAgent = "KoaZaFootballAnalytics/1.0 (RSS reader)";
            request.Timeout = 10000; request.ReadWriteTimeout = 10000;
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            using (var response = (HttpWebResponse)request.GetResponse())
            using (var stream = response.GetResponseStream())
            using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                return reader.ReadToEnd();
        }

        private static List<RssHeadline> Parse(string xml, string source)
        {
            var doc = XDocument.Parse(xml);
            XNamespace atom = "http://www.w3.org/2005/Atom";
            var entries = doc.Descendants("item").Select(item => new RssHeadline {
                Source = source, Title = Clean((string)item.Element("title")),
                Link = Clean((string)item.Element("link")), Summary = Clean((string)item.Element("description")),
                PublishedUtc = ParseDate((string)item.Element("pubDate"))
            }).ToList();
            if (entries.Count == 0)
                entries = doc.Descendants(atom + "entry").Select(item => new RssHeadline {
                    Source = source, Title = Clean((string)item.Element(atom + "title")),
                    Link = (string)item.Elements(atom + "link").Select(l => (string)l.Attribute("href")).FirstOrDefault(),
                    Summary = Clean((string)item.Element(atom + "summary")),
                    PublishedUtc = ParseDate((string)item.Element(atom + "updated"))
                }).ToList();
            return entries.Where(x => !string.IsNullOrWhiteSpace(x.Title)).ToList();
        }
        private static DateTime? ParseDate(string value) { DateTime d; return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out d) ? (DateTime?)d : null; }
        private static string Clean(string value) { if (string.IsNullOrWhiteSpace(value)) return ""; return System.Text.RegularExpressions.Regex.Replace(System.Net.WebUtility.HtmlDecode(value), "<[^>]+>", " ").Trim(); }
        private static string UriSafeName(string value) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").Substring(0, 16); }
    }
}