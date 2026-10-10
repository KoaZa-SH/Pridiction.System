using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Script.Serialization;
using KoaZa.Data;

namespace KoaZa.Services
{
    // One normalized row per completed fixture. Null means "not supplied", never zero.
    public sealed class MatchStatisticsRecord
    {
        public string Provider { get; set; }
        public string ExternalId { get; set; }
        public string League { get; set; }
        public string KickoffUtc { get; set; }
        public string Status { get; set; }
        public string HomeTeam { get; set; }
        public string AwayTeam { get; set; }
        public int? HomeGoals { get; set; }
        public int? AwayGoals { get; set; }
        public string Referee { get; set; }
        public int? HomeShots { get; set; }
        public int? AwayShots { get; set; }
        public int? HomeShotsOnTarget { get; set; }
        public int? AwayShotsOnTarget { get; set; }
        public int? HomeCorners { get; set; }
        public int? AwayCorners { get; set; }
        public int? HomeYellowCards { get; set; }
        public int? AwayYellowCards { get; set; }
        public int? HomeRedCards { get; set; }
        public int? AwayRedCards { get; set; }
        public int? HomeFouls { get; set; }
        public int? AwayFouls { get; set; }
        public double? HomePossession { get; set; }
        public double? AwayPossession { get; set; }
        public string DataSource { get; set; }
        public string UpdatedUtc { get; set; }
    }

    public sealed class MatchEventRecord
    {
        public string Provider { get; set; }
        public string ExternalId { get; set; }
        public int EventIndex { get; set; }
        public int? Minute { get; set; }
        public string Team { get; set; }
        public string Player { get; set; }
        public string Assist { get; set; }
        public string Type { get; set; }
        public string Detail { get; set; }
    }

    public sealed class MarketEstimate
    {
        public string Market { get; set; }
        public string Outcome { get; set; }
        public double Probability { get; set; }
        public int SampleSize { get; set; }
        public bool HasSufficientData { get; set; }
        public string Explanation { get; set; }
    }

    public sealed class MultiMarketPrediction
    {
        public double ExpectedHomeGoals { get; set; }
        public double ExpectedAwayGoals { get; set; }
        public int HomeGoalSamples { get; set; }
        public int AwayGoalSamples { get; set; }
        public IList<MarketEstimate> Markets { get; set; }
        public string DataQuality { get; set; }
    }

    public static class MatchStatisticsParser
    {
        public static MatchStatisticsRecord ParseApiFootball(string json, FixtureRecord fixture)
        {
            if (string.IsNullOrWhiteSpace(json) || fixture == null) return null;
            var root = Deserialize(json);
            var responses = List(Get(root, "response"));
            var item = responses.FirstOrDefault();
            if (item == null) return ParseFiveDollarOrGeneric(json, fixture);

            var result = NewRecord(fixture);
            var fixtureMap = Map(Get(item, "fixture"));
            string referee = String(Get(fixtureMap, "referee", null));
            if (!string.IsNullOrWhiteSpace(referee)) result.Referee = referee;
            fixture.Referee = result.Referee;
            var teams = Map(Get(item, "teams"));
            var home = Map(Get(teams, "home"));
            var away = Map(Get(teams, "away"));
            var stats = List(Get(item, "statistics"));
            if (stats.Count == 0) stats = List(Get(Get(root, "statisticsPayload"), "response"));
            foreach (var teamStats in stats)
            {
                var team = Map(Get(teamStats, "team"));
                string name = String(Get(team, "name", ""));
                bool isHome;
                if (Same(name, fixture.HomeTeam)) isHome = true;
                else if (Same(name, fixture.AwayTeam)) isHome = false;
                else
                {
                    string id = String(Get(team, "id", ""));
                    string homeId = String(Get(home, "id", ""));
                    string awayId = String(Get(away, "id", ""));
                    if (homeId.Length > 0 && id == homeId) isHome = true;
                    else if (awayId.Length > 0 && id == awayId) isHome = false;
                    else continue;
                }
                foreach (var stat in List(Get(teamStats, "statistics")))
                {
                    string type = String(Get(stat, "type", "")).Trim().ToLowerInvariant();
                    object value = Get(stat, "value", null);
                    if (type == "ball possession")
                    {
                        double? possession = Double(value);
                        if (possession.HasValue) { if (isHome) result.HomePossession = possession; else result.AwayPossession = possession; }
                        continue;
                    }
                    int? parsed = Int(value);
                    if (!parsed.HasValue) continue;
                    if (type == "total shots") { if (isHome) result.HomeShots = parsed; else result.AwayShots = parsed; }
                    else if (type == "shots on goal") { if (isHome) result.HomeShotsOnTarget = parsed; else result.AwayShotsOnTarget = parsed; }
                    else if (type == "corner kicks") { if (isHome) result.HomeCorners = parsed; else result.AwayCorners = parsed; }
                    else if (type == "yellow cards") { if (isHome) result.HomeYellowCards = parsed; else result.AwayYellowCards = parsed; }
                    else if (type == "red cards") { if (isHome) result.HomeRedCards = parsed; else result.AwayRedCards = parsed; }
                    else if (type == "fouls") { if (isHome) result.HomeFouls = parsed; else result.AwayFouls = parsed; }
                }
            }
            return result;
        }

        public static MatchStatisticsRecord ParseFiveDollarOrGeneric(string json, FixtureRecord fixture)
        {
            if (string.IsNullOrWhiteSpace(json) || fixture == null) return null;
            var root = Deserialize(json);
            object data = Get(root, "data", root);
            var list = List(data);
            object item = list.FirstOrDefault() ?? data;
            var row = NewRecord(fixture);
            var home = Map(Get(item, "teams") is IDictionary ? Get(Map(Get(item, "teams")), "home") : null);
            var away = Map(Get(item, "teams") is IDictionary ? Get(Map(Get(item, "teams")), "away") : null);
            var corners = Map(Get(item, "corners"));
            int? hc = Int(Get(corners, "home", null)); int? ac = Int(Get(corners, "away", null));
            if (hc.HasValue) row.HomeCorners = hc; if (ac.HasValue) row.AwayCorners = ac;
            var cards = Map(Get(item, "cards"));
            var homeCards = Map(Get(cards, "home")); var awayCards = Map(Get(cards, "away"));
            int? hy = Int(Get(homeCards, "yellow", null)); int? ay = Int(Get(awayCards, "yellow", null));
            int? hr = Int(Get(homeCards, "red", null)); int? ar = Int(Get(awayCards, "red", null));
            if (hy.HasValue) row.HomeYellowCards = hy; if (ay.HasValue) row.AwayYellowCards = ay;
            if (hr.HasValue) row.HomeRedCards = hr; if (ar.HasValue) row.AwayRedCards = ar;
            var statRows = List(Get(item, "statistics"));
            foreach (var teamStats in statRows)
            {
                string teamName = String(Get(Map(Get(teamStats, "team")), "name", ""));
                bool isHome;
                if (Same(teamName, fixture.HomeTeam)) isHome = true;
                else if (Same(teamName, fixture.AwayTeam)) isHome = false;
                else continue;
                foreach (var stat in List(Get(teamStats, "statistics")))
                {
                    string name = String(Get(stat, "type", Get(stat, "name", ""))).Trim().ToLowerInvariant();
                    object value = Get(stat, "value", null);
                    if (name == "ball possession")
                    {
                        double? possessionValue = Double(value);
                        if (possessionValue.HasValue) { if (isHome) row.HomePossession = possessionValue; else row.AwayPossession = possessionValue; }
                        continue;
                    }
                    int? parsed = Int(value);
                    if (!parsed.HasValue) continue;
                    if (name == "total shots" || name == "shots") { if (isHome) row.HomeShots = parsed; else row.AwayShots = parsed; }
                    else if (name == "shots on goal" || name == "shots on target") { if (isHome) row.HomeShotsOnTarget = parsed; else row.AwayShotsOnTarget = parsed; }
                    else if (name == "corner kicks" || name == "corners") { if (isHome) row.HomeCorners = parsed; else row.AwayCorners = parsed; }
                    else if (name == "yellow cards") { if (isHome) row.HomeYellowCards = parsed; else row.AwayYellowCards = parsed; }
                    else if (name == "red cards") { if (isHome) row.HomeRedCards = parsed; else row.AwayRedCards = parsed; }
                    else if (name == "fouls") { if (isHome) row.HomeFouls = parsed; else row.AwayFouls = parsed; }
                }
            }
            var stats = Map(Get(item, "statistics"));
            if (stats.Count == 0) stats = Map(Get(item, "stats"));
            var shots = Map(Get(stats, "shots"));
            int? hs = Int(Get(shots, "home", null)); int? ass = Int(Get(shots, "away", null));
            var onTarget = Map(Get(stats, "shots_on_target"));
            if (onTarget.Count == 0) onTarget = Map(Get(item, "shots_on_target"));
            int? hot = Int(Get(onTarget, "home", null)); int? aot = Int(Get(onTarget, "away", null));
            var offTarget = Map(Get(stats, "shots_off_target"));
            if (!hs.HasValue)
            {
                int? homeOff = Int(Get(offTarget, "home", null)); int? awayOff = Int(Get(offTarget, "away", null));
                if (hot.HasValue && homeOff.HasValue) hs = hot.Value + homeOff.Value;
                if (aot.HasValue && awayOff.HasValue) ass = aot.Value + awayOff.Value;
            }
            if (hs.HasValue) row.HomeShots = hs; if (ass.HasValue) row.AwayShots = ass;
            if (hot.HasValue) row.HomeShotsOnTarget = hot; if (aot.HasValue) row.AwayShotsOnTarget = aot;
            var possession = Map(Get(stats, "possession"));
            if (possession.Count == 0) possession = Map(Get(item, "possession"));
            double? hp = Double(Get(possession, "home", null)); double? ap = Double(Get(possession, "away", null));
            if (hp.HasValue) row.HomePossession = hp; if (ap.HasValue) row.AwayPossession = ap;
            string referee = String(Get(item, "referee", Get(Map(Get(item, "fixture")), "referee", null)));
            if (!string.IsNullOrWhiteSpace(referee)) row.Referee = referee;
            fixture.Referee = row.Referee;
            if (home.Count > 0 && row.HomeTeam.Length == 0) row.HomeTeam = String(Get(home, "name", ""));
            if (away.Count > 0 && row.AwayTeam.Length == 0) row.AwayTeam = String(Get(away, "name", ""));
            return row;
        }

        public static bool HasEventPayload(string json, FixtureRecord fixture)
        {
            if (string.IsNullOrWhiteSpace(json) || fixture == null) return false;
            var root = Deserialize(json);
            var responses = List(Get(root, "response"));
            var item = responses.FirstOrDefault();
            if (item != null && Get(item, "events") is IList) return true;
            var eventsPayload = Get(root, "eventsPayload");
            if (eventsPayload is IDictionary)
            {
                object errors = Get(eventsPayload, "errors");
                if (errors is IDictionary && ((IDictionary)errors).Count > 0) return false;
                return Get(eventsPayload, "response") is IList;
            }
            var data = Get(root, "data");
            var genericItem = List(data).FirstOrDefault() ?? data;
            return genericItem != null && Get(genericItem, "events") is IList;
        }

        public static List<MatchEventRecord> ParseEvents(string json, FixtureRecord fixture)
        {
            var output = new List<MatchEventRecord>();
            if (string.IsNullOrWhiteSpace(json) || fixture == null) return output;
            var root = Deserialize(json);
            var item = List(Get(root, "response")).FirstOrDefault();
            if (item == null) item = Get(root, "data", root);
            var events = List(Get(item, "events"));
            if (events.Count == 0) events = List(Get(Get(root, "eventsPayload"), "response"));
            int index = 0;
            foreach (var ev in events)
            {
                var time = Map(Get(ev, "time"));
                int? minute = Int(Get(time, "elapsed", null)) ?? Int(Get(ev, "minute", null));
                object teamObject = Get(ev, "team", null);
                var team = Map(teamObject);
                string teamName = String(Get(team, "name", teamObject));
                if (string.Equals(teamName, "home", StringComparison.OrdinalIgnoreCase)) teamName = fixture.HomeTeam;
                else if (string.Equals(teamName, "away", StringComparison.OrdinalIgnoreCase)) teamName = fixture.AwayTeam;
                object playerObject = Get(ev, "player", Get(ev, "player_in", null));
                object assistObject = Get(ev, "assist", Get(ev, "player_out", null));
                var player = Map(playerObject); var assist = Map(assistObject);
                string playerName = String(Get(player, "name", playerObject));
                string assistName = String(Get(assist, "name", assistObject));
                output.Add(new MatchEventRecord {
                    Provider = fixture.Provider, ExternalId = fixture.ExternalId, EventIndex = index++,
                    Minute = minute, Team = teamName,
                    Player = playerName, Assist = assistName,
                    Type = String(Get(ev, "type", "")), Detail = String(Get(ev, "detail", Get(ev, "period", "")))
                });
            }
            return output;
        }

        private static MatchStatisticsRecord NewRecord(FixtureRecord f) {
            return new MatchStatisticsRecord { Provider=f.Provider, ExternalId=f.ExternalId, League=f.League,
                KickoffUtc=f.KickoffUtc, Status=f.Status, HomeTeam=f.HomeTeam, AwayTeam=f.AwayTeam, HomeGoals=f.HomeGoals,
                AwayGoals=f.AwayGoals, Referee=f.Referee,
                HomeCorners=f.HomeCorners, AwayCorners=f.AwayCorners,
                HomeShotsOnTarget=f.HomeShotsOnTarget, AwayShotsOnTarget=f.AwayShotsOnTarget,
                HomeYellowCards=f.HomeYellowCards, AwayYellowCards=f.AwayYellowCards,
                DataSource=f.Provider, UpdatedUtc=DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) };
        }
        private static Dictionary<string, object> Deserialize(string json) {
            return Map(new JavaScriptSerializer { MaxJsonLength = int.MaxValue }.DeserializeObject(json));
        }
        private static Dictionary<string, object> Map(object o) { return o as Dictionary<string, object> ?? new Dictionary<string, object>(); }
        private static object Get(object o, string key, object fallback = null) { var m=Map(o); return m.ContainsKey(key) ? m[key] : fallback; }
        private static List<object> List(object o) {
            if (o is ArrayList) return ((ArrayList)o).Cast<object>().ToList();
            if (o is object[]) return ((object[])o).ToList();
            return new List<object>();
        }
        private static string String(object o) { return o == null ? "" : Convert.ToString(o, CultureInfo.InvariantCulture); }
        private static int? Int(object o) {
            if (o == null || o == DBNull.Value) return null;
            int n; if (int.TryParse(String(o).Replace("%","").Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) return n;
            double d; return double.TryParse(String(o), NumberStyles.Any, CultureInfo.InvariantCulture, out d) ? (int?)Math.Round(d) : null;
        }
        private static double? Double(object o) {
            if (o == null || o == DBNull.Value) return null;
            double n; return double.TryParse(String(o).Replace("%","").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out n) ? (double?)n : null;
        }
        private static bool Same(string a, string b) { return string.Equals((a??"").Trim(), (b??"").Trim(), StringComparison.OrdinalIgnoreCase); }
    }

    public static class MultiMarketPredictionService
    {
        public static MultiMarketPrediction AnalyzeEventMarkets(FixtureRecord fixture, IList<MatchStatisticsRecord> statsHistory)
        {
            statsHistory = statsHistory ?? new List<MatchStatisticsRecord>();
            var output = new MultiMarketPrediction { Markets = new List<MarketEstimate>(), DataQuality = "Historical statistics rows: " + statsHistory.Count };
            AddCountMarket(output, statsHistory, fixture, "Corners", "Total over 8.5", "Total under 8.5",
                s => Sum(s.HomeCorners,s.AwayCorners), 8.5, 5);
            AddCountMarket(output, statsHistory, fixture, "Yellow cards", "Total over 3.5", "Total under 3.5",
                s => Sum(s.HomeYellowCards,s.AwayYellowCards), 3.5, 5);
            AddCountMarket(output, statsHistory, fixture, "Shots on target", "Total over 8.5", "Total under 8.5",
                s => Sum(s.HomeShotsOnTarget,s.AwayShotsOnTarget), 8.5, 5);
            AddCountMarket(output, statsHistory, fixture, "Total shots", "Total over 22.5", "Total under 22.5",
                s => Sum(s.HomeShots,s.AwayShots), 22.5, 5);
            AddCountMarket(output, statsHistory, fixture, "Home team corners", "Over 4.5", "Under 4.5",
                s => Same(s.HomeTeam,fixture.HomeTeam) ? s.HomeCorners : s.AwayCorners, 4.5, 5);
            AddCountMarket(output, statsHistory, fixture, "Away team corners", "Over 4.5", "Under 4.5",
                s => Same(s.HomeTeam,fixture.AwayTeam) ? s.HomeCorners : s.AwayCorners, 4.5, 5);
            var refereeRows = statsHistory.Where(s => !string.IsNullOrWhiteSpace(fixture.Referee) &&
                Same(s.Referee,fixture.Referee) && s.HomeYellowCards.HasValue && s.AwayYellowCards.HasValue &&
                DateTimeOrder(s.KickoffUtc,fixture.KickoffUtc) < 0).OrderByDescending(s => s.KickoffUtc).Take(30).ToList();
            if (refereeRows.Count >= 5)
            {
                double mean = refereeRows.Average(s => s.HomeYellowCards.Value + s.AwayYellowCards.Value);
                double pOver = 1 - CdfPoisson(3, mean);
                output.Markets.Add(new MarketEstimate { Market="Referee yellow cards", Outcome="Total over 3.5",
                    Probability=Clamp(pOver,0,1), SampleSize=refereeRows.Count, HasSufficientData=true,
                    Explanation="Referee mean: "+mean.ToString("0.00",CultureInfo.InvariantCulture)+" yellow cards/match; Poisson approximation" });
            }
            else output.Markets.Add(new MarketEstimate { Market="Referee yellow cards", Outcome="Total over 3.5",
                Probability=0, SampleSize=refereeRows.Count, HasSufficientData=false,
                Explanation=string.IsNullOrWhiteSpace(fixture.Referee) ? "Upcoming referee is not known." : "Need at least five prior matches with the same referee." });
            return output;
        }

        public static MultiMarketPrediction Analyze(FixtureRecord fixture, IList<FixtureRecord> goalHistory,
            IList<MatchStatisticsRecord> statsHistory)
        {
            goalHistory = goalHistory ?? new List<FixtureRecord>();
            statsHistory = statsHistory ?? new List<MatchStatisticsRecord>();
            var homeGoals = goalHistory.Where(m => m.HomeGoals.HasValue && m.AwayGoals.HasValue &&
                (Same(m.HomeTeam, fixture.HomeTeam) || Same(m.AwayTeam, fixture.HomeTeam))).Take(12).ToList();
            var awayGoals = goalHistory.Where(m => m.HomeGoals.HasValue && m.AwayGoals.HasValue &&
                (Same(m.HomeTeam, fixture.AwayTeam) || Same(m.AwayTeam, fixture.AwayTeam))).Take(12).ToList();
            double homeAttackAtHome = AverageTeamGoal(goalHistory, fixture.HomeTeam, true, true);
            double awayDefenceAway = AverageTeamGoal(goalHistory, fixture.AwayTeam, false, false);
            double awayAttackAway = AverageTeamGoal(goalHistory, fixture.AwayTeam, false, true);
            double homeDefenceHome = AverageTeamGoal(goalHistory, fixture.HomeTeam, true, false);
            double homeOverallAttack = homeGoals.Count == 0 ? double.NaN : AverageGoals(homeGoals, fixture.HomeTeam, true);
            double awayOverallAttack = awayGoals.Count == 0 ? double.NaN : AverageGoals(awayGoals, fixture.AwayTeam, true);
            double awayOverallDefence = awayGoals.Count == 0 ? double.NaN : AverageGoals(awayGoals, fixture.AwayTeam, false);
            double homeOverallDefence = homeGoals.Count == 0 ? double.NaN : AverageGoals(homeGoals, fixture.HomeTeam, false);
            double homeRate = MeanAvailable(homeAttackAtHome, awayDefenceAway, homeOverallAttack, awayOverallDefence, 1.35);
            double awayRate = MeanAvailable(awayAttackAway, homeDefenceHome, awayOverallAttack, homeOverallDefence, 1.10);
            var h2h = goalHistory.Where(m => (Same(m.HomeTeam, fixture.HomeTeam) && Same(m.AwayTeam, fixture.AwayTeam)) ||
                (Same(m.HomeTeam, fixture.AwayTeam) && Same(m.AwayTeam, fixture.HomeTeam))).Take(8).ToList();
            if (h2h.Count >= 3) {
                double h2hHome = h2h.Average(m => Same(m.HomeTeam, fixture.HomeTeam) ? m.HomeGoals.Value : m.AwayGoals.Value);
                double h2hAway = h2h.Average(m => Same(m.HomeTeam, fixture.AwayTeam) ? m.HomeGoals.Value : m.AwayGoals.Value);
                homeRate = 0.8 * homeRate + 0.2 * h2hHome;
                awayRate = 0.8 * awayRate + 0.2 * h2hAway;
            }
            // Event data gently adjusts goal rates when a team has enough prior observations.
            // The adjustment is deliberately capped: raw shots/corners are not treated as goals.
            double leagueSot = MeanPerTeam(statsHistory, s => s.HomeShotsOnTarget, s => s.AwayShotsOnTarget, fixture.KickoffUtc);
            double leagueCorners = MeanPerTeam(statsHistory, s => s.HomeCorners, s => s.AwayCorners, fixture.KickoffUtc);
            double homeSot = TeamAverage(statsHistory, fixture.HomeTeam, true, s => s.HomeShotsOnTarget, s => s.AwayShotsOnTarget, fixture.KickoffUtc);
            double homeSotConceded = TeamAverage(statsHistory, fixture.HomeTeam, false, s => s.HomeShotsOnTarget, s => s.AwayShotsOnTarget, fixture.KickoffUtc);
            double awaySot = TeamAverage(statsHistory, fixture.AwayTeam, true, s => s.HomeShotsOnTarget, s => s.AwayShotsOnTarget, fixture.KickoffUtc);
            double awaySotConceded = TeamAverage(statsHistory, fixture.AwayTeam, false, s => s.HomeShotsOnTarget, s => s.AwayShotsOnTarget, fixture.KickoffUtc);
            if (leagueSot > 0 && homeSot > 0 && awaySotConceded > 0)
                homeRate *= Clamp(0.85 + 0.075 * (homeSot / leagueSot + awaySotConceded / leagueSot), 0.85, 1.15);
            if (leagueSot > 0 && awaySot > 0 && homeSotConceded > 0)
                awayRate *= Clamp(0.85 + 0.075 * (awaySot / leagueSot + homeSotConceded / leagueSot), 0.85, 1.15);
            double homeCornerAvg = TeamAverage(statsHistory, fixture.HomeTeam, true, s => s.HomeCorners, s => s.AwayCorners, fixture.KickoffUtc);
            double awayCornerAvg = TeamAverage(statsHistory, fixture.AwayTeam, true, s => s.HomeCorners, s => s.AwayCorners, fixture.KickoffUtc);
            if (leagueCorners > 0 && homeCornerAvg > 0) homeRate *= Clamp(1 + 0.025 * (homeCornerAvg / leagueCorners - 1), 0.97, 1.03);
            if (leagueCorners > 0 && awayCornerAvg > 0) awayRate *= Clamp(1 + 0.025 * (awayCornerAvg / leagueCorners - 1), 0.97, 1.03);
            homeRate = Clamp(homeRate, 0.05, 5); awayRate = Clamp(awayRate, 0.05, 5);
            var output = new MultiMarketPrediction { ExpectedHomeGoals=homeRate, ExpectedAwayGoals=awayRate,
                HomeGoalSamples=homeGoals.Count, AwayGoalSamples=awayGoals.Count, Markets=new List<MarketEstimate>() };
            var goalMarkets = PoissonModel.Analyze(homeRate, awayRate);
            foreach (var m in goalMarkets.Markets) output.Markets.Add(new MarketEstimate { Market=m.Market, Outcome=m.Outcome,
                Probability=Clamp(m.Probability,0,1), SampleSize=Math.Min(homeGoals.Count,awayGoals.Count),
                HasSufficientData=homeGoals.Count>=5 && awayGoals.Count>=5, Explanation="Historical scores + modest head-to-head blend" });

            AddCountMarket(output, statsHistory, fixture, "Corners", "Total over 8.5", "Total under 8.5",
                s => Sum(s.HomeCorners,s.AwayCorners), 8.5, 5);
            AddCountMarket(output, statsHistory, fixture, "Yellow cards", "Total over 3.5", "Total under 3.5",
                s => Sum(s.HomeYellowCards,s.AwayYellowCards), 3.5, 5);
            AddCountMarket(output, statsHistory, fixture, "Shots on target", "Total over 8.5", "Total under 8.5",
                s => Sum(s.HomeShotsOnTarget,s.AwayShotsOnTarget), 8.5, 5);
            AddCountMarket(output, statsHistory, fixture, "Total shots", "Total over 22.5", "Total under 22.5",
                s => Sum(s.HomeShots,s.AwayShots), 22.5, 5);
            AddCountMarket(output, statsHistory, fixture, "Home team corners", "Over 4.5", "Under 4.5",
                s => Same(s.HomeTeam,fixture.HomeTeam) ? s.HomeCorners : s.AwayCorners, 4.5, 5);
            AddCountMarket(output, statsHistory, fixture, "Away team corners", "Over 4.5", "Under 4.5",
                s => Same(s.HomeTeam,fixture.AwayTeam) ? s.HomeCorners : s.AwayCorners, 4.5, 5);
            var referee = statsHistory.Where(s => !string.IsNullOrWhiteSpace(s.Referee) && s.HomeYellowCards.HasValue && s.AwayYellowCards.HasValue)
                .Where(s => s.KickoffUtc.CompareTo(fixture.KickoffUtc ?? "") < 0).ToList();
            string targetReferee = fixture.Referee;
            var refereeRows = string.IsNullOrWhiteSpace(targetReferee) ? new List<MatchStatisticsRecord>() :
                referee.Where(s => Same(s.Referee,targetReferee)).Take(30).ToList();
            if (refereeRows.Count >= 5) {
                double mean = refereeRows.Average(s => s.HomeYellowCards.Value + s.AwayYellowCards.Value);
                double pOver = 1 - CdfPoisson(3, mean);
                output.Markets.Add(new MarketEstimate { Market="Referee yellow cards", Outcome="Total over 3.5",
                    Probability=Clamp(pOver,0,1), SampleSize=refereeRows.Count, HasSufficientData=true,
                    Explanation="Referee mean: "+mean.ToString("0.00",CultureInfo.InvariantCulture)+" yellow cards/match; Poisson approximation" });
            } else output.Markets.Add(new MarketEstimate { Market="Referee yellow cards", Outcome="Total over 3.5",
                Probability=0, SampleSize=refereeRows.Count, HasSufficientData=false,
                Explanation="Need at least five earlier matches with the same referee; no probability invented." });
            output.DataQuality = "Goals: " + homeGoals.Count + "/" + awayGoals.Count + " team matches; stats rows: " + statsHistory.Count;
            return output;
        }

        private static void AddCountMarket(MultiMarketPrediction output, IList<MatchStatisticsRecord> history, FixtureRecord fixture,
            string market, string overLabel, string underLabel, Func<MatchStatisticsRecord,int?> selector, double threshold, int minSample)
        {
            var prior = history.Where(s => s.HomeGoals.HasValue && s.AwayGoals.HasValue &&
                DateTimeOrder(s.KickoffUtc, fixture.KickoffUtc) < 0 &&
                (Same(s.HomeTeam,fixture.HomeTeam) || Same(s.AwayTeam,fixture.HomeTeam) ||
                 Same(s.HomeTeam,fixture.AwayTeam) || Same(s.AwayTeam,fixture.AwayTeam)))
                .Select(s => selector(s)).Where(v => v.HasValue).Select(v => (double)v.Value).Take(80).ToList();
            if (prior.Count < minSample) {
                output.Markets.Add(new MarketEstimate { Market=market, Outcome=overLabel, Probability=0, SampleSize=prior.Count,
                    HasSufficientData=false, Explanation="Insufficient historical event statistics; not calculated." });
                output.Markets.Add(new MarketEstimate { Market=market, Outcome=underLabel, Probability=0, SampleSize=prior.Count,
                    HasSufficientData=false, Explanation="Insufficient historical event statistics; not calculated." });
                return;
            }
            double mean = Clamp(prior.Average(), 0.05, 50);
            double pOver = 1 - CdfPoisson((int)Math.Floor(threshold), mean);
            output.Markets.Add(new MarketEstimate { Market=market, Outcome=overLabel, Probability=Clamp(pOver,0,1),
                SampleSize=prior.Count, HasSufficientData=true, Explanation="Empirical historical mean "+mean.ToString("0.00",CultureInfo.InvariantCulture)+"; Poisson approximation" });
            output.Markets.Add(new MarketEstimate { Market=market, Outcome=underLabel, Probability=Clamp(1-pOver,0,1),
                SampleSize=prior.Count, HasSufficientData=true, Explanation="Complement of over probability; Poisson approximation" });
        }
        private static double MeanPerTeam(IList<MatchStatisticsRecord> rows, Func<MatchStatisticsRecord,int?> home, Func<MatchStatisticsRecord,int?> away, string before)
        {
            var values = (rows ?? new List<MatchStatisticsRecord>())
                .Where(s => DateTimeOrder(s.KickoffUtc, before) < 0)
                .SelectMany(s => new[] { home(s), away(s) })
                .Where(v => v.HasValue).Select(v => (double)v.Value).ToList();
            return values.Count < 10 ? 0 : values.Average();
        }
        private static double TeamAverage(IList<MatchStatisticsRecord> rows, string team, bool own,
            Func<MatchStatisticsRecord,int?> homeValue, Func<MatchStatisticsRecord,int?> awayValue, string before)
        {
            var values = (rows ?? new List<MatchStatisticsRecord>())
                .Where(s => DateTimeOrder(s.KickoffUtc, before) < 0 &&
                    (Same(s.HomeTeam,team) || Same(s.AwayTeam,team)))
                .OrderByDescending(s => s.KickoffUtc)
                .Take(12)
                .Select(s => Same(s.HomeTeam,team) == own ? homeValue(s) : awayValue(s))
                .Where(v => v.HasValue).Select(v => (double)v.Value).ToList();
            return values.Count < 5 ? 0 : values.Average();
        }
        private static int? Sum(int? a,int? b) { return a.HasValue && b.HasValue ? (int?)(a.Value+b.Value) : null; }
        private static int DateTimeOrder(string a,string b)
        {
            DateTime da, db;
            bool pa = DateTime.TryParse(a, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out da);
            bool pb = DateTime.TryParse(b, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out db);
            if (pa && pb) return DateTime.Compare(da, db);
            return string.Compare(a ?? "", b ?? "", StringComparison.Ordinal);
        }
        private static double AverageTeamGoal(IList<FixtureRecord> rows, string team, bool venueHome, bool scored)
        {
            var values = (rows ?? new List<FixtureRecord>())
                .Where(m => m.HomeGoals.HasValue && m.AwayGoals.HasValue &&
                    (venueHome ? Same(m.HomeTeam, team) : Same(m.AwayTeam, team)))
                .Take(12)
                .Select(m => {
                    bool home = Same(m.HomeTeam, team);
                    if (scored) return (double)(home ? m.HomeGoals.Value : m.AwayGoals.Value);
                    return (double)(home ? m.AwayGoals.Value : m.HomeGoals.Value);
                }).ToList();
            return values.Count < 3 ? double.NaN : values.Average();
        }
        private static double MeanAvailable(double a, double b, double fallbackA, double fallbackB, double neutral)
        {
            var values = new List<double>();
            if (!double.IsNaN(a) && !double.IsInfinity(a)) values.Add(a);
            if (!double.IsNaN(b) && !double.IsInfinity(b)) values.Add(b);
            if (values.Count > 0) return values.Average();
            values.Clear();
            if (!double.IsNaN(fallbackA) && !double.IsInfinity(fallbackA)) values.Add(fallbackA);
            if (!double.IsNaN(fallbackB) && !double.IsInfinity(fallbackB)) values.Add(fallbackB);
            return values.Count > 0 ? values.Average() : neutral;
        }
        private static double AverageGoals(List<FixtureRecord> rows,string team,bool scored) {
            return rows.Average(m => {
                bool home=Same(m.HomeTeam,team);
                int own=scored ? (home?m.HomeGoals.Value:m.AwayGoals.Value) : (home?m.AwayGoals.Value:m.HomeGoals.Value);
                return (double)own;
            });
        }
        private static double CdfPoisson(int max,double lambda) {
            double term=Math.Exp(-lambda), sum=term;
            for(int i=1;i<=max;i++){term*=lambda/i;sum+=term;}
            return Clamp(sum,0,1);
        }
        private static double Clamp(double v,double min,double max) { return double.IsNaN(v)||double.IsInfinity(v)?min:Math.Max(min,Math.Min(max,v)); }
        private static bool Same(string a,string b) { return string.Equals((a??"").Trim(),(b??"").Trim(),StringComparison.OrdinalIgnoreCase); }
    }
}
