# ADR-0008: Single PostgreSQL schema, not one schema per module

## Status
Accepted

## Context
The brief asks us to evaluate whether modules should have separate PostgreSQL
schemas (e.g. `transactions.*`, `providers.*`, `audit.*`, `integration.*`) and
explicitly warns: *"Avoid pretending that PostgreSQL schemas provide security
boundaries if the actual deployment does not enforce them."*

Checking the actual EF Core configuration: every entity is mapped via
`builder.ToTable("Name")` with no schema argument, so every table lives in
Postgres's default `public` schema (`AccountConfiguration.cs`,
`CustomerConfiguration.cs`, `TransactionConfiguration.cs`,
`BankLinkConfiguration.cs`, `WebhookSourceConfiguration.cs`,
`InboxMessageConfiguration.cs`, `OutboxMessageConfiguration.cs` — confirmed
across all migration snapshots too). This was never a deliberate per-schema
design; it's simply what "don't add a schema argument" produces by default.

Per-schema separation would only provide real value here if:
- Different modules connected to Postgres with different database roles/grants
  (they don't — the API uses a single `postgres` connection string/role for
  everything, per `docker-compose.yml` and the k8s ConfigMap), or
- Modules were operated/migrated independently by different teams (they
  aren't — one team, one deployable, one migration Job), or
- There were a genuine need to visually/organizationally group ~7 tables
  (there isn't at this table count).

## Decision
Keep everything in the single default `public` schema. Do not introduce
per-module PostgreSQL schemas.

## Consequences
- Simpler migrations (no schema-qualification to get wrong), simpler
  connection configuration, simpler `search_path` reasoning.
- No false sense of security: since the API connects with one role that can
  read/write every table regardless of schema, splitting into schemas today
  would be cosmetic organization, not an actual security boundary — doing it
  anyway and implying otherwise would be exactly the "pretending schemas are
  a security boundary" trap the brief warns against.
- **Revisit when**: the table count grows enough that schema-based grouping
  becomes genuinely useful for humans browsing the database, or — more
  importantly — if a real per-module database role/grant model is introduced
  (e.g. a reporting/analytics role that should only read certain tables). At
  that point, schema separation combined with `GRANT`s per schema would
  provide an actual boundary, not just a label.

## Alternatives considered
- **One schema per module (`transactions.*`, `providers.*`, etc.)** —
  rejected for now: no independent role/grant model exists to make the
  separation a real boundary rather than a naming convention; would add
  migration complexity for a benefit that isn't realized without also
  restructuring database access control, which nothing today requires.
