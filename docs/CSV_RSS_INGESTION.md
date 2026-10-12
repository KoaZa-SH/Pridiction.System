# CSV historical data and Persian RSS

## Implemented in this milestone

- Manual CSV import through CSV & Persian RSS in the main window.
- Supports common Football-Data.co.uk columns: Date, Div, HomeTeam, AwayTeam, FTHG, FTAG, HS, AS, HST, AST, HC, AC, HY, AY, HR, AR and Referee.
- Valid rows are normalized into the existing MatchStatistics SQLite table so the current historical-statistics model can read them.
- Missing statistic cells remain SQL NULL; they are never converted to zero.
- Stable row identifiers make repeated imports of the same league/date/teams an upsert rather than a duplicate.
- Persian sports RSS feeds are fetched, de-duplicated, and cached locally. Cache is retained and used when a feed is unreachable.
- RSS refresh runs at startup and every 30 minutes while the application is open; failures do not stop the main UI.

## Current limitations / next milestone

- CSV sources are manually selected; automatic download of historical CSVs is not enabled by default because source terms differ.
- Competition catalog is not yet populated dynamically from every provider. That requires each configured provider's league/competition endpoint and account-plan validation.
- RSS items are visible in the data-source window. The existing competition-specific news panel has not yet been replaced by a unified cross-source feed.
- Dates from CSV are stored as UTC date-only values because many historical CSV datasets do not include kickoff time or timezone.
- Provider coverage and terms of use must be checked before enabling automated downloads or AI/model training for a source.

## Included RSS sources

- https://www.varzesh3.com/rss/all
- https://parsfootball.com/feed
- https://www.khabarvarzeshi.com/rss
