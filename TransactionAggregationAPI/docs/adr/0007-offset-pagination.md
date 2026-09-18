# ADR-0007: Offset pagination for transaction history

## Status
Accepted — with an explicit revisit threshold

## Context
The brief asks us to deliberately choose between offset and keyset/cursor
pagination for transaction history, rather than defaulting to one without
thought. Offset pagination (`Skip`/`Take`, `PaginatedResponse<T>`) is simpler
to implement, supports "jump to page N," and matches how the UI currently
presents transaction history (a page-numbered table, not an infinite-scroll
feed). Its known weakness is that `OFFSET` cost grows with the offset — deep
pages get progressively more expensive to compute — and results can shift if
rows are inserted/deleted between page loads.

Keyset/cursor pagination avoids both problems but gives up "jump to page N"
and requires the client to carry an opaque cursor instead of a page number.

## Decision
Use offset pagination (`PaginatedResponse<T>`, `PagedResult<T>`) for the
customer-facing transaction/customer list endpoints, backed by the composite
indexes already in place (`CustomerId+Date+Category`, `CustomerId+Status`),
which make `ORDER BY Date ... OFFSET ... LIMIT ...` for a single customer's
transactions efficient at realistic per-customer row counts (thousands, not
tens of millions, per customer).

**Revisit when**: a single customer's transaction history routinely exceeds
tens of thousands of rows *and* deep pages are actually being requested (not
just page 1–2), or the UI moves to infinite-scroll rather than page numbers —
either would justify migrating that specific endpoint to keyset pagination.

## Consequences
- Query cost is bounded by per-customer data volume, not global table size,
  because every paginated query is already scoped by `CustomerId` first via
  the composite index — the classic "offset pagination gets slow" failure
  mode is a global unscoped query problem, which this system doesn't have.
- We accept minor result-set shifting if transactions are inserted between two
  page loads of the same request; this is cosmetic (a transaction appearing on
  an adjacent page on refresh) and not a correctness issue for financial data
  integrity.

## Alternatives considered
- **Keyset/cursor pagination everywhere** — rejected as the default: adds
  client-side complexity (opaque cursors, no "jump to page 3") for a
  performance problem that doesn't exist at per-customer scale today.
