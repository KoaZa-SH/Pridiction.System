# Historical statistics and multi-market model

## Data flow

- Fixtures and FixtureHistory continue to store fixture/result records.
- FixtureDetails preserves the raw provider response for traceability and reparsing.
- MatchStatistics stores one normalized record per provider/fixture with nullable fields for shots, shots on target, corners, cards, fouls, possession and referee.
- MatchEvents stores the normalized event timeline. Refreshes replace a fixture's event list atomically so stale events are not retained.
- MarketPredictions stores versioned market outputs, sample sizes, explanations and a data-sufficiency flag. Missing probabilities are SQL NULL, not fake 0%.
- The existing Predictions table is retained for compatibility with the original goal-market model.

The database creates new tables automatically when the application starts; existing fixture/history data is not dropped. Historical statistics use an upsert that preserves already-known non-null fields when a provider returns a partial response. Reads deduplicate equivalent fixtures across providers and prefer the row with more populated statistics.

## Providers

- API-FOOTBALL/API-SPORTS: fixture details plus explicit /fixtures/statistics?fixture=... and /fixtures/events?fixture=... requests. Coverage depends on league and plan.
- 5DollarFootballAPI: league fixtures and fixture detail requests with include=events,stats. Enter a provider-specific league ID in the import window and configure a key in API Settings.
- Football-Charts: league fixtures/results endpoint. Enter its league slug (for example, premier). This source exposes results and its own goal-based probabilities, not the event-stat fields used for shots/corners/cards. Its own model probabilities are not yet blended into KoaZa's estimates.
- Other configured providers can continue supplying fixtures/results; they only contribute event-stat predictions when a compatible detailed-stat response is implemented and stored.

The import window attempts to collect detailed statistics for at most 10 completed fixtures per import, and only from API-FOOTBALL or 5DollarFootballAPI. This is intentionally bounded to reduce accidental quota exhaustion. A failed detail request for one match does not cancel the rest of the import.

## Model behavior

- Goal markets use a Poisson score model.
- Home/away venue-specific scoring and conceding rates are used when enough matches exist; otherwise the model falls back to overall team rates or neutral priors.
- Head-to-head results are blended modestly (20%) only when at least three prior meetings exist.
- Historical shots on target adjust expected goals conservatively, capped at ±15%; corners have a much smaller capped effect (±3%). These adjustments are used only when enough historical event data exists and only from matches before the target kickoff.
- Separate market estimates are produced for total corners, team corners, total shots, shots on target, total yellow cards and (when the referee is known and at least five prior matches with that referee are available) referee-card totals.
- A market is marked unavailable when the historical sample is insufficient. No zero probability is shown as if it were a calculated result.
- Probabilities are statistical estimates, not guarantees. The current count-market approximation uses a Poisson distribution; proper calibration/backtesting and over-dispersion checks are still needed before trusting it operationally.

## Important limitations

- Injury and lineup payloads may be present in raw API details, but they are not yet used as predictive features. Treating every injured player equally without minutes, role and player-strength data would add misleading noise.
- Football-Charts' published probability feed is currently used for fixtures/results only; an external-probability ensemble needs verified response-field mapping and persistence before it should influence the model.
- A live Windows/.NET Framework build and authenticated API smoke test are still required. No provider key is embedded in source code.
