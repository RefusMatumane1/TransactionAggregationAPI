# ADR-0004: No Saga

## Status
Accepted

## Context
The brief explicitly warns against implementing a Saga "merely because the
project is production-grade," and requires a Saga only when there is a
distributed business workflow spanning multiple **independently committed**
steps that needs compensating behavior on partial failure.

Reviewing the actual workflows in this system:
- **Bank link (initiate → complete → revoke)**: each step is a single request
  handled within one database transaction against one database. `CompleteBankLink`
  does call an external provider (`IBankAggregatorClient.ExchangeAuthorizationCodeAsync`,
  `GetLinkedAccountAsync`) before its local commit, but there is nothing to
  *compensate* — if the external call fails, the handler simply returns a
  failure `Result` and no local state has changed. There is no second
  already-committed step elsewhere that would need to be rolled back.
- **Transaction ingestion**: a single handler, single database transaction,
  covering "create transactions + categorize + write outbox row." Not a
  multi-step distributed workflow.

Nowhere in the current system does one step commit, then a *later, separate*
step fail in a way that requires undoing the first step's already-committed
effects.

## Decision
Do not implement a Saga (orchestration or choreography). Model each workflow as
a single transactional unit of work per request, and use plain `Result` failures
for anything that goes wrong before that unit of work commits.

## Consequences
- Simpler code, no saga state machine, no compensation logic to write, test, or
  keep correct across versions.
- **Revisit this decision if** a future workflow genuinely spans multiple
  independently-committed steps with real compensation needs — for example, if
  bank-link completion is split so that "create local account" commits
  separately from and before "activate the link with the provider," and a
  failure in the second step must undo the first. That is not the current
  design, but if it becomes necessary, this ADR should be superseded rather than
  silently ignored.

## Alternatives considered
- **Saga (orchestration)** — rejected: no compensating action exists to model;
  would add a state machine and persistence for state that doesn't need to
  outlive a single request/transaction today.
