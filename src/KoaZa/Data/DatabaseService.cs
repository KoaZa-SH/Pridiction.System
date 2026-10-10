using System;
using System.IO;
using System.Data.SQLite;
namespace KoaZa.Data
{
    public sealed class DatabaseService
    {
        private readonly string _connectionString;
        public DatabaseService()
        {
            string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "koaza.db");
            _connectionString = "Data Source=" + path + ";Version=3;";
            if (!File.Exists(path)) SQLiteConnection.CreateFile(path);
            Initialize();
        }
        private void Initialize()
        {
            using (var con = new SQLiteConnection(_connectionString))
            {
                con.Open();
                using (var cmd = new SQLiteCommand("CREATE TABLE IF NOT EXISTS Predictions (Id INTEGER PRIMARY KEY AUTOINCREMENT, CreatedUtc TEXT NOT NULL, HomeTeam TEXT NOT NULL, AwayTeam TEXT NOT NULL, HomeGoalRate REAL NOT NULL, AwayGoalRate REAL NOT NULL, HomeWin REAL NOT NULL, DrawProbability REAL NOT NULL, AwayWin REAL NOT NULL, Over25 REAL NOT NULL, Under25 REAL NOT NULL, BothTeamsScore REAL NOT NULL, ModelVersion TEXT NOT NULL);", con)) cmd.ExecuteNonQuery();
            }
        }
        public void SavePrediction(string home, string away, double homeRate, double awayRate, Services.MatchProbabilities p)
        {
            using (var con = new SQLiteConnection(_connectionString))
            {
                con.Open();
                using (var cmd = new SQLiteCommand("INSERT INTO Predictions (CreatedUtc, HomeTeam, AwayTeam, HomeGoalRate, AwayGoalRate, HomeWin, DrawProbability, AwayWin, Over25, Under25, BothTeamsScore, ModelVersion) VALUES (@date,@home,@away,@hr,@ar,@hw,@draw,@aw,@over,@under,@btts,@version);", con))
                {
                    cmd.Parameters.AddWithValue("@date", DateTime.UtcNow.ToString("o")); cmd.Parameters.AddWithValue("@home", home); cmd.Parameters.AddWithValue("@away", away);
                    cmd.Parameters.AddWithValue("@hr", homeRate); cmd.Parameters.AddWithValue("@ar", awayRate); cmd.Parameters.AddWithValue("@hw", p.HomeWin); cmd.Parameters.AddWithValue("@draw", p.Draw); cmd.Parameters.AddWithValue("@aw", p.AwayWin);
                    cmd.Parameters.AddWithValue("@over", p.Over25); cmd.Parameters.AddWithValue("@under", p.Under25); cmd.Parameters.AddWithValue("@btts", p.BothTeamsScore); cmd.Parameters.AddWithValue("@version", "poisson-baseline-0.1"); cmd.ExecuteNonQuery();
                }
            }
        }
    }
}
