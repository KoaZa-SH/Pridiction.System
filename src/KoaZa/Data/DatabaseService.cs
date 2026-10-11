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
                using (var cmd = new SQLiteCommand("DELETE FROM Fixtures WHERE Id NOT IN (SELECT MIN(Id) FROM Fixtures GROUP BY Provider, ExternalId);", con)) cmd.ExecuteNonQuery();
                using (var cmd = new SQLiteCommand("CREATE UNIQUE INDEX IF NOT EXISTS UX_Fixtures_ProviderExternal ON Fixtures(Provider, ExternalId);", con)) cmd.ExecuteNonQuery();
                using (var cmd = new SQLiteCommand("CREATE TABLE IF NOT EXISTS FixtureHistory (Id INTEGER PRIMARY KEY AUTOINCREMENT, Provider TEXT NOT NULL, League TEXT NOT NULL, KickoffUtc TEXT, HomeTeam TEXT NOT NULL, AwayTeam TEXT NOT NULL, HomeGoals INTEGER, AwayGoals INTEGER, Status TEXT, ExternalId TEXT, UpdatedUtc TEXT NOT NULL, ArchivedUtc TEXT NOT NULL, HomeCorners INTEGER NULL, AwayCorners INTEGER NULL, HomeShotsOnTarget INTEGER NULL, AwayShotsOnTarget INTEGER NULL, HomeYellowCards INTEGER NULL, AwayYellowCards INTEGER NULL, Referee TEXT NULL, VarReviews INTEGER NULL, UNIQUE(Provider, ExternalId, HomeGoals, AwayGoals, Status, KickoffUtc));", con)) cmd.ExecuteNonQuery();
                EnsureColumnExists(con, "Predictions", "MarketsJson", "TEXT NULL");
                foreach (string column in new[] { "HomeCorners", "AwayCorners", "HomeShotsOnTarget", "AwayShotsOnTarget", "HomeYellowCards", "AwayYellowCards", "Referee", "VarReviews" })
                {
                    string type = column == "Referee" ? "TEXT NULL" : "INTEGER NULL";
                    EnsureColumnExists(con, "Fixtures", column, type);
                    EnsureColumnExists(con, "FixtureHistory", column, type);
                }
                using (var cmd = new SQLiteCommand("CREATE TABLE IF NOT EXISTS MatchStatistics (Id INTEGER PRIMARY KEY AUTOINCREMENT, Provider TEXT NOT NULL, ExternalId TEXT NOT NULL, League TEXT, KickoffUtc TEXT, HomeTeam TEXT NOT NULL, AwayTeam TEXT NOT NULL, HomeGoals INTEGER, AwayGoals INTEGER, Referee TEXT, HomeShots INTEGER, AwayShots INTEGER, HomeShotsOnTarget INTEGER, AwayShotsOnTarget INTEGER, HomeCorners INTEGER, AwayCorners INTEGER, HomeYellowCards INTEGER, AwayYellowCards INTEGER, HomeRedCards INTEGER, AwayRedCards INTEGER, HomeFouls INTEGER, AwayFouls INTEGER, HomePossession REAL, AwayPossession REAL, DataSource TEXT, Status TEXT, UpdatedUtc TEXT NOT NULL, UNIQUE(Provider, ExternalId));", con)) cmd.ExecuteNonQuery();
                EnsureColumnExists(con, "MatchStatistics", "Status", "TEXT NULL");
                using (var cmd = new SQLiteCommand("CREATE TABLE IF NOT EXISTS MatchEvents (Id INTEGER PRIMARY KEY AUTOINCREMENT, Provider TEXT NOT NULL, ExternalId TEXT NOT NULL, EventIndex INTEGER NOT NULL, Minute INTEGER, Team TEXT, Player TEXT, Assist TEXT, Type TEXT, Detail TEXT, UNIQUE(Provider, ExternalId, EventIndex));", con)) cmd.ExecuteNonQuery();
                using (var cmd = new SQLiteCommand("CREATE TABLE IF NOT EXISTS MarketPredictions (Id INTEGER PRIMARY KEY AUTOINCREMENT, RunId TEXT NOT NULL, CreatedUtc TEXT NOT NULL, Provider TEXT, ExternalId TEXT, League TEXT, KickoffUtc TEXT, HomeTeam TEXT NOT NULL, AwayTeam TEXT NOT NULL, Market TEXT NOT NULL, Outcome TEXT NOT NULL, Probability REAL, SampleSize INTEGER NOT NULL, HasSufficientData INTEGER NOT NULL, Explanation TEXT, ModelVersion TEXT NOT NULL, UNIQUE(RunId, Market, Outcome));", con)) cmd.ExecuteNonQuery();
                using (var cmd = new SQLiteCommand("CREATE INDEX IF NOT EXISTS IX_MatchStatistics_TeamsKickoff ON MatchStatistics(HomeTeam, AwayTeam, KickoffUtc);", con)) cmd.ExecuteNonQuery();
                using (var cmd = new SQLiteCommand("CREATE INDEX IF NOT EXISTS IX_MatchStatistics_RefereeKickoff ON MatchStatistics(Referee, KickoffUtc);", con)) cmd.ExecuteNonQuery();
                using (var cmd = new SQLiteCommand("CREATE INDEX IF NOT EXISTS IX_MarketPredictions_FixtureCreated ON MarketPredictions(HomeTeam, AwayTeam, CreatedUtc);", con)) cmd.ExecuteNonQuery();
                using (var cmd = new SQLiteCommand("INSERT INTO MatchStatistics (Provider,ExternalId,League,KickoffUtc,HomeTeam,AwayTeam,HomeGoals,AwayGoals,Status,Referee,HomeShotsOnTarget,AwayShotsOnTarget,HomeCorners,AwayCorners,HomeYellowCards,AwayYellowCards,DataSource,UpdatedUtc) SELECT Provider,ExternalId,League,KickoffUtc,HomeTeam,AwayTeam,HomeGoals,AwayGoals,Status,Referee,HomeShotsOnTarget,AwayShotsOnTarget,HomeCorners,AwayCorners,HomeYellowCards,AwayYellowCards,Provider,UpdatedUtc FROM Fixtures WHERE ExternalId IS NOT NULL AND TRIM(ExternalId)<>'' AND (HomeShotsOnTarget IS NOT NULL OR AwayShotsOnTarget IS NOT NULL OR HomeCorners IS NOT NULL OR AwayCorners IS NOT NULL OR HomeYellowCards IS NOT NULL OR AwayYellowCards IS NOT NULL OR (Referee IS NOT NULL AND TRIM(Referee)<>'')) AND (UPPER(Status) IN ('FT','AET','PEN','FINISHED','MATCH FINISHED','FULL TIME','AFTER EXTRA TIME','AFTER PENALTIES') OR Status IS NULL OR Status='') AND 1=1 ON CONFLICT(Provider,ExternalId) DO UPDATE SET HomeShotsOnTarget=COALESCE(MatchStatistics.HomeShotsOnTarget,excluded.HomeShotsOnTarget),AwayShotsOnTarget=COALESCE(MatchStatistics.AwayShotsOnTarget,excluded.AwayShotsOnTarget),HomeCorners=COALESCE(MatchStatistics.HomeCorners,excluded.HomeCorners),AwayCorners=COALESCE(MatchStatistics.AwayCorners,excluded.AwayCorners),HomeYellowCards=COALESCE(MatchStatistics.HomeYellowCards,excluded.HomeYellowCards),AwayYellowCards=COALESCE(MatchStatistics.AwayYellowCards,excluded.AwayYellowCards),Referee=COALESCE(MatchStatistics.Referee,excluded.Referee);", con)) cmd.ExecuteNonQuery();
                using (var cmd = new SQLiteCommand("INSERT INTO MatchStatistics (Provider,ExternalId,League,KickoffUtc,HomeTeam,AwayTeam,HomeGoals,AwayGoals,Status,Referee,HomeShotsOnTarget,AwayShotsOnTarget,HomeCorners,AwayCorners,HomeYellowCards,AwayYellowCards,DataSource,UpdatedUtc) SELECT Provider,ExternalId,League,KickoffUtc,HomeTeam,AwayTeam,HomeGoals,AwayGoals,Status,Referee,HomeShotsOnTarget,AwayShotsOnTarget,HomeCorners,AwayCorners,HomeYellowCards,AwayYellowCards,Provider,UpdatedUtc FROM FixtureHistory WHERE ExternalId IS NOT NULL AND TRIM(ExternalId)<>'' AND (HomeShotsOnTarget IS NOT NULL OR AwayShotsOnTarget IS NOT NULL OR HomeCorners IS NOT NULL OR AwayCorners IS NOT NULL OR HomeYellowCards IS NOT NULL OR AwayYellowCards IS NOT NULL OR (Referee IS NOT NULL AND TRIM(Referee)<>'')) AND (UPPER(Status) IN ('FT','AET','PEN','FINISHED','MATCH FINISHED','FULL TIME','AFTER EXTRA TIME','AFTER PENALTIES') OR Status IS NULL OR Status='') AND 1=1 ON CONFLICT(Provider,ExternalId) DO UPDATE SET HomeShotsOnTarget=COALESCE(MatchStatistics.HomeShotsOnTarget,excluded.HomeShotsOnTarget),AwayShotsOnTarget=COALESCE(MatchStatistics.AwayShotsOnTarget,excluded.AwayShotsOnTarget),HomeCorners=COALESCE(MatchStatistics.HomeCorners,excluded.HomeCorners),AwayCorners=COALESCE(MatchStatistics.AwayCorners,excluded.AwayCorners),HomeYellowCards=COALESCE(MatchStatistics.HomeYellowCards,excluded.HomeYellowCards),AwayYellowCards=COALESCE(MatchStatistics.AwayYellowCards,excluded.AwayYellowCards),Referee=COALESCE(MatchStatistics.Referee,excluded.Referee);", con)) cmd.ExecuteNonQuery();
                using (var tx = con.BeginTransaction())
                {
                    using (var cmd = new SQLiteCommand("INSERT OR IGNORE INTO FixtureHistory (Provider,League,KickoffUtc,HomeTeam,AwayTeam,HomeGoals,AwayGoals,Status,ExternalId,UpdatedUtc,ArchivedUtc,HomeCorners,AwayCorners,HomeShotsOnTarget,AwayShotsOnTarget,HomeYellowCards,AwayYellowCards,Referee,VarReviews) SELECT Provider,League,KickoffUtc,HomeTeam,AwayTeam,HomeGoals,AwayGoals,Status,ExternalId,UpdatedUtc,@archived,HomeCorners,AwayCorners,HomeShotsOnTarget,AwayShotsOnTarget,HomeYellowCards,AwayYellowCards,Referee,VarReviews FROM Fixtures WHERE HomeGoals IS NOT NULL AND AwayGoals IS NOT NULL;", con, tx))
                    { cmd.Parameters.AddWithValue("@archived", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)); cmd.ExecuteNonQuery(); }
                    using (var cmd = new SQLiteCommand("UPDATE Fixtures SET ExternalId=lower(hex(randomblob(16))) WHERE ExternalId IS NULL OR TRIM(ExternalId)='';", con, tx)) cmd.ExecuteNonQuery();
                    using (var cmd = new SQLiteCommand("DELETE FROM Fixtures WHERE Id NOT IN (SELECT MAX(Id) FROM Fixtures GROUP BY Provider,ExternalId);", con, tx)) cmd.ExecuteNonQuery();
                    using (var cmd = new SQLiteCommand("CREATE UNIQUE INDEX IF NOT EXISTS UX_Fixtures_Provider_ExternalId ON Fixtures(Provider, ExternalId);", con, tx)) cmd.ExecuteNonQuery();
                    tx.Commit();
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

        public void SaveMatchStatistics(MatchStatisticsRecord s)
        {
            if (s == null || string.IsNullOrWhiteSpace(s.Provider) || string.IsNullOrWhiteSpace(s.ExternalId)) return;
            using (var con = new SQLiteConnection(_connectionString))
            {
                con.Open();
                const string sql = "INSERT INTO MatchStatistics (Provider,ExternalId,League,KickoffUtc,HomeTeam,AwayTeam,HomeGoals,AwayGoals,Status,Referee,HomeShots,AwayShots,HomeShotsOnTarget,AwayShotsOnTarget,HomeCorners,AwayCorners,HomeYellowCards,AwayYellowCards,HomeRedCards,AwayRedCards,HomeFouls,AwayFouls,HomePossession,AwayPossession,DataSource,UpdatedUtc) VALUES (@p,@id,@l,@k,@h,@a,@hg,@ag,@status,@r,@hs,@as,@hot,@aot,@hc,@ac,@hy,@ay,@hr,@ar,@hf,@af,@hp,@ap,@src,@u) ON CONFLICT(Provider,ExternalId) DO UPDATE SET League=COALESCE(excluded.League,MatchStatistics.League),KickoffUtc=COALESCE(excluded.KickoffUtc,MatchStatistics.KickoffUtc),HomeTeam=COALESCE(NULLIF(excluded.HomeTeam,''),MatchStatistics.HomeTeam),AwayTeam=COALESCE(NULLIF(excluded.AwayTeam,''),MatchStatistics.AwayTeam),HomeGoals=COALESCE(excluded.HomeGoals,MatchStatistics.HomeGoals),AwayGoals=COALESCE(excluded.AwayGoals,MatchStatistics.AwayGoals),Status=COALESCE(excluded.Status,MatchStatistics.Status),Referee=COALESCE(excluded.Referee,MatchStatistics.Referee),HomeShots=COALESCE(excluded.HomeShots,MatchStatistics.HomeShots),AwayShots=COALESCE(excluded.AwayShots,MatchStatistics.AwayShots),HomeShotsOnTarget=COALESCE(excluded.HomeShotsOnTarget,MatchStatistics.HomeShotsOnTarget),AwayShotsOnTarget=COALESCE(excluded.AwayShotsOnTarget,MatchStatistics.AwayShotsOnTarget),HomeCorners=COALESCE(excluded.HomeCorners,MatchStatistics.HomeCorners),AwayCorners=COALESCE(excluded.AwayCorners,MatchStatistics.AwayCorners),HomeYellowCards=COALESCE(excluded.HomeYellowCards,MatchStatistics.HomeYellowCards),AwayYellowCards=COALESCE(excluded.AwayYellowCards,MatchStatistics.AwayYellowCards),HomeRedCards=COALESCE(excluded.HomeRedCards,MatchStatistics.HomeRedCards),AwayRedCards=COALESCE(excluded.AwayRedCards,MatchStatistics.AwayRedCards),HomeFouls=COALESCE(excluded.HomeFouls,MatchStatistics.HomeFouls),AwayFouls=COALESCE(excluded.AwayFouls,MatchStatistics.AwayFouls),HomePossession=COALESCE(excluded.HomePossession,MatchStatistics.HomePossession),AwayPossession=COALESCE(excluded.AwayPossession,MatchStatistics.AwayPossession),DataSource=COALESCE(excluded.DataSource,MatchStatistics.DataSource),UpdatedUtc=excluded.UpdatedUtc;";
                using (var cmd = new SQLiteCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@p", s.Provider); cmd.Parameters.AddWithValue("@id", s.ExternalId);
                    cmd.Parameters.AddWithValue("@l", (object)s.League ?? DBNull.Value); cmd.Parameters.AddWithValue("@k", (object)s.KickoffUtc ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@h", s.HomeTeam ?? ""); cmd.Parameters.AddWithValue("@a", s.AwayTeam ?? "");
                    AddNullable(cmd,"@hg",s.HomeGoals); AddNullable(cmd,"@ag",s.AwayGoals); cmd.Parameters.AddWithValue("@status",string.IsNullOrWhiteSpace(s.Status) ? (object)DBNull.Value : s.Status); cmd.Parameters.AddWithValue("@r",string.IsNullOrWhiteSpace(s.Referee) ? (object)DBNull.Value : s.Referee);
                    AddNullable(cmd,"@hs",s.HomeShots); AddNullable(cmd,"@as",s.AwayShots); AddNullable(cmd,"@hot",s.HomeShotsOnTarget); AddNullable(cmd,"@aot",s.AwayShotsOnTarget);
                    AddNullable(cmd,"@hc",s.HomeCorners); AddNullable(cmd,"@ac",s.AwayCorners); AddNullable(cmd,"@hy",s.HomeYellowCards); AddNullable(cmd,"@ay",s.AwayYellowCards);
                    AddNullable(cmd,"@hr",s.HomeRedCards); AddNullable(cmd,"@ar",s.AwayRedCards); AddNullable(cmd,"@hf",s.HomeFouls); AddNullable(cmd,"@af",s.AwayFouls);
                    AddNullable(cmd,"@hp",s.HomePossession); AddNullable(cmd,"@ap",s.AwayPossession);
                    cmd.Parameters.AddWithValue("@src",(object)s.DataSource ?? s.Provider); cmd.Parameters.AddWithValue("@u",DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture));
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public List<MatchStatisticsRecord> GetHistoricalMatchStatistics(string league, string homeTeam, string awayTeam, int limit)
        {
            var rows = new List<MatchStatisticsRecord>();
            using (var con = new SQLiteConnection(_connectionString))
            {
                con.Open();
                string sql = "SELECT * FROM MatchStatistics WHERE KickoffUtc < @before AND (UPPER(HomeTeam)=UPPER(@home) OR UPPER(AwayTeam)=UPPER(@home) OR UPPER(HomeTeam)=UPPER(@away) OR UPPER(AwayTeam)=UPPER(@away)) ";
                if (!string.IsNullOrWhiteSpace(league)) sql += "AND UPPER(League)=UPPER(@league) ";
                sql += "ORDER BY KickoffUtc DESC LIMIT @limit;";
                using (var cmd = new SQLiteCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@before", DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture));
                    cmd.Parameters.AddWithValue("@home", homeTeam ?? ""); cmd.Parameters.AddWithValue("@away", awayTeam ?? "");
                    if (!string.IsNullOrWhiteSpace(league)) cmd.Parameters.AddWithValue("@league", league);
                    cmd.Parameters.AddWithValue("@limit", Math.Max(1,limit) * 3);
                    using (var rd = cmd.ExecuteReader()) while (rd.Read()) rows.Add(ReadStatistics(rd));
                }
            }
            return DeduplicateStatistics(rows).Take(Math.Max(1,limit)).ToList();
        }

        public void SaveMatchEvents(IEnumerable<MatchEventRecord> events)
        {
            var rows = (events ?? Enumerable.Empty<MatchEventRecord>()).ToList();
            if (rows.Count == 0) return;
            using (var con = new SQLiteConnection(_connectionString))
            {
                con.Open();
                using (var tx = con.BeginTransaction())
                {
                    foreach (var e in rows)
                    {
                        using (var cmd = new SQLiteCommand("INSERT OR REPLACE INTO MatchEvents (Provider,ExternalId,EventIndex,Minute,Team,Player,Assist,Type,Detail) VALUES (@p,@id,@i,@m,@t,@player,@assist,@type,@detail);", con, tx))
                        {
                            cmd.Parameters.AddWithValue("@p", e.Provider ?? "");
                            cmd.Parameters.AddWithValue("@id", e.ExternalId ?? "");
                            cmd.Parameters.AddWithValue("@i", e.EventIndex);
                            cmd.Parameters.AddWithValue("@m", e.Minute.HasValue ? (object)e.Minute.Value : DBNull.Value);
                            cmd.Parameters.AddWithValue("@t", (object)e.Team ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@player", (object)e.Player ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@assist", (object)e.Assist ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@type", (object)e.Type ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@detail", (object)e.Detail ?? DBNull.Value);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    tx.Commit();
                }
            }
        }

        public void ReplaceMatchEvents(string provider, string externalId, IEnumerable<MatchEventRecord> events)
        {
            if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(externalId)) return;
            var rows = (events ?? Enumerable.Empty<MatchEventRecord>()).ToList();
            using (var con = new SQLiteConnection(_connectionString))
            {
                con.Open();
                using (var tx = con.BeginTransaction())
                {
                    using (var delete = new SQLiteCommand("DELETE FROM MatchEvents WHERE Provider=@p AND ExternalId=@id;", con, tx))
                    {
                        delete.Parameters.AddWithValue("@p", provider); delete.Parameters.AddWithValue("@id", externalId);
                        delete.ExecuteNonQuery();
                    }
                    foreach (var e in rows)
                    {
                        using (var cmd = new SQLiteCommand("INSERT INTO MatchEvents (Provider,ExternalId,EventIndex,Minute,Team,Player,Assist,Type,Detail) VALUES (@p,@id,@i,@m,@t,@player,@assist,@type,@detail);", con, tx))
                        {
                            cmd.Parameters.AddWithValue("@p", provider); cmd.Parameters.AddWithValue("@id", externalId);
                            cmd.Parameters.AddWithValue("@i", e.EventIndex);
                            cmd.Parameters.AddWithValue("@m", e.Minute.HasValue ? (object)e.Minute.Value : DBNull.Value);
                            cmd.Parameters.AddWithValue("@t", (object)e.Team ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@player", (object)e.Player ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@assist", (object)e.Assist ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@type", (object)e.Type ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@detail", (object)e.Detail ?? DBNull.Value);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    tx.Commit();
                }
            }
        }

        public List<MatchStatisticsRecord> GetRecentMatchStatistics(string league, int limit)
        {
            var rows = new List<MatchStatisticsRecord>();
            using (var con = new SQLiteConnection(_connectionString))
            {
                con.Open();
                string sql = "SELECT * FROM MatchStatistics WHERE KickoffUtc IS NOT NULL AND KickoffUtc<>'' AND KickoffUtc < @before AND HomeGoals IS NOT NULL AND AwayGoals IS NOT NULL AND (UPPER(Status) IN ('FT','AET','PEN','FINISHED','MATCH FINISHED','FULL TIME','AFTER EXTRA TIME','AFTER PENALTIES') OR Status IS NULL OR Status='') AND (HomeShots IS NOT NULL OR AwayShots IS NOT NULL OR HomeShotsOnTarget IS NOT NULL OR AwayShotsOnTarget IS NOT NULL OR HomeCorners IS NOT NULL OR AwayCorners IS NOT NULL OR HomeYellowCards IS NOT NULL OR AwayYellowCards IS NOT NULL OR (Referee IS NOT NULL AND TRIM(Referee)<>'')) ";
                if (!string.IsNullOrWhiteSpace(league)) sql += "AND UPPER(League)=UPPER(@league) ";
                sql += "ORDER BY KickoffUtc DESC LIMIT @limit;";
                using (var cmd = new SQLiteCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@before", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
                    if (!string.IsNullOrWhiteSpace(league)) cmd.Parameters.AddWithValue("@league", league);
                    cmd.Parameters.AddWithValue("@limit", Math.Max(1, limit) * 3);
                    using (var rd = cmd.ExecuteReader()) while (rd.Read()) rows.Add(ReadStatistics(rd));
                }
            }
            return DeduplicateStatistics(rows).Take(Math.Max(1, limit)).ToList();
        }

        private static List<MatchStatisticsRecord> DeduplicateStatistics(IEnumerable<MatchStatisticsRecord> source)
        {
            return (source ?? Enumerable.Empty<MatchStatisticsRecord>())
                .GroupBy(s => NormalizeTeam(s.HomeTeam) + "|" + NormalizeTeam(s.AwayTeam) + "|" +
                    NormalizeDate(s.KickoffUtc), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(StatisticsCompleteness).ThenByDescending(s => s.UpdatedUtc).First())
                .OrderByDescending(s => s.KickoffUtc).ToList();
        }

        private static int StatisticsCompleteness(MatchStatisticsRecord s)
        {
            int count = 0;
            if (s.HomeGoals.HasValue) count++; if (s.AwayGoals.HasValue) count++;
            if (!string.IsNullOrWhiteSpace(s.Referee)) count++;
            if (s.HomeShots.HasValue) count++; if (s.AwayShots.HasValue) count++;
            if (s.HomeShotsOnTarget.HasValue) count++; if (s.AwayShotsOnTarget.HasValue) count++;
            if (s.HomeCorners.HasValue) count++; if (s.AwayCorners.HasValue) count++;
            if (s.HomeYellowCards.HasValue) count++; if (s.AwayYellowCards.HasValue) count++;
            if (s.HomeRedCards.HasValue) count++; if (s.AwayRedCards.HasValue) count++;
            if (s.HomeFouls.HasValue) count++; if (s.AwayFouls.HasValue) count++;
            return count;
        }

        private static string NormalizeTeam(string value)
        {
            return (value ?? "").Trim().ToUpperInvariant();
        }

        private static string NormalizeDate(string value)
        {
            DateTime parsed;
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out parsed)
                ? parsed.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture) : (value ?? "").Trim();
        }

        private static MatchStatisticsRecord ReadStatistics(SQLiteDataReader r)
        {
            return new MatchStatisticsRecord {
                Provider=Convert.ToString(r["Provider"]), ExternalId=Convert.ToString(r["ExternalId"]), League=Convert.ToString(r["League"]),
                KickoffUtc=Convert.ToString(r["KickoffUtc"]), Status=Convert.ToString(r["Status"]), HomeTeam=Convert.ToString(r["HomeTeam"]), AwayTeam=Convert.ToString(r["AwayTeam"]),
                HomeGoals=NullableInt(r["HomeGoals"]), AwayGoals=NullableInt(r["AwayGoals"]), Referee=Convert.ToString(r["Referee"]),
                HomeShots=NullableInt(r["HomeShots"]), AwayShots=NullableInt(r["AwayShots"]), HomeShotsOnTarget=NullableInt(r["HomeShotsOnTarget"]),
                AwayShotsOnTarget=NullableInt(r["AwayShotsOnTarget"]), HomeCorners=NullableInt(r["HomeCorners"]), AwayCorners=NullableInt(r["AwayCorners"]),
                HomeYellowCards=NullableInt(r["HomeYellowCards"]), AwayYellowCards=NullableInt(r["AwayYellowCards"]), HomeRedCards=NullableInt(r["HomeRedCards"]),
                AwayRedCards=NullableInt(r["AwayRedCards"]), HomeFouls=NullableInt(r["HomeFouls"]), AwayFouls=NullableInt(r["AwayFouls"]),
                HomePossession=NullableDouble(r["HomePossession"]), AwayPossession=NullableDouble(r["AwayPossession"]), DataSource=Convert.ToString(r["DataSource"]),
                UpdatedUtc=Convert.ToString(r["UpdatedUtc"])
            };
        }

        private static int? NullableInt(object v) { return v == DBNull.Value ? (int?)null : Convert.ToInt32(v); }
        private static double? NullableDouble(object v) { return v == DBNull.Value ? (double?)null : Convert.ToDouble(v); }
        private static void AddNullable(SQLiteCommand cmd, string name, int? value) { cmd.Parameters.AddWithValue(name, value.HasValue ? (object)value.Value : DBNull.Value); }
        private static void AddNullable(SQLiteCommand cmd, string name, double? value) { cmd.Parameters.AddWithValue(name, value.HasValue ? (object)value.Value : DBNull.Value); }

        public void SaveMarketPredictions(FixtureRecord fixture, MultiMarketPrediction prediction)
        {
            if (fixture == null || prediction == null || prediction.Markets == null || prediction.Markets.Count == 0) return;
            string runId = Guid.NewGuid().ToString("N");
            string created = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
            using (var con = new SQLiteConnection(_connectionString))
            {
                con.Open();
                using (var tx = con.BeginTransaction())
                {
                    foreach (var m in prediction.Markets)
                    {
                        using (var cmd = new SQLiteCommand("INSERT OR REPLACE INTO MarketPredictions (RunId,CreatedUtc,Provider,ExternalId,League,KickoffUtc,HomeTeam,AwayTeam,Market,Outcome,Probability,SampleSize,HasSufficientData,Explanation,ModelVersion) VALUES (@run,@created,@provider,@id,@league,@kickoff,@home,@away,@market,@outcome,@prob,@samples,@enough,@explanation,@version);", con, tx))
                        {
                            cmd.Parameters.AddWithValue("@run", runId); cmd.Parameters.AddWithValue("@created", created);
                            cmd.Parameters.AddWithValue("@provider", (object)fixture.Provider ?? DBNull.Value); cmd.Parameters.AddWithValue("@id", (object)fixture.ExternalId ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@league", (object)fixture.League ?? DBNull.Value); cmd.Parameters.AddWithValue("@kickoff", (object)fixture.KickoffUtc ?? DBNull.Value);
                            cmd.Parameters.AddWithValue("@home", fixture.HomeTeam ?? ""); cmd.Parameters.AddWithValue("@away", fixture.AwayTeam ?? "");
                            cmd.Parameters.AddWithValue("@market", m.Market ?? ""); cmd.Parameters.AddWithValue("@outcome", m.Outcome ?? "");
                            cmd.Parameters.AddWithValue("@prob", m.HasSufficientData ? (object)m.Probability : DBNull.Value);
                            cmd.Parameters.AddWithValue("@samples", m.SampleSize); cmd.Parameters.AddWithValue("@enough", m.HasSufficientData ? 1 : 0);
                            cmd.Parameters.AddWithValue("@explanation", m.Explanation ?? ""); cmd.Parameters.AddWithValue("@version", "multi-market-historical-0.1");
                            cmd.ExecuteNonQuery();
                        }
                    }
                    tx.Commit();
                }
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
                        using (var insert = new SQLiteCommand("INSERT INTO Fixtures (Provider, League, KickoffUtc, HomeTeam, AwayTeam, HomeGoals, AwayGoals, Status, ExternalId, UpdatedUtc, HomeCorners, AwayCorners, HomeShotsOnTarget, AwayShotsOnTarget, HomeYellowCards, AwayYellowCards, Referee, VarReviews) SELECT @provider,@league,@kickoff,@home,@away,@hg,@ag,@status,@external,@updated,@hc,@ac,@hs,@as,@hy,@ay,@referee,@var WHERE NOT EXISTS (SELECT 1 FROM Fixtures WHERE Provider=@provider AND ExternalId=@external);", con, transaction))
                        {
                            AddFixtureParameters(insert, f, provider, external, now);
                            insert.ExecuteNonQuery();
                        }
                        using (var update = new SQLiteCommand("UPDATE Fixtures SET League=CASE WHEN @league<>'' THEN @league ELSE League END, KickoffUtc=CASE WHEN @kickoff<>'' THEN @kickoff ELSE KickoffUtc END, HomeTeam=@home, AwayTeam=@away, HomeGoals=COALESCE(@hg,HomeGoals), AwayGoals=COALESCE(@ag,AwayGoals), Status=CASE WHEN HomeGoals IS NOT NULL AND AwayGoals IS NOT NULL AND UPPER(Status) IN ('FT','AET','PEN','FINISHED','MATCH FINISHED','FULL TIME','AFTER EXTRA TIME','AFTER PENALTIES') THEN Status WHEN @status<>'' THEN @status ELSE Status END, UpdatedUtc=@updated, HomeCorners=COALESCE(@hc,HomeCorners), AwayCorners=COALESCE(@ac,AwayCorners), HomeShotsOnTarget=COALESCE(@hs,HomeShotsOnTarget), AwayShotsOnTarget=COALESCE(@as,AwayShotsOnTarget), HomeYellowCards=COALESCE(@hy,HomeYellowCards), AwayYellowCards=COALESCE(@ay,AwayYellowCards), Referee=CASE WHEN @referee<>'' THEN @referee ELSE Referee END, VarReviews=COALESCE(@var,VarReviews) WHERE Provider=@provider AND ExternalId=@external;", con, transaction))
                        {
                            AddFixtureParameters(update, f, provider, external, now);
                            update.ExecuteNonQuery();
                        }
                        if (!string.IsNullOrWhiteSpace(f.RawJson))
                        {
                            using (var raw = new SQLiteCommand("INSERT INTO FixtureDetails (Provider,ExternalId,RawJson,UpdatedUtc) VALUES (@provider,@external,@json,@updated) ON CONFLICT(Provider,ExternalId) DO UPDATE SET RawJson=excluded.RawJson,UpdatedUtc=excluded.UpdatedUtc WHERE instr(FixtureDetails.RawJson, '"detailsVersion"') = 0 OR instr(excluded.RawJson, '"detailsVersion"') > 0;", con, transaction))
                            {
                                raw.Parameters.AddWithValue("@provider", provider);
                                raw.Parameters.AddWithValue("@external", external);
                                raw.Parameters.AddWithValue("@json", f.RawJson);
                                raw.Parameters.AddWithValue("@updated", now);
                                raw.ExecuteNonQuery();
                            }
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
                .ToList();
        }

        private static string FixtureIdentity(FixtureRecord fixture)
        {
            string home = NormalizeTeamKey(fixture.HomeTeam);
            string away = NormalizeTeamKey(fixture.AwayTeam);
            DateTime kickoff;
            bool hasKickoff = DateTime.TryParse(fixture.KickoffUtc, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out kickoff);
            if (home.Length == 0 || away.Length == 0 || home == "home" || away == "away" || string.IsNullOrWhiteSpace(fixture.KickoffUtc))
                return home + "|" + away + "|" + (fixture.League ?? "").Trim().ToLowerInvariant() + "|" + fixture.Provider + "|" + fixture.ExternalId;
            string timeKey = hasKickoff ? kickoff.ToString("yyyy-MM-dd-HH", CultureInfo.InvariantCulture) : fixture.KickoffUtc.Trim().ToLowerInvariant();
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