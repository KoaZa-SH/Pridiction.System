using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using KoaZa.Services;

namespace KoaZa.Data
{
    public sealed class TeamGoalStats
    {
        public string Team { get; set; }
        public int Matches { get; set; }
        public double ScoredPerMatch { get; set; }
        public double ConcededPerMatch { get; set; }
        public double HomeScoredPerMatch { get; set; }
        public double HomeConcededPerMatch { get; set; }
        public double AwayScoredPerMatch { get; set; }
        public double AwayConcededPerMatch { get; set; }
    }

    public sealed class DatabaseService
    {
        private readonly string _connectionString;
        private readonly string _databasePath;
        private static readonly string UserDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KoaZa");
        private static readonly string BackupFolder = Path.Combine(UserDataFolder, "Backups");
        public static string LastBackupError { get; private set; }

        public DatabaseService()
        {
            Directory.CreateDirectory(UserDataFolder);
            _databasePath = Path.Combine(UserDataFolder, "koaza.db");
            string legacyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "koaza.db");
            if (!File.Exists(_databasePath) && File.Exists(legacyPath))
            {
                try { File.Copy(legacyPath, _databasePath, false); }
                catch (Exception ex) { throw new IOException("The existing database could not be migrated from the previous application folder. The original file was left untouched.", ex); }
            }
            _connectionString = "Data Source=" + _databasePath + ";Version=3;";
            if (!File.Exists(_databasePath)) SQLiteConnection.CreateFile(_databasePath);
            Initialize();
            try { CreateAutomaticBackupIfDue(); LastBackupError = null; }
            catch (Exception ex) { LastBackupError = ex.Message; }
        }

        public string CreateBackupNow()
        {
            Directory.CreateDirectory(BackupFolder);
            string backupPath = Path.Combine(BackupFolder, "koaza_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".db");
            using (var source = new SQLiteConnection(_connectionString))
            using (var destination = new SQLiteConnection("Data Source=" + backupPath + ";Version=3;"))
            {
                source.Open();
                destination.Open();
                source.BackupDatabase(destination, "main", "main", -1, null, 0);
            }
            PruneBackups(30);
            LastBackupError = null;
            return backupPath;
        }

        private void CreateAutomaticBackupIfDue()
        {
            Directory.CreateDirectory(BackupFolder);
            bool backedUpToday = Directory.GetFiles(BackupFolder, "koaza_*.db")
                .Select(path => File.GetLastWriteTime(path).Date).Any(date => date == DateTime.Today);
            if (!backedUpToday) CreateBackupNow();
        }

        private static void PruneBackups(int keep)
        {
            foreach (string file in Directory.GetFiles(BackupFolder, "koaza_*.db")
                .OrderByDescending(File.GetLastWriteTime).Skip(keep))
            {
                try { File.Delete(file); } catch { }
            }
        }

        private void Initialize()
        {
            using (var con = new SQLiteConnection(_connectionString))
            {
                con.Open();
                using (var cmd = new SQLiteCommand("CREATE TABLE IF NOT EXISTS Predictions (Id INTEGER PRIMARY KEY AUTOINCREMENT, CreatedUtc TEXT NOT NULL, HomeTeam TEXT NOT NULL, AwayTeam TEXT NOT NULL, HomeGoalRate REAL NOT NULL, AwayGoalRate REAL NOT NULL, HomeWin REAL NOT NULL, DrawProbability REAL NOT NULL, AwayWin REAL NOT NULL, Over25 REAL NOT NULL, Under25 REAL NOT NULL, BothTeamsScore REAL NOT NULL, ModelVersion TEXT NOT NULL);", con)) cmd.ExecuteNonQuery();
                using (var cmd = new SQLiteCommand("CREATE TABLE IF NOT EXISTS Fixtures (Id INTEGER PRIMARY KEY AUTOINCREMENT, Provider TEXT NOT NULL, League TEXT NOT NULL, KickoffUtc TEXT, HomeTeam TEXT NOT NULL, AwayTeam TEXT NOT NULL, HomeGoals INTEGER NULL, AwayGoals INTEGER NULL, Status TEXT, ExternalId TEXT, UpdatedUtc TEXT NOT NULL);", con)) cmd.ExecuteNonQuery();
                using (var cmd = new SQLiteCommand("CREATE TABLE IF NOT EXISTS FixtureDetails (Id INTEGER PRIMARY KEY AUTOINCREMENT, Provider TEXT NOT NULL, ExternalId TEXT NOT NULL, RawJson TEXT NOT NULL, UpdatedUtc TEXT NOT NULL, UNIQUE(Provider, ExternalId));", con)) cmd.ExecuteNonQuery();
                using (var cmd = new SQLiteCommand("CREATE TABLE IF NOT EXISTS FixtureHistory (Id INTEGER PRIMARY KEY AUTOINCREMENT, Provider TEXT NOT NULL, League TEXT NOT NULL, KickoffUtc TEXT, HomeTeam TEXT NOT NULL, AwayTeam TEXT NOT NULL, HomeGoals INTEGER, AwayGoals INTEGER, Status TEXT, ExternalId TEXT, UpdatedUtc TEXT NOT NULL, ArchivedUtc TEXT NOT NULL, UNIQUE(Provider, ExternalId, HomeGoals, AwayGoals, Status, KickoffUtc));", con)) cmd.ExecuteNonQuery();
                using (var cmd = new SQLiteCommand("CREATE UNIQUE INDEX IF NOT EXISTS UX_Fixtures_Provider_ExternalId ON Fixtures(Provider, ExternalId);", con)) cmd.ExecuteNonQuery();
                using (var cmd = new SQLiteCommand("CREATE INDEX IF NOT EXISTS IX_Fixtures_Kickoff ON Fixtures(KickoffUtc);", con)) cmd.ExecuteNonQuery();
            }
        }

        public void SavePrediction(string home, string away, double homeRate, double awayRate, MatchProbabilities p)
        {
            using (var con = new SQLiteConnection(_connectionString))
            {
                con.Open();
                using (var cmd = new SQLiteCommand("INSERT INTO Predictions (CreatedUtc, HomeTeam, AwayTeam, HomeGoalRate, AwayGoalRate, HomeWin, DrawProbability, AwayWin, Over25, Under25, BothTeamsScore, ModelVersion) VALUES (@date,@home,@away,@hr,@ar,@hw,@draw,@aw,@over,@under,@btts,@version);", con))
                {
                    cmd.Parameters.AddWithValue("@date", DateTime.UtcNow.ToString("o"));
                    cmd.Parameters.AddWithValue("@home", home);
                    cmd.Parameters.AddWithValue("@away", away);
                    cmd.Parameters.AddWithValue("@hr", homeRate);
                    cmd.Parameters.AddWithValue("@ar", awayRate);
                    cmd.Parameters.AddWithValue("@hw", p.HomeWin);
                    cmd.Parameters.AddWithValue("@draw", p.Draw);
                    cmd.Parameters.AddWithValue("@aw", p.AwayWin);
                    cmd.Parameters.AddWithValue("@over", p.Over25);
                    cmd.Parameters.AddWithValue("@under", p.Under25);
                    cmd.Parameters.AddWithValue("@btts", p.BothTeamsScore);
                    cmd.Parameters.AddWithValue("@version", "historical-poisson-0.2");
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public void SaveFixtures(List<FixtureRecord> fixtures)
        {
            if (fixtures == null || fixtures.Count == 0) return;
            try { CreateBackupNow(); } catch { /* A backup failure must not silently destroy a successful data refresh. */ }
            using (var con = new SQLiteConnection(_connectionString))
            {
                con.Open();
                using (var transaction = con.BeginTransaction())
                {
                    foreach (var f in fixtures)
                    {
                        // Before refreshing an existing fixture, preserve any completed result in an
                        // append-only archive. The unique key prevents repeated imports from duplicating it.
                        using (var archive = new SQLiteCommand("INSERT OR IGNORE INTO FixtureHistory (Provider, League, KickoffUtc, HomeTeam, AwayTeam, HomeGoals, AwayGoals, Status, ExternalId, UpdatedUtc, ArchivedUtc) SELECT Provider, League, KickoffUtc, HomeTeam, AwayTeam, HomeGoals, AwayGoals, Status, ExternalId, UpdatedUtc, @archived FROM Fixtures WHERE Provider=@provider AND ExternalId=@external AND HomeGoals IS NOT NULL AND AwayGoals IS NOT NULL;", con, transaction))
                        {
                            archive.Parameters.AddWithValue("@archived", DateTime.UtcNow.ToString("o"));
                            archive.Parameters.AddWithValue("@provider", f.Provider ?? "");
                            archive.Parameters.AddWithValue("@external", string.IsNullOrEmpty(f.ExternalId) ? Guid.Empty.ToString("N") : f.ExternalId);
                            archive.ExecuteNonQuery();
                        }
                        using (var cmd = new SQLiteCommand("INSERT OR REPLACE INTO Fixtures (Provider, League, KickoffUtc, HomeTeam, AwayTeam, HomeGoals, AwayGoals, Status, ExternalId, UpdatedUtc) VALUES (@provider,@league,@kickoff,@home,@away,@hg,@ag,@status,@external,@updated);", con, transaction))
                        {
                            cmd.Parameters.AddWithValue("@provider", f.Provider ?? "");
                            cmd.Parameters.AddWithValue("@league", f.League ?? "");
                            cmd.Parameters.AddWithValue("@kickoff", f.KickoffUtc ?? "");
                            cmd.Parameters.AddWithValue("@home", f.HomeTeam ?? "");
                            cmd.Parameters.AddWithValue("@away", f.AwayTeam ?? "");
                            cmd.Parameters.AddWithValue("@hg", f.HomeGoals.HasValue ? (object)f.HomeGoals.Value : DBNull.Value);
                            cmd.Parameters.AddWithValue("@ag", f.AwayGoals.HasValue ? (object)f.AwayGoals.Value : DBNull.Value);
                            cmd.Parameters.AddWithValue("@status", f.Status ?? "");
                            cmd.Parameters.AddWithValue("@external", string.IsNullOrEmpty(f.ExternalId) ? Guid.NewGuid().ToString("N") : f.ExternalId);
                            cmd.Parameters.AddWithValue("@updated", DateTime.UtcNow.ToString("o"));
                            cmd.ExecuteNonQuery();
                        }
                    }
                    transaction.Commit();
                }
            }
        }

        public void SaveFixtureDetails(string provider, string externalId, string rawJson)
        {
            if (string.IsNullOrWhiteSpace(externalId) || string.IsNullOrWhiteSpace(rawJson)) return;
            using (var con = new SQLiteConnection(_connectionString))
            {
                con.Open();
                using (var cmd = new SQLiteCommand("INSERT OR REPLACE INTO FixtureDetails (Provider, ExternalId, RawJson, UpdatedUtc) VALUES (@provider,@external,@json,@updated);", con))
                {
                    cmd.Parameters.AddWithValue("@provider", provider ?? "");
                    cmd.Parameters.AddWithValue("@external", externalId);
                    cmd.Parameters.AddWithValue("@json", rawJson);
                    cmd.Parameters.AddWithValue("@updated", DateTime.UtcNow.ToString("o"));
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public string GetFixtureDetails(string provider, string externalId)
        {
            using (var con = new SQLiteConnection(_connectionString))
            {
                con.Open();
                using (var cmd = new SQLiteCommand("SELECT RawJson FROM FixtureDetails WHERE Provider=@provider AND ExternalId=@external LIMIT 1;", con))
                {
                    cmd.Parameters.AddWithValue("@provider", provider ?? "");
                    cmd.Parameters.AddWithValue("@external", externalId ?? "");
                    object value = cmd.ExecuteScalar();
                    return value == null || value == DBNull.Value ? null : Convert.ToString(value);
                }
            }
        }

        public List<FixtureRecord> GetUpcomingFixtures(DateTime fromLocal, DateTime toLocal)
        {
            var all = new List<FixtureRecord>();
            using (var con = new SQLiteConnection(_connectionString))
            {
                con.Open();
                using (var cmd = new SQLiteCommand("SELECT Provider, League, KickoffUtc, HomeTeam, AwayTeam, HomeGoals, AwayGoals, Status, ExternalId FROM Fixtures WHERE KickoffUtc IS NOT NULL AND KickoffUtc <> '' ORDER BY KickoffUtc;", con))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        DateTime kickoff;
                        string raw = Convert.ToString(reader["KickoffUtc"]);
                        if (!DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out kickoff))
                            continue;
                        string status = Convert.ToString(reader["Status"]);
                        bool live = IsLiveStatus(status);
                        if (IsCompletedStatus(status) || (!live && (reader["HomeGoals"] != DBNull.Value || reader["AwayGoals"] != DBNull.Value))) continue;

                        // Upcoming fixtures are limited to the requested local date range, but live matches
                        // remain visible even if their kickoff was yesterday and the local date has changed.
                        DateTime local = kickoff.ToLocalTime();
                        if (!live && (local < fromLocal || local >= toLocal)) continue;
                        all.Add(new FixtureRecord
                        {
                            Provider = Convert.ToString(reader["Provider"]), League = Convert.ToString(reader["League"]),
                            KickoffUtc = raw, HomeTeam = Convert.ToString(reader["HomeTeam"]), AwayTeam = Convert.ToString(reader["AwayTeam"]),
                            Status = status, ExternalId = Convert.ToString(reader["ExternalId"])
                        });
                    }
                }
            }
            return all;
        }

        public List<FixtureRecord> GetCompletedMatches(string league, string team, int limit)
        {
            var matches = new List<FixtureRecord>();
            using (var con = new SQLiteConnection(_connectionString))
            {
                con.Open();
                string sql = "SELECT Provider, League, KickoffUtc, HomeTeam, AwayTeam, HomeGoals, AwayGoals, Status, ExternalId FROM Fixtures WHERE HomeGoals IS NOT NULL AND AwayGoals IS NOT NULL AND (UPPER(Status) IN ('FT','AET','PEN','FINISHED','MATCH FINISHED','FULL TIME','AFTER EXTRA TIME','AFTER PENALTIES') OR Status IS NULL OR Status='') ";
                if (!string.IsNullOrWhiteSpace(league)) sql += "AND UPPER(League)=UPPER(@league) ";
                sql += "AND (UPPER(HomeTeam)=UPPER(@team) OR UPPER(AwayTeam)=UPPER(@team)) ORDER BY KickoffUtc DESC LIMIT @limit;";
                using (var cmd = new SQLiteCommand(sql, con))
                {
                    if (!string.IsNullOrWhiteSpace(league)) cmd.Parameters.AddWithValue("@league", league);
                    cmd.Parameters.AddWithValue("@team", team ?? "");
                    cmd.Parameters.AddWithValue("@limit", limit);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            matches.Add(new FixtureRecord
                            {
                                Provider = Convert.ToString(reader["Provider"]), League = Convert.ToString(reader["League"]),
                                KickoffUtc = Convert.ToString(reader["KickoffUtc"]), HomeTeam = Convert.ToString(reader["HomeTeam"]),
                                AwayTeam = Convert.ToString(reader["AwayTeam"]), HomeGoals = Convert.ToInt32(reader["HomeGoals"]),
                                AwayGoals = Convert.ToInt32(reader["AwayGoals"]), Status = Convert.ToString(reader["Status"]),
                                ExternalId = Convert.ToString(reader["ExternalId"])
                            });
                        }
                    }
                }
            }
            return matches;
        }


        public List<FixtureRecord> GetRecentCompletedMatches(string league, int limit)
        {
            var matches = new List<FixtureRecord>();
            using (var con = new SQLiteConnection(_connectionString))
            {
                con.Open();
                string sql = "SELECT Provider, League, KickoffUtc, HomeTeam, AwayTeam, HomeGoals, AwayGoals, Status, ExternalId FROM Fixtures WHERE HomeGoals IS NOT NULL AND AwayGoals IS NOT NULL AND (UPPER(Status) IN ('FT','AET','PEN','FINISHED','MATCH FINISHED','FULL TIME','AFTER EXTRA TIME','AFTER PENALTIES') OR Status IS NULL OR Status='') ";
                if (!string.IsNullOrWhiteSpace(league)) sql += "AND UPPER(League)=UPPER(@league) ";
                sql += "ORDER BY KickoffUtc DESC LIMIT @limit;";
                using (var cmd = new SQLiteCommand(sql, con))
                {
                    if (!string.IsNullOrWhiteSpace(league)) cmd.Parameters.AddWithValue("@league", league);
                    cmd.Parameters.AddWithValue("@limit", limit);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            matches.Add(new FixtureRecord
                            {
                                Provider = Convert.ToString(reader["Provider"]), League = Convert.ToString(reader["League"]),
                                KickoffUtc = Convert.ToString(reader["KickoffUtc"]), HomeTeam = Convert.ToString(reader["HomeTeam"]),
                                AwayTeam = Convert.ToString(reader["AwayTeam"]), HomeGoals = Convert.ToInt32(reader["HomeGoals"]),
                                AwayGoals = Convert.ToInt32(reader["AwayGoals"]), Status = Convert.ToString(reader["Status"]),
                                ExternalId = Convert.ToString(reader["ExternalId"])
                            });
                        }
                    }
                }
            }
            return matches;
        }

        public List<string> GetKnownLeagues()
        {
            var leagues = new List<string>();
            using (var con = new SQLiteConnection(_connectionString))
            {
                con.Open();
                using (var cmd = new SQLiteCommand("SELECT DISTINCT League FROM Fixtures WHERE League IS NOT NULL AND TRIM(League)<>'' ORDER BY League;", con))
                using (var reader = cmd.ExecuteReader()) while (reader.Read()) leagues.Add(Convert.ToString(reader[0]));
            }
            return leagues;
        }

        private static bool IsCompletedStatus(string status)
        {
            string s = (status ?? "").Trim().ToUpperInvariant();
            return s == "FT" || s == "AET" || s == "PEN" || s == "FINISHED" || s == "MATCH FINISHED" || s == "FULL TIME" || s == "AFTER EXTRA TIME" || s == "AFTER PENALTIES";
        }

        private static bool IsLiveStatus(string status)
        {
            string s = (status ?? "").Trim().ToUpperInvariant();
            return s == "LIVE" || s == "1H" || s == "HT" || s == "2H" || s == "ET" || s == "P" ||
                   s == "BT" || s == "INT" || s == "INPLAY" || s == "IN PROGRESS" || s == "INPROGRESS" ||
                   s == "FIRST HALF" || s == "SECOND HALF" || s == "HALFTIME" || s == "EXTRA TIME";
        }
    }
}