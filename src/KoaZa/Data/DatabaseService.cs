using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;
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

    public sealed class CompetitionCatalogEntry
    {
        public string League { get; set; }
        public string Providers { get; set; }
        public int FixtureCount { get; set; }
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
                using (var cmd = new SQLiteCommand("CREATE TABLE IF NOT EXISTS Predictions (Id INTEGER PRIMARY KEY AUTOINCREMENT, CreatedUtc TEXT NOT NULL, HomeTeam TEXT NOT NULL, AwayTeam TEXT NOT NULL, HomeGoalRate REAL NOT NULL, AwayGoalRate REAL NOT NULL, HomeWin REAL NOT NULL, DrawProbability REAL NOT NULL, AwayWin REAL NOT NULL, Over25 REAL NOT NULL, Under25 REAL NOT NULL, BothTeamsScore REAL NOT NULL, ModelVersion TEXT NOT NULL, MarketsJson TEXT NULL);", con)) cmd.ExecuteNonQuery();
                using (var cmd = new SQLiteCommand("CREATE TABLE IF NOT EXISTS Fixtures (Id INTEGER PRIMARY KEY AUTOINCREMENT, Provider TEXT NOT NULL, League TEXT NOT NULL, KickoffUtc TEXT, HomeTeam TEXT NOT NULL, AwayTeam TEXT NOT NULL, HomeGoals INTEGER NULL, AwayGoals INTEGER NULL, Status TEXT, ExternalId TEXT, UpdatedUtc TEXT NOT NULL, HomeCorners INTEGER NULL, AwayCorners INTEGER NULL, HomeShotsOnTarget INTEGER NULL, AwayShotsOnTarget INTEGER NULL, HomeYellowCards INTEGER NULL, AwayYellowCards INTEGER NULL, Referee TEXT NULL, VarReviews INTEGER NULL);", con)) cmd.ExecuteNonQuery();
                using (var cmd = new SQLiteCommand("CREATE TABLE IF NOT EXISTS FixtureDetails (Id INTEGER PRIMARY KEY AUTOINCREMENT, Provider TEXT NOT NULL, ExternalId TEXT NOT NULL, RawJson TEXT NOT NULL, UpdatedUtc TEXT NOT NULL, UNIQUE(Provider, ExternalId));", con)) cmd.ExecuteNonQuery();
                using (var cmd = new SQLiteCommand("CREATE TABLE IF NOT EXISTS FixtureHistory (Id INTEGER PRIMARY KEY AUTOINCREMENT, Provider TEXT NOT NULL, League TEXT NOT NULL, KickoffUtc TEXT, HomeTeam TEXT NOT NULL, AwayTeam TEXT NOT NULL, HomeGoals INTEGER, AwayGoals INTEGER, Status TEXT, ExternalId TEXT, UpdatedUtc TEXT NOT NULL, ArchivedUtc TEXT NOT NULL, HomeCorners INTEGER NULL, AwayCorners INTEGER NULL, HomeShotsOnTarget INTEGER NULL, AwayShotsOnTarget INTEGER NULL, HomeYellowCards INTEGER NULL, AwayYellowCards INTEGER NULL, Referee TEXT NULL, VarReviews INTEGER NULL, UNIQUE(Provider, ExternalId, HomeGoals, AwayGoals, Status, KickoffUtc));", con)) cmd.ExecuteNonQuery();
                EnsureColumnExists(con, "Predictions", "MarketsJson", "TEXT NULL");
                foreach (string column in new[] { "HomeCorners", "AwayCorners", "HomeShotsOnTarget", "AwayShotsOnTarget", "HomeYellowCards", "AwayYellowCards", "Referee", "VarReviews" })
                {
                    string type = column == "Referee" ? "TEXT NULL" : "INTEGER NULL";
                    EnsureColumnExists(con, "Fixtures", column, type);
                    EnsureColumnExists(con, "FixtureHistory", column, type);
                }
                using (var cmd = new SQLiteCommand("CREATE UNIQUE INDEX IF NOT EXISTS UX_Fixtures_Provider_ExternalId ON Fixtures(Provider, ExternalId);", con)) cmd.ExecuteNonQuery();
                using (var cmd = new SQLiteCommand("CREATE INDEX IF NOT EXISTS IX_Fixtures_Kickoff ON Fixtures(KickoffUtc);", con)) cmd.ExecuteNonQuery();
                using (var cmd = new SQLiteCommand("CREATE INDEX IF NOT EXISTS IX_Fixtures_League_Kickoff ON Fixtures(League, KickoffUtc);", con)) cmd.ExecuteNonQuery();
                using (var cmd = new SQLiteCommand("CREATE INDEX IF NOT EXISTS IX_Fixtures_HomeTeam_Kickoff ON Fixtures(HomeTeam, KickoffUtc);", con)) cmd.ExecuteNonQuery();
                using (var cmd = new SQLiteCommand("CREATE INDEX IF NOT EXISTS IX_Fixtures_AwayTeam_Kickoff ON Fixtures(AwayTeam, KickoffUtc);", con)) cmd.ExecuteNonQuery();
            }
        }

        private static void EnsureColumnExists(SQLiteConnection con, string table, string column, string type)
        {
            bool exists = false;
            using (var cmd = new SQLiteCommand("PRAGMA table_info(" + table + ");", con))
            using (var reader = cmd.ExecuteReader())
                while (reader.Read())
                    if (string.Equals(Convert.ToString(reader["name"]), column, StringComparison.OrdinalIgnoreCase)) { exists = true; break; }
            if (!exists)
            {
                using (var alter = new SQLiteCommand("ALTER TABLE " + table + " ADD COLUMN " + column + " " + type + ";", con))
                    alter.ExecuteNonQuery();
            }
        }

        public void SavePrediction(string home, string away, double homeRate, double awayRate, MatchProbabilities p)
        {
            using (var con = new SQLiteConnection(_connectionString))
            {
                con.Open();
                using (var cmd = new SQLiteCommand("INSERT INTO Predictions (CreatedUtc, HomeTeam, AwayTeam, HomeGoalRate, AwayGoalRate, HomeWin, DrawProbability, AwayWin, Over25, Under25, BothTeamsScore, ModelVersion, MarketsJson) VALUES (@date,@home,@away,@hr,@ar,@hw,@draw,@aw,@over,@under,@btts,@version,@markets);", con))
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
                    cmd.Parameters.AddWithValue("@version", "multi-factor-historical-0.3");
                    cmd.Parameters.AddWithValue("@markets", new JavaScriptSerializer().Serialize(p.Markets ?? new List<MarketProbability>()));
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
                        if (f == null || string.IsNullOrWhiteSpace(f.HomeTeam) || string.IsNullOrWhiteSpace(f.AwayTeam)) continue;
                        string provider = f.Provider ?? "";
                        string external = string.IsNullOrWhiteSpace(f.ExternalId) ? CreateStableFixtureId(provider, f) : f.ExternalId.Trim();
                        f.ExternalId = external;
                        string now = DateTime.UtcNow.ToString("o");
                        using (var archive = new SQLiteCommand("INSERT OR IGNORE INTO FixtureHistory (Provider, League, KickoffUtc, HomeTeam, AwayTeam, HomeGoals, AwayGoals, Status, ExternalId, UpdatedUtc, ArchivedUtc, HomeCorners, AwayCorners, HomeShotsOnTarget, AwayShotsOnTarget, HomeYellowCards, AwayYellowCards, Referee, VarReviews) SELECT Provider, League, KickoffUtc, HomeTeam, AwayTeam, HomeGoals, AwayGoals, Status, ExternalId, UpdatedUtc, @archived, HomeCorners, AwayCorners, HomeShotsOnTarget, AwayShotsOnTarget, HomeYellowCards, AwayYellowCards, Referee, VarReviews FROM Fixtures WHERE Provider=@provider AND ExternalId=@external AND HomeGoals IS NOT NULL AND AwayGoals IS NOT NULL AND UPPER(Status) IN ('FT','AET','PEN','FINISHED','MATCH FINISHED','FULL TIME','AFTER EXTRA TIME','AFTER PENALTIES');", con, transaction))
                        {
                            archive.Parameters.AddWithValue("@archived", now);
                            archive.Parameters.AddWithValue("@provider", provider);
                            archive.Parameters.AddWithValue("@external", external);
                            archive.ExecuteNonQuery();
                        }
                        using (var insert = new SQLiteCommand("INSERT OR IGNORE INTO Fixtures (Provider, League, KickoffUtc, HomeTeam, AwayTeam, HomeGoals, AwayGoals, Status, ExternalId, UpdatedUtc, HomeCorners, AwayCorners, HomeShotsOnTarget, AwayShotsOnTarget, HomeYellowCards, AwayYellowCards, Referee, VarReviews) VALUES (@provider,@league,@kickoff,@home,@away,@hg,@ag,@status,@external,@updated,@hc,@ac,@hs,@as,@hy,@ay,@referee,@var);", con, transaction))
                        {
                            AddFixtureParameters(insert, f, provider, external, now);
                            insert.ExecuteNonQuery();
                        }
                        using (var update = new SQLiteCommand("UPDATE Fixtures SET League=CASE WHEN @league<>'' THEN @league ELSE League END, KickoffUtc=CASE WHEN @kickoff<>'' THEN @kickoff ELSE KickoffUtc END, HomeTeam=@home, AwayTeam=@away, HomeGoals=COALESCE(@hg,HomeGoals), AwayGoals=COALESCE(@ag,AwayGoals), Status=CASE WHEN HomeGoals IS NOT NULL AND AwayGoals IS NOT NULL AND UPPER(Status) IN ('FT','AET','PEN','FINISHED','MATCH FINISHED','FULL TIME','AFTER EXTRA TIME','AFTER PENALTIES') THEN Status WHEN @status<>'' THEN @status ELSE Status END, UpdatedUtc=@updated, HomeCorners=COALESCE(@hc,HomeCorners), AwayCorners=COALESCE(@ac,AwayCorners), HomeShotsOnTarget=COALESCE(@hs,HomeShotsOnTarget), AwayShotsOnTarget=COALESCE(@as,AwayShotsOnTarget), HomeYellowCards=COALESCE(@hy,HomeYellowCards), AwayYellowCards=COALESCE(@ay,AwayYellowCards), Referee=CASE WHEN @referee<>'' THEN @referee ELSE Referee END, VarReviews=COALESCE(@var,VarReviews) WHERE Provider=@provider AND ExternalId=@external;", con, transaction))
                        {
                            AddFixtureParameters(update, f, provider, external, now);
                            update.ExecuteNonQuery();
                        }
                    }
                    transaction.Commit();
                }
            }
        }

        private static string CreateStableFixtureId(string provider, FixtureRecord fixture)
        {
            string identity = (provider ?? "") + "|" + (fixture.League ?? "") + "|" + (fixture.KickoffUtc ?? "") + "|" +
                (fixture.HomeTeam ?? "").Trim().ToUpperInvariant() + "|" + (fixture.AwayTeam ?? "").Trim().ToUpperInvariant();
            using (var sha = SHA256.Create())
                return "generated-" + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(identity))).Replace("-", "");
        }

        private static void AddFixtureParameters(SQLiteCommand cmd, FixtureRecord f, string provider, string external, string now)
        {
            cmd.Parameters.AddWithValue("@provider", provider);
            cmd.Parameters.AddWithValue("@league", f.League ?? "");
            cmd.Parameters.AddWithValue("@kickoff", f.KickoffUtc ?? "");
            cmd.Parameters.AddWithValue("@home", f.HomeTeam ?? "");
            cmd.Parameters.AddWithValue("@away", f.AwayTeam ?? "");
            cmd.Parameters.AddWithValue("@hg", f.HomeGoals.HasValue ? (object)f.HomeGoals.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@ag", f.AwayGoals.HasValue ? (object)f.AwayGoals.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@status", f.Status ?? "");
            cmd.Parameters.AddWithValue("@external", external);
            cmd.Parameters.AddWithValue("@updated", now);
            cmd.Parameters.AddWithValue("@hc", f.HomeCorners.HasValue ? (object)f.HomeCorners.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@ac", f.AwayCorners.HasValue ? (object)f.AwayCorners.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@hs", f.HomeShotsOnTarget.HasValue ? (object)f.HomeShotsOnTarget.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@as", f.AwayShotsOnTarget.HasValue ? (object)f.AwayShotsOnTarget.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@hy", f.HomeYellowCards.HasValue ? (object)f.HomeYellowCards.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@ay", f.AwayYellowCards.HasValue ? (object)f.AwayYellowCards.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("@referee", f.Referee ?? "");
            cmd.Parameters.AddWithValue("@var", f.VarReviews.HasValue ? (object)f.VarReviews.Value : DBNull.Value);
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

        public void SaveNormalizedFixtureStatistics(FixtureRecord stats)
        {
            if (stats == null || string.IsNullOrWhiteSpace(stats.Provider) || string.IsNullOrWhiteSpace(stats.ExternalId)) return;
            using (var con = new SQLiteConnection(_connectionString))
            {
                con.Open();
                using (var cmd = new SQLiteCommand("UPDATE Fixtures SET HomeCorners=COALESCE(@hc,HomeCorners), AwayCorners=COALESCE(@ac,AwayCorners), HomeShotsOnTarget=COALESCE(@hs,HomeShotsOnTarget), AwayShotsOnTarget=COALESCE(@as,AwayShotsOnTarget), HomeYellowCards=COALESCE(@hy,HomeYellowCards), AwayYellowCards=COALESCE(@ay,AwayYellowCards), Referee=CASE WHEN @referee<>'' THEN @referee ELSE Referee END, VarReviews=COALESCE(@var,VarReviews), UpdatedUtc=@updated WHERE Provider=@provider AND ExternalId=@external;", con))
                {
                    cmd.Parameters.AddWithValue("@hc", stats.HomeCorners.HasValue ? (object)stats.HomeCorners.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@ac", stats.AwayCorners.HasValue ? (object)stats.AwayCorners.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@hs", stats.HomeShotsOnTarget.HasValue ? (object)stats.HomeShotsOnTarget.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@as", stats.AwayShotsOnTarget.HasValue ? (object)stats.AwayShotsOnTarget.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@hy", stats.HomeYellowCards.HasValue ? (object)stats.HomeYellowCards.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@ay", stats.AwayYellowCards.HasValue ? (object)stats.AwayYellowCards.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@referee", stats.Referee ?? "");
                    cmd.Parameters.AddWithValue("@var", stats.VarReviews.HasValue ? (object)stats.VarReviews.Value : DBNull.Value);
                    cmd.Parameters.AddWithValue("@updated", DateTime.UtcNow.ToString("o"));
                    cmd.Parameters.AddWithValue("@provider", stats.Provider);
                    cmd.Parameters.AddWithValue("@external", stats.ExternalId);
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
                using (var cmd = new SQLiteCommand("SELECT Provider, League, KickoffUtc, HomeTeam, AwayTeam, HomeGoals, AwayGoals, Status, ExternalId, HomeCorners, AwayCorners, HomeShotsOnTarget, AwayShotsOnTarget, HomeYellowCards, AwayYellowCards, Referee, VarReviews FROM Fixtures WHERE KickoffUtc IS NOT NULL AND KickoffUtc <> '' ORDER BY KickoffUtc;", con))
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
                            HomeGoals = reader["HomeGoals"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["HomeGoals"]),
                            AwayGoals = reader["AwayGoals"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["AwayGoals"]),
                            Status = status, ExternalId = Convert.ToString(reader["ExternalId"]),
                            HomeCorners = reader["HomeCorners"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["HomeCorners"]),
                            AwayCorners = reader["AwayCorners"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["AwayCorners"]),
                            HomeShotsOnTarget = reader["HomeShotsOnTarget"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["HomeShotsOnTarget"]),
                            AwayShotsOnTarget = reader["AwayShotsOnTarget"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["AwayShotsOnTarget"]),
                            HomeYellowCards = reader["HomeYellowCards"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["HomeYellowCards"]),
                            AwayYellowCards = reader["AwayYellowCards"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["AwayYellowCards"]),
                            Referee = reader["Referee"] == DBNull.Value ? "" : Convert.ToString(reader["Referee"]),
                            VarReviews = reader["VarReviews"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["VarReviews"]),
                        });
                    }
                }
            }
            return DeduplicateFixtures(all);
        }

        public List<FixtureRecord> GetCompletedMatches(string league, string team, int limit)
        {
            var matches = new List<FixtureRecord>();
            using (var con = new SQLiteConnection(_connectionString))
            {
                con.Open();
                string sql = "SELECT Provider, League, KickoffUtc, HomeTeam, AwayTeam, HomeGoals, AwayGoals, Status, ExternalId, HomeCorners, AwayCorners, HomeShotsOnTarget, AwayShotsOnTarget, HomeYellowCards, AwayYellowCards, Referee, VarReviews FROM Fixtures WHERE HomeGoals IS NOT NULL AND AwayGoals IS NOT NULL AND (UPPER(Status) IN ('FT','AET','PEN','FINISHED','MATCH FINISHED','FULL TIME','AFTER EXTRA TIME','AFTER PENALTIES') OR Status IS NULL OR Status='') ";
                if (!string.IsNullOrWhiteSpace(league)) sql += "AND UPPER(League)=UPPER(@league) ";
                sql += "AND (UPPER(HomeTeam)=UPPER(@team) OR UPPER(AwayTeam)=UPPER(@team)) ORDER BY KickoffUtc DESC LIMIT @fetchLimit;";
                using (var cmd = new SQLiteCommand(sql, con))
                {
                    if (!string.IsNullOrWhiteSpace(league)) cmd.Parameters.AddWithValue("@league", league);
                    cmd.Parameters.AddWithValue("@team", team ?? "");
                    cmd.Parameters.AddWithValue("@fetchLimit", Math.Max(limit * 5, limit));
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
                                ExternalId = Convert.ToString(reader["ExternalId"]),
                                HomeCorners = reader["HomeCorners"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["HomeCorners"]),
                            AwayCorners = reader["AwayCorners"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["AwayCorners"]),
                            HomeShotsOnTarget = reader["HomeShotsOnTarget"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["HomeShotsOnTarget"]),
                            AwayShotsOnTarget = reader["AwayShotsOnTarget"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["AwayShotsOnTarget"]),
                            HomeYellowCards = reader["HomeYellowCards"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["HomeYellowCards"]),
                            AwayYellowCards = reader["AwayYellowCards"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["AwayYellowCards"]),
                            Referee = reader["Referee"] == DBNull.Value ? "" : Convert.ToString(reader["Referee"]),
                            VarReviews = reader["VarReviews"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["VarReviews"]),
                            });
                        }
                    }
                }
            }
            return DeduplicateFixtures(matches).Take(limit).ToList();
        }


        public List<FixtureRecord> GetRecentCompletedMatches(string league, int limit)
        {
            var matches = new List<FixtureRecord>();
            using (var con = new SQLiteConnection(_connectionString))
            {
                con.Open();
                string sql = "SELECT Provider, League, KickoffUtc, HomeTeam, AwayTeam, HomeGoals, AwayGoals, Status, ExternalId, HomeCorners, AwayCorners, HomeShotsOnTarget, AwayShotsOnTarget, HomeYellowCards, AwayYellowCards, Referee, VarReviews FROM Fixtures WHERE HomeGoals IS NOT NULL AND AwayGoals IS NOT NULL AND (UPPER(Status) IN ('FT','AET','PEN','FINISHED','MATCH FINISHED','FULL TIME','AFTER EXTRA TIME','AFTER PENALTIES') OR Status IS NULL OR Status='') ";
                if (!string.IsNullOrWhiteSpace(league)) sql += "AND UPPER(League)=UPPER(@league) ";
                sql += "ORDER BY KickoffUtc DESC LIMIT @fetchLimit;";
                using (var cmd = new SQLiteCommand(sql, con))
                {
                    if (!string.IsNullOrWhiteSpace(league)) cmd.Parameters.AddWithValue("@league", league);
                    cmd.Parameters.AddWithValue("@fetchLimit", Math.Max(limit * 5, limit));
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
                                ExternalId = Convert.ToString(reader["ExternalId"]),
                                HomeCorners = reader["HomeCorners"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["HomeCorners"]),
                            AwayCorners = reader["AwayCorners"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["AwayCorners"]),
                            HomeShotsOnTarget = reader["HomeShotsOnTarget"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["HomeShotsOnTarget"]),
                            AwayShotsOnTarget = reader["AwayShotsOnTarget"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["AwayShotsOnTarget"]),
                            HomeYellowCards = reader["HomeYellowCards"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["HomeYellowCards"]),
                            AwayYellowCards = reader["AwayYellowCards"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["AwayYellowCards"]),
                            Referee = reader["Referee"] == DBNull.Value ? "" : Convert.ToString(reader["Referee"]),
                            VarReviews = reader["VarReviews"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["VarReviews"]),
                            });
                        }
                    }
                }
            }
            return DeduplicateFixtures(matches).Take(limit).ToList();
        }

        private static List<FixtureRecord> DeduplicateFixtures(IEnumerable<FixtureRecord> fixtures)
        {
            return (fixtures ?? Enumerable.Empty<FixtureRecord>())
                .Where(f => f != null)
                .GroupBy(FixtureIdentity, StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var ordered = group.OrderByDescending(FixtureCompleteness).ToList();
                    var best = ordered[0];
                    foreach (var other in ordered.Skip(1))
                    {
                        if (!best.HomeGoals.HasValue) best.HomeGoals = other.HomeGoals;
                        if (!best.AwayGoals.HasValue) best.AwayGoals = other.AwayGoals;
                        if (!best.HomeCorners.HasValue) best.HomeCorners = other.HomeCorners;
                        if (!best.AwayCorners.HasValue) best.AwayCorners = other.AwayCorners;
                        if (!best.HomeShotsOnTarget.HasValue) best.HomeShotsOnTarget = other.HomeShotsOnTarget;
                        if (!best.AwayShotsOnTarget.HasValue) best.AwayShotsOnTarget = other.AwayShotsOnTarget;
                        if (!best.HomeYellowCards.HasValue) best.HomeYellowCards = other.HomeYellowCards;
                        if (!best.AwayYellowCards.HasValue) best.AwayYellowCards = other.AwayYellowCards;
                        if (!best.VarReviews.HasValue) best.VarReviews = other.VarReviews;
                        if (string.IsNullOrWhiteSpace(best.Referee)) best.Referee = other.Referee;
                        if (string.IsNullOrWhiteSpace(best.Status)) best.Status = other.Status;
                    }
                    return best;
                })
                .OrderBy(f => f.KickoffUtc)
                .ToList();
        }

        private static string FixtureIdentity(FixtureRecord fixture)
        {
            string home = NormalizeTeamKey(fixture.HomeTeam);
            string away = NormalizeTeamKey(fixture.AwayTeam);
            DateTime kickoff;
            string timeKey = DateTime.TryParse(fixture.KickoffUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out kickoff)
                ? kickoff.ToString("yyyy-MM-dd-HH", CultureInfo.InvariantCulture)
                : (fixture.KickoffUtc ?? "").Trim().ToLowerInvariant();
            return home + "|" + away + "|" + timeKey;
        }

        private static string NormalizeTeamKey(string name)
        {
            string value = (name ?? "").ToLowerInvariant();
            value = System.Text.RegularExpressions.Regex.Replace(value, "[^a-z0-9]+", " ");
            var ignored = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "fc", "cf", "afc", "sc", "club", "football", "the" };
            var parts = value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Where(p => !ignored.Contains(p)).ToList();
            value = string.Join(" ", parts);
            if (value == "man utd" || value == "manchester utd") value = "manchester united";
            if (value == "man city") value = "manchester city";
            if (value == "wolves") value = "wolverhampton wanderers";
            if (value == "inter") value = "inter milan";
            if (value == "psg") value = "paris saint germain";
            return value;
        }

        private static int FixtureCompleteness(FixtureRecord f)
        {
            int score = 0;
            if (f.HomeGoals.HasValue) score += 10;
            if (f.AwayGoals.HasValue) score += 10;
            if (f.HomeCorners.HasValue) score += 2;
            if (f.AwayCorners.HasValue) score += 2;
            if (f.HomeShotsOnTarget.HasValue) score += 2;
            if (f.AwayShotsOnTarget.HasValue) score += 2;
            if (f.HomeYellowCards.HasValue) score += 2;
            if (f.AwayYellowCards.HasValue) score += 2;
            if (f.VarReviews.HasValue) score += 1;
            if (!string.IsNullOrWhiteSpace(f.Referee)) score += 1;
            if (!string.IsNullOrWhiteSpace(f.Status)) score += 1;
            return score;
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

        public List<CompetitionCatalogEntry> GetKnownCompetitionCatalog()
        {
            var entries = new List<CompetitionCatalogEntry>();
            using (var con = new SQLiteConnection(_connectionString))
            {
                con.Open();
                const string sql = "SELECT League, GROUP_CONCAT(DISTINCT Provider), COUNT(*) FROM Fixtures WHERE League IS NOT NULL AND TRIM(League)<>'' GROUP BY League ORDER BY League;";
                using (var cmd = new SQLiteCommand(sql, con))
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        entries.Add(new CompetitionCatalogEntry
                        {
                            League = Convert.ToString(reader[0]),
                            Providers = reader.IsDBNull(1) ? "Unknown" : Convert.ToString(reader[1]),
                            FixtureCount = Convert.ToInt32(reader[2], CultureInfo.InvariantCulture)
                        });
                    }
                }
            }
            return entries;
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
                   s == "BT" || s == "INT" || s == "INPLAY" || s == "IN_PLAY" || s == "IN PROGRESS" || s == "INPROGRESS" ||
                   s == "FIRST HALF" || s == "SECOND HALF" || s == "FIRST_HALF" || s == "SECOND_HALF" ||
                   s == "HALFTIME" || s == "EXTRA TIME" || s == "EXTRA_TIME" || s == "PAUSED" || s == "PENALTY_SHOOTOUT";
        }
    }
}