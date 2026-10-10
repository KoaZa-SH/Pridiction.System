# KoaZa Football Analytics — Product Requirements

## Product goal

KoaZa is a bilingual (Persian/English) Windows desktop football analytics application. Its primary workflow is selecting an upcoming fixture and viewing probability estimates for common football markets, calculated from historical match data. It must not require users to manually enter expected-goal (xG) rates for every fixture.

## Main user workflow

1. Open a day-by-day fixture list for the next seven days.
2. Filter fixtures by date, competition, and optionally team.
3. Select a fixture.
4. Load the available historical results and team statistics for both clubs.
5. Calculate probabilities using a documented statistical model.
6. Display market outcomes as percentage labels and horizontal color-coded probability bars.
7. Show data freshness, historical sample size, model version, and warnings when data is sparse or unavailable.
8. Allow refresh/import and support adding competitions and markets without rewriting the whole application.

## Competition catalogue

Initial requested competitions:
- English Premier League
- Bundesliga
- La Liga
- Serie A
- Ligue 1 (France)
- International national-team competitions
- UEFA Champions League and other relevant continental club competitions
- FIFA World Cup
- Olympic football
- Iranian leagues, especially the Persian Gulf Pro League

The competition catalogue must be data-driven/configurable so additional competitions can be added. Each competition needs provider-specific IDs/codes because identifiers differ by data provider. A competition must be marked unavailable when the configured free provider does not cover it; the UI must not fabricate fixtures.

## Data collection and storage

- Automatically import scheduled fixtures for the selected date range.
- Import completed historical matches, including kickoff, competition, home/away teams, full-time score, status, provider, and provider external ID.
- Store records locally in SQLite and upsert them safely to avoid duplicates.
- Track the source and update timestamp.
- Prefer provider APIs. Any website extraction must respect the site's terms, robots policy, rate limits, and applicable law; do not treat scraping as guaranteed or as a substitute for API coverage.
- Provider coverage, free quotas, historical depth, and Iranian/international competition availability must be checked at runtime and described honestly.
- Never require a user to manually enter expected goals for each upcoming match. If xG data is unavailable, estimate expected goals from historical scores and team/league context.

## Statistical model

First implementation should use completed historical results, not user-entered rates:
- Home and away goals scored/conceded.
- Recent form with a configurable rolling window.
- Home/away split.
- League scoring baseline and opponent strength where feasible.
- A Poisson-based goal distribution as a transparent baseline.
- Explicit handling for insufficient sample size and missing history.

Later improvements may include time-decay weighting, team-strength ratings, player/injury information if legally and reliably available, probability calibration, and backtesting. Do not label the estimates as guaranteed predictions. Evaluate forecasts against held-out historical matches before presenting them as validated.

## Initial markets

- Match result (1X2: home win / draw / away win)
- Double chance (1X / X2 / 12)
- Both teams to score (yes / no)
- Total goals over/under 1.5, 2.5, 3.5 (with additional lines configurable)
- Home team goals over/under and away team goals over/under
- First-half result and first-half goal totals when enough data is available
- Correct score distribution

Market definitions must be explicit. For example, BTTS is a yes/no outcome for the match, not a home-versus-away probability. The 1X2 market has three outcomes. Outcome probabilities within a complete market should sum to approximately 100%, subject to rounding and model truncation.

## Extensible markets

Markets should be represented through a reusable market-definition/calculation interface rather than hard-coded into the main window. Each market definition should specify its identifier, localized labels, outcome labels, calculation logic, and display order. Validate that output probabilities are finite and between 0 and 1.

## UI and localization

- Full Persian and English UI, with a language selector.
- Fixture list grouped by local date/day, for the next seven days.
- Competition filters and an easy way to add/enable competitions.
- Match analysis screen with tabs or sections for markets, score distribution, and data details.
- Horizontal probability bars with distinct accessible colors and percentages.
- Clearly show missing/stale data, provider limitations, historical sample size, and last update.
- Keep manual expected-goal input out of the normal user workflow; any diagnostic/manual override must be optional and clearly marked as a testing tool.

## Security and reliability

- API keys stay local and protected; never commit credentials.
- Respect provider quotas and use bounded retries.
- Cache data locally and show when online refresh fails.
- Predictions must not be represented as certainties or financial advice.

## Implementation milestones

1. Replace the manual xG-first home screen with the upcoming-fixture/date/competition workflow.
2. Expand provider and competition catalogue coverage; import and store completed historical results as well as fixtures.
3. Add SQLite queries for team/league historical statistics and calculate model inputs automatically.
4. Implement the market calculation registry and probability-bar UI.
5. Add tests for market probabilities, historical-stat calculations, and missing-data handling.
6. Backtest on chronological holdout data, document performance and calibration, and publish only after a clean CI build.

## Known constraints

A free data budget cannot guarantee complete schedules and historical data for every requested league and competition. Coverage must be provider-specific and visible in the application. The system should support more than one provider, but data from different providers should only be merged after team/competition mapping and duplicate-resolution rules are implemented.
