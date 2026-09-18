# Data retention

Instructions.md section 41 is explicit: *"Do not invent regulatory
requirements. Where requirements are unknown, explicitly state assumptions and
identify what must be confirmed with compliance/legal teams."* Nothing below
is a confirmed legal requirement — this is a starting checklist of decisions
that currently have no answer in code or documentation, written so a
compliance/legal review has a concrete list to react to rather than a blank
page.

## What the system currently does (facts, not assumptions)

- Transaction, account, and customer records are retained indefinitely — there
  is no automated deletion, archival, or expiry job anywhere in the codebase.
- `InboxMessage`/`OutboxMessage` rows are retained indefinitely once processed;
  there is no cleanup/retention job for them either (see the "Cleanup/retention"
  gap noted in [ADR-0003](adr/0003-polling-inbox-outbox-not-a-broker.md) —
  worth a follow-up once dead-lettered message volume is measured).
- **There is no customer deletion capability at all.** The README's endpoint
  table lists `DELETE /customers/{id} — Delete account`, but no such endpoint
  exists in `CustomerEndpoints.cs` (the only `MapDelete` in the whole API is
  bank-link revocation, which just changes a `BankLink`'s status — it doesn't
  delete anything). If a right-to-erasure obligation applies, this is a real
  gap today, not a "verify the cascade behavior" question — there is nothing
  to verify. The README should be corrected to stop documenting an endpoint
  that doesn't exist.
- Sensitive fields (bank-link tokens) are encrypted at rest via
  `IBankLinkCredentialProtector`; general PII (email, name, transaction
  descriptions) is not encrypted at the column level, only via Postgres's own
  encryption-at-rest (if enabled at the infrastructure layer — verify this is
  actually configured on the production database, not assumed).

## Open questions for compliance/legal (assumptions, not answers)

| Question | Current assumption | Why it matters |
|---|---|---|
| How long must transaction records be retained? | Unknown — banking/financial regulations in the operating jurisdiction(s) may mandate a minimum (commonly multi-year) retention period | Determines whether "retain forever" (current behavior) is actually compliant, over-retention (a liability/minimization concern), or under-retention |
| Is there a maximum retention period, or a right-to-erasure obligation (e.g. POPIA in South Africa, given the ZAR currency and `.co.za` seed data, or GDPR if any EU customers)? | Unknown — not confirmed which regulatory regime applies | Determines whether indefinite retention is itself a compliance gap |
| Must deleted customers' transaction history be retained for audit purposes even after account deletion? | Unknown | Directly conflicts with a naive "delete everything on customer delete" implementation if audit retention is required |
| What counts as an audit trail requirement here — do `AuditEntry`-style records need their own extended retention independent of the source data? | No dedicated `AuditEntry` entity currently exists in the domain model (the brief listed it as a candidate entity, not a confirmed requirement) | If audit logging is legally required, it needs its own retention policy, likely longer than operational data |
| Are there data residency requirements (e.g. data must stay in-region)? | Unknown — not evaluated; infrastructure/deployment region isn't specified in this repo | Affects where Postgres/backups/logs can physically live |
| Does encryption-at-rest need to be field-level for PII (not just disk-level), and does encryption-in-transit need to be enforced end-to-end (TLS termination point matters)? | Assumed disk-level (Postgres/cloud provider) is sufficient today | A stricter regime may require field-level encryption for email/name, not just tokens |

## Recommendation

Before a production launch handling real customer financial data, get an
explicit answer to each row above from whoever owns compliance for this
product, and turn each answer into either a scheduled retention/archival job,
a documented exception, or an ADR — not a silent assumption. Until that
happens, treat "retain everything indefinitely, delete nothing automatically"
as the current, unreviewed default, not a deliberate policy.
