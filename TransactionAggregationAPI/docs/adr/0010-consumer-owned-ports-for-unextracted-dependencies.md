# ADR-0010: Consumer-owned ports for dependencies on not-yet-extracted modules

## Status
Accepted

## Context
Phase 1 (ADR-0001, ADR-0009) extracted WebhookSources first specifically because
the audit found it had zero coupling to any other entity — it proved the
project-per-module / DbContext-per-module / schema-per-module pattern at the
lowest possible risk. BankLinks is the second module extracted, and it does
**not** have that luxury: `CompleteBankLinkCommandHandler` needs an `Account` to
exist for a newly linked external bank account, but Accounts/Customers haven't
been extracted into their own module yet — they're still logical-only, living in
the original shared `TransactionAggregation.Domain/Application/Infrastructure/
Persistence` projects (see ADR-0001's phase tracking).

The original code (`CompleteBankLinkCommandHandler`) resolved this by loading a
`Customer` with `.Include(c => c.Accounts)` and calling `customer.AddAccount(...)`
— Customer's aggregate reaching into Account's own "create an account" use case.
This is exactly the kind of cross-entity reach-through the whole restructuring
exists to eliminate (see the ~10 handlers the original audit found doing this).
Simply moving `BankLink` into its own module without fixing this would only
relocate the coupling, not remove it: `Modules.BankLinks` would need either a
direct reference to the `Account` entity and `IApplicationDbContext` (recreating
the shared-context coupling ADR-0009 exists to prevent), or the old
`customer.AddAccount()` call kept in place, permanently blocking Accounts from
ever extracting cleanly later.

## Decision
When a module being extracted needs a capability that a not-yet-extracted module
would otherwise provide, **the consumer defines and owns the interface** (a
"port"), and an adapter implementing it lives wherever the dependency
currently lives — wired to the port via DI in `Program.cs` (the composition
root). This is standard Ports & Adapters / Hexagonal Architecture, applied at
module-extraction boundaries specifically.

Concretely, for BankLinks → Account:
- `Modules.BankLinks.Ports.IAccountProvisioningPort` — owned by BankLinks,
  takes only primitives (`Guid customerId, string accountNumber, ...`), returns
  a `SharedKernel.Common.Models.Result<Guid>`. BankLinks has zero reference to
  the `Account` entity, `AccountType` enum, or `IApplicationDbContext`.
- `TransactionAggregation.Application.Adapters.AccountProvisioningAdapter` —
  implements the port. Lives in the still-unextracted Application project
  (which already has `IApplicationDbContext` and `Account`), so it's a
  perfectly normal same-module operation from Application's point of view:
  find-or-create an `Account` via `Account.Create(...)` directly (never
  `Customer.AddAccount(...)`), add it to `IApplicationDbContext.Accounts`, save.
- `Program.cs` wires `services.AddScoped<IAccountProvisioningPort,
  AccountProvisioningAdapter>()`.

The same shape is used in the opposite direction, for a not-yet-extracted
module reading BankLinks' data: `ProcessInboundTransactionsCommandHandler`
(still in the shared Application project, since Transactions isn't extracted
yet) used to run `context.BankLinks.FirstOrDefaultAsync(...)` directly against
the old shared `IApplicationDbContext`. That's now
`Modules.BankLinks.Contracts.IBankLinksReadApi.FindActiveLinkByExternalAccountIdAsync(...)`,
a **published read contract** BankLinks owns and implements
(`BankLinksReadApi`, internal to the module), returning a narrow
`ActiveBankLinkInfo` DTO of primitives — never the `BankLink` entity or
`IBankLinksDbContext`'s `DbSet<BankLink>` itself. This is deliberately
narrower than `IMessagingDbContext` (which exposes `DbSet<InboxMessage>`/
`DbSet<OutboxMessage>` directly to every module) — `IMessagingDbContext` is a
genuinely generic building block every module is expected to write
Inbox/Outbox rows through directly, whereas `IBankLinksReadApi` exists
specifically so Transactions can't reach through into BankLinks' aggregate the
way the pre-restructuring code reached into everything through the one shared
`IApplicationDbContext`.

`CreateAccountCommandHandler` (in the still-unextracted Account/Customer area)
had the identical `customer.AddAccount(...)` violation and is fixed the same
way — call `Account.Create(...)` directly — but does **not** go through
`IAccountProvisioningPort`: it's already inside the same not-yet-extracted
project as `Account`, so there's no module boundary to cross yet, and routing
an intra-project call through a port would be needless indirection. The port
exists only where a module boundary (BankLinks vs. everything else) is already
real; it's not applied speculatively ahead of an actual extraction.

## Consequences
- BankLinks extracts cleanly today without waiting for Accounts/Customers to
  extract first, and — critically — needs **zero code changes** in
  `Modules.BankLinks` when Accounts/Customers eventually do extract: only
  `AccountProvisioningAdapter`'s implementation moves to wherever the new
  `Modules.Accounts` project ends up, and `Program.cs`'s DI wiring is updated.
  The port's shape doesn't change because it was never tied to
  `IApplicationDbContext` or the `Account` entity's actual location.
- `Customer.AddAccount()` / `Customer.Accounts` still exist on the `Customer`
  entity (used by `TransactionAggregationAPI/SeedData.cs` and a few
  domain-level unit tests) — not removed this phase. Both production call
  sites that used to route through them (`CreateAccountCommandHandler`,
  `CompleteBankLinkCommandHandler`/its adapter) no longer do, so the method is
  effectively dead in the request path, but deleting it now would also require
  a migration reworking the `Customer`→`Account` EF relationship
  (`CustomerConfiguration.HasMany(c => c.Accounts)`), which is out of scope for
  a BankLinks-focused phase. Deferred to whichever phase extracts
  Accounts/Customers, same reasoning ADR-0001 already applies to that
  extraction being deferred.
- One more interface for a reader to track per cross-module dependency, versus
  the old one-shared-context model where nothing needed to be tracked because
  everything was reachable. This is the intended trade-off: the friction of
  defining a narrow contract is the mechanism that makes a module boundary
  real instead of aspirational.

## Alternatives considered
- **Extract Accounts/Customers together with BankLinks in this same phase** —
  rejected: this session's decision was one module per phase, verified green
  before moving on (ADR-0001); Accounts/Customers extraction is a materially
  bigger change (it's the module every other entity currently references) and
  deserves its own reviewable phase.
- **Give BankLinks a direct `ProjectReference` to `TransactionAggregation.
  Application` and use `IApplicationDbContext` directly** — rejected: this is
  exactly the shared-context coupling ADR-0009 exists to make a compile error.
  It would also invert the intended dependency direction — `Application`
  already depends on `Modules.BankLinks` (for `IAccountProvisioningPort` and
  `IBankLinksReadApi`), so `Modules.BankLinks` depending back on `Application`
  would be a circular project reference.
- **Keep `customer.AddAccount(...)` as-is and only move `BankLink` itself** —
  rejected: relocates the entity without removing the coupling that made the
  original audit flag it; `Modules.BankLinks` would still need a reference to
  `Customer`/`Account`/`IApplicationDbContext` to call it.
