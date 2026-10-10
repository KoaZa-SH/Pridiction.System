# Historical statistics and multi-market model

## Data flow

- Fixtures and FixtureHistory continue to store fixture/result records.
- FixtureDetails preserves the raw provider response for traceability and reparsing.
- MatchStatistics stores one normalized record per provider/fixture with completion status and nullable fields for total shots, shots on target, corners, cards, fouls, possession and referee.
- MatchEvents stores the normalized event timeline. Refreshes replace a fixture's event list atomically when the provider supplies a valid event payload.
- MarketPredictions stores versioned event-market outputs, sample sizes, explanations and a data-sufficiency flag. Missing probabilities are SQL NULL, not fake 0%.
- The existing Predictions table and goal-model markets are retained for compatibility.

The database creates new tables automatically when the application starts; existing fixture/history data is not dropped. Historical statistics use an upsert that preserves already-known non-null fields when a provider returns a partial response. Reads deduplicate equivalent fixtures across providers and prefer the row with more populated statistics. Legacy corners, shots-on-target, cards and referee values are backfilled from Fixtures and FixtureHistory.

## Providers

- API-FOOTBALL/API-SPORTS: fixture details plus separate statistics and events requests. Coverage depends on league and plan.
- 5DollarFootballAPI: league fixtures and fixture detail requests with stats/events included. Configure its key in API Settings and use a provider-specific league ID in the import window.
- Football-Charts: league fixtures/results endpoint. Enter its league slug (for example, premier). This source provides fixtures/results and its own goal-based probabilities, not the event-stat fields used for shots/corners/cards. Its own model probabilities are not yet blended into KoaZa's estimates.
- Other configured providers can continue supplying fixtures/results; they only contribute event-stat predictions when a compatible detailed-stat response is implemented and stored.

The import window attempts to collect detailed statistics for at most 10 completed fixtures per import (up to 5 for API-FOOTBALL to reduce request-quota use). A failed detail request for one match does not cancel the rest of the import.

## Model behavior

- The existing Poisson goal model remains in place, including its home/away form, head-to-head and available shots-on-target/corner adjustments.
- A separate event-market layer calculates total corners, team corners, total shots, shots on target and total yellow-card markets from historical event counts.
- When the upcoming referee is known and at least five prior matches with that referee have card data, a separate referee-card estimate is calculated; the tooltip shows the referee's historical mean and sample size.
- Event markets require at least five prior observations. Missing fields are not treated as zero, and insufficient markets are labelled unavailable.
- The current count-market approximation uses a Poisson distribution. Probabilities are estimates, not guarantees; proper calibration/backtesting and over-dispersion checks are still needed before operational reliance.

## Important limitations

- Injury and lineup payloads may be present in raw API details, but they are not yet used as predictive features. Treating every injured player equally without minutes, role and player-strength data would add misleading noise.
- Football-Charts' published probability feed is currently used for fixtures/results only; an external-probability ensemble needs verified response-field mapping and persistence before it should influence the model.
- A live Windows/.NET Framework build and authenticated API smoke test are still required. No provider key is embedded in source code.
