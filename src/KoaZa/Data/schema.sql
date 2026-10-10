PRAGMA foreign_keys = ON;
CREATE TABLE IF NOT EXISTS Predictions (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  CreatedUtc TEXT NOT NULL,
  HomeTeam TEXT NOT NULL,
  AwayTeam TEXT NOT NULL,
  HomeGoalRate REAL NOT NULL CHECK(HomeGoalRate >= 0),
  AwayGoalRate REAL NOT NULL CHECK(AwayGoalRate >= 0),
  HomeWin REAL NOT NULL CHECK(HomeWin BETWEEN 0 AND 1),
  DrawProbability REAL NOT NULL CHECK(DrawProbability BETWEEN 0 AND 1),
  AwayWin REAL NOT NULL CHECK(AwayWin BETWEEN 0 AND 1),
  Over25 REAL NOT NULL CHECK(Over25 BETWEEN 0 AND 1),
  Under25 REAL NOT NULL CHECK(Under25 BETWEEN 0 AND 1),
  BothTeamsScore REAL NOT NULL CHECK(BothTeamsScore BETWEEN 0 AND 1),
  ModelVersion TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_Predictions_CreatedUtc ON Predictions(CreatedUtc);

-- Structured historical event statistics. NULL means the provider did not report a value.
CREATE TABLE IF NOT EXISTS MatchStatistics (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  Provider TEXT NOT NULL,
  ExternalId TEXT NOT NULL,
  League TEXT,
  KickoffUtc TEXT,
  HomeTeam TEXT NOT NULL,
  AwayTeam TEXT NOT NULL,
  HomeGoals INTEGER,
  AwayGoals INTEGER,
  Referee TEXT,
  HomeShots INTEGER,
  AwayShots INTEGER,
  HomeShotsOnTarget INTEGER,
  AwayShotsOnTarget INTEGER,
  HomeCorners INTEGER,
  AwayCorners INTEGER,
  HomeYellowCards INTEGER,
  AwayYellowCards INTEGER,
  HomeRedCards INTEGER,
  AwayRedCards INTEGER,
  HomeFouls INTEGER,
  AwayFouls INTEGER,
  HomePossession REAL,
  AwayPossession REAL,
  DataSource TEXT,
  UpdatedUtc TEXT NOT NULL,
  UNIQUE(Provider, ExternalId)
);
CREATE INDEX IF NOT EXISTS IX_MatchStatistics_TeamsKickoff ON MatchStatistics(HomeTeam, AwayTeam, KickoffUtc);
CREATE INDEX IF NOT EXISTS IX_MatchStatistics_RefereeKickoff ON MatchStatistics(Referee, KickoffUtc);

CREATE TABLE IF NOT EXISTS MatchEvents (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  Provider TEXT NOT NULL,
  ExternalId TEXT NOT NULL,
  EventIndex INTEGER NOT NULL,
  Minute INTEGER,
  Team TEXT,
  Player TEXT,
  Assist TEXT,
  Type TEXT,
  Detail TEXT,
  UNIQUE(Provider, ExternalId, EventIndex)
);

-- Every model run is preserved with its sample size and data sufficiency flag.
-- Unavailable probabilities are NULL, not fake 0%.
CREATE TABLE IF NOT EXISTS MarketPredictions (
  Id INTEGER PRIMARY KEY AUTOINCREMENT,
  RunId TEXT NOT NULL,
  CreatedUtc TEXT NOT NULL,
  Provider TEXT,
  ExternalId TEXT,
  League TEXT,
  KickoffUtc TEXT,
  HomeTeam TEXT NOT NULL,
  AwayTeam TEXT NOT NULL,
  Market TEXT NOT NULL,
  Outcome TEXT NOT NULL,
  Probability REAL,
  SampleSize INTEGER NOT NULL,
  HasSufficientData INTEGER NOT NULL,
  Explanation TEXT,
  ModelVersion TEXT NOT NULL,
  UNIQUE(RunId, Market, Outcome)
);
CREATE INDEX IF NOT EXISTS IX_MarketPredictions_FixtureCreated ON MarketPredictions(HomeTeam, AwayTeam, CreatedUtc);
