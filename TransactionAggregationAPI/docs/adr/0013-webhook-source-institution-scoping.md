# ADR-0013: Inbound sources are scoped to the institutions they serve

## Status
Accepted

## Context
Inbound deliveries identify their target by `externalAccountId`, a value the **sender**
supplies. Authentication (a per-source API key for the webhook, a registered `source`
header for Kafka) established *who* was sending, but nothing checked *what they were
allowed to write to*. `ProcessInboundTransactionsCommandHandler` resolved every active
`BankLink` with that external account id, at any institution, and stored the
transactions for each linked customer.

The concrete failure: any holder of **any** active source key could post a batch naming
another provider's `externalAccountId` and place fabricated transactions in that
customer's history. External account ids are not secrets (they appear in bank
statements and aggregator dashboards), and one leaked key would compromise every
institution, not just the one it belonged to. The threat model claimed the opposite, so
the gap also sat behind a false assurance.

## Decision
- `WebhookSource` carries `AuthorizedInstitutions` (`text[]`). It must be non-empty on
  create, and is managed through `PUT /api/v1/admin/webhook-sources/{id}/institutions`.
  Names are compared case-insensitively against `BankLink.Institution`.
- Processing applies a delivery only to links at institutions the sending source is
  authorized for. Links at other institutions are skipped and logged. If none remain,
  the handler returns `Ingestion.SourceNotAuthorized` (`ErrorType.Forbidden`), which the
  inbox classifies as **permanent** and dead-letters at once, with an
  `inbound.dead_lettered` audit event.
- **The check happens at processing, not at receipt.** A delivery can legitimately arrive
  before the customer finishes linking the account, so an unknown account at receipt time
  isn't evidence of anything. The inbox already holds the delivery durably, and replaying
  a dead-lettered delivery (after an admin fixes a source's scope) requeues it.
- **Fail closed on migration.** Existing sources get an empty scope
  (`AddWebhookSourceAuthorizedInstitutions`), so their deliveries dead-letter until an
  admin scopes them. The Development mock source is scoped automatically from
  `MockAggregator:AuthorizedInstitutions`, which defaults to every institution.

## Consequences
- A leaked key can now reach only its own institutions' accounts: the blast radius
  matches the business relationship.
- Deploying this is a behaviour change for any existing production source, and must be
  paired with scoping each source. That is the deliberate cost of failing closed.
- Covered against real Postgres by
  `IngestionIntegrityTests.Handle_SourceNotAuthorizedForTheLinksInstitution_IsRefusedAsPermanent_AndStoresNothing`.

## Alternatives considered
- **Bind each `BankLink` to the source that created it.** More precise, but links are
  created through the OAuth flow, which knows the aggregator and not the webhook source,
  and one aggregator legitimately fronts many institutions. It can be layered on later.
- **Reject at receipt (HTTP 403).** Rejected: it would drop legitimate deliveries that
  arrive before linking completes.
