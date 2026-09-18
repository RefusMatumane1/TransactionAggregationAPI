# ADR-0006: No database partitioning yet

## Status
Accepted — with an explicit revisit threshold

## Context
The brief requires evaluating partitioning, not assuming it. Partitioning
(e.g. the `transactions` table by date range) buys query-performance and
maintenance benefits at high row counts, at the cost of partition-maintenance
automation, more complex index design, and migration complexity.

We have no production volume yet, and no measured query-performance problem.
Targeted composite indexes (`CustomerId+Date+Category`, `CustomerId+Status` —
see `TransactionConfiguration.cs`) are sufficient for the query patterns the
API actually issues (per-customer, date-ranged, category-filtered, paginated
lookups) at any volume a single unpartitioned Postgres table comfortably
handles (tens of millions of rows is routine for a well-indexed table).

## Decision
Do not partition the `transactions` table (or any other table) now.

**Revisit when any of these becomes true:**
- The `transactions` table exceeds roughly 50–100 million rows, or
- `EXPLAIN ANALYZE` on the hot per-customer/date-range queries shows sequential
  scans or index bloat that composite indexes alone can't fix, or
- A data-retention/archival requirement emerges (ADR/decision not yet made —
  see the Data Retention section of the original brief) that would benefit from
  dropping old partitions wholesale instead of deleting rows.

If/when that happens, partition `transactions` by `Date` (range partitioning),
since date-range filtering is already the dominant query shape, and old
partitions can then be detached/archived instead of bulk-deleted.

## Consequences
- Simpler schema, simpler migrations, simpler mental model today.
- We accept that crossing the revisit threshold above requires a follow-up
  migration project (expand/contract migration to introduce partitioning
  without downtime) — this is a known, bounded cost, not a hidden one.

## Alternatives considered
- **Partition from day one "to be safe"** — rejected: no measured need,
  and partitioning an empty/low-volume table adds maintenance overhead
  (partition creation jobs, more complex index management) for a benefit that
  doesn't exist yet. This is the literal "complexity without justification"
  the brief warns against.
