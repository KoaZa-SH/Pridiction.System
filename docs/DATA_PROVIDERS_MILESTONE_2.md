# KoaZa data providers — milestone 2

This milestone adds a provider-neutral fixture import flow. It does not yet calculate predictions from imported results; the current Poisson screen still uses manually entered expected-goal rates.

## Supported providers

### 1. API-Football
- Provider in settings: API-Football
- Requires an API key from https://www.api-football.com/
- Fixture endpoint: /fixtures?league={id}&season={year}
- Enter a numeric league ID. Example: 39 for the English Premier League. Verify current coverage and plan limits in the provider's own dashboard.

### 2. football-data.org
- Provider in settings: football-data.org
- Requires a token from https://www.football-data.org/
- Uses the X-Auth-Token header.
- Fixture endpoint: /v4/competitions/{code}/matches?season={year}
- Enter a competition code such as PL (Premier League), PD (La Liga), BL1 (Bundesliga), SA (Serie A), or FL1 (Ligue 1). Available competitions depend on the provider's current free plan.

### 3. OpenLigaDB
- Provider in settings: OpenLigaDB
- Does not require a key.
- Best suited to German league results and fixtures.
- Example league shortcut: bl1 for Bundesliga; bl2 for 2. Bundesliga.
- Endpoint: /getmatchdata/{leagueShortcut}/{season}

## Setup
1. Open API Settings / تنظیمات API.
2. Select a provider.
3. Paste that provider's key/token if required and click Add provider.
4. Repeat to add other providers or multiple keys for the same provider.
5. Click Save securely. Credentials are encrypted with Windows DPAPI for the current Windows user under %LOCALAPPDATA%\KoaZa; they are not stored in the repository.
6. Click Fetch Fixtures / دریافت مسابقات on the main screen.
7. For automatic fallback, select Auto fallback and use a standard competition code: EPL, LALIGA, BUNDESLIGA, SERIEA, LIGUE1, or IRAN. The app tries enabled providers in the order they appear in API Settings and continues when a provider errors or returns no fixtures. Alternatively, select one provider and enter its native league/competition code. Then enter the season year and click Fetch & save fixtures.

## Key rotation and provider limits
The app tries enabled credentials for the selected provider. It moves to the next credential on HTTP authentication/rate-limit errors or provider-reported API errors. A normal connection or server error is reported instead of cycling through all keys. Multiple keys only provide additional usable quota if the provider actually grants independent quota to those credentials; this feature does not bypass provider terms or limits.

## Current limitations
- Provider adapters currently cover fixtures/results only.
- Iran league coverage is expected to rely on API-Football (if the selected plan exposes it); football-data.org's free competition set and OpenLigaDB's coverage are narrower.
- Automatic fallback currently maps the Premier League, La Liga, Bundesliga, Serie A and Ligue 1 across supported services. Iran's league relies on API-Football coverage; other providers may not cover it.
- Imported fixtures are saved to SQLite and displayed for that import. The next milestone is to read completed fixture history from SQLite, compute team form/goal rates, and feed those data into the prediction model.
- No bookmaker odds or guaranteed outcomes are provided.