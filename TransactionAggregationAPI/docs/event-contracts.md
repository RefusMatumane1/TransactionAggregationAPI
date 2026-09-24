# Event contracts and versioning

Two kinds of messages cross a boundary where the producer and the consumer can be at
different versions:

1. **Inbound provider deliveries.** The REST webhook body and the Kafka
   `bank-transactions` record value use one wire format.
2. **Outbox messages.** Integration events written in the same transaction as the change
   they describe, and dispatched later by the worker. During a rolling deploy, a new API
   pod can write a message that an old worker pod reads.

Both are versioned explicitly. Neither relies on "every reader was deployed first".

## 1. Inbound: `BankTransactionsMessage`

Source: `Transactions.Application/Features/Transactions/Commands/ReceiveBankTransactions/BankTransactionsMessage.cs`.
The same record binds the webhook body and deserialises the Kafka value, so the two
channels cannot drift apart.

```json
{
  "schemaVersion": 1,
  "externalAccountId": "acc_123",
  "transactions": [
    {
      "id": "txn_abc",
      "amount": -150.00,
      "currency": "ZAR",
      "description": "Woolworths",
      "category": "Groceries",
      "date": "2026-09-10T12:00:00Z",
      "status": "posted"
    }
  ]
}
```

| Field | Rules |
|---|---|
| `schemaVersion` | Optional; omitted means **1**, so senders written before the field existed keep working. Must be in `SupportedSchemaVersions`, otherwise 400 (webhook) or dead-letter (Kafka). |
| `externalAccountId` | Required. Applied only to bank links at institutions the sending source is authorized for ([ADR-0013](adr/0013-webhook-source-institution-scoping.md)). |
| `transactions` | 1–500 items. |
| `id` | Required. Unique **per institution**. The storage key is `(CustomerId, SourceName, SourceExternalId)`. |
| `amount` | Non-zero, at most 4 decimal places (stored as `numeric(19,4)`, so 3-decimal currencies such as KWD, BHD and JOD are exact). |
| `currency` | 3-letter ISO 4217 code. |
| `date` | Any ISO 8601 form. Values without an offset are interpreted in the institution's configured zone (`normalization-rules.json`). |
| `status` | `pending` or `posted` (default). A later `posted` settles a stored pending row in place. |

**Delivery identity.** The `Idempotency-Key` header (webhook) or `idempotency-key` header
(Kafka) identifies a delivery; without one, the SHA-256 of the canonical payload is used.
Reusing a key for **different content** is refused (422 on the webhook, dead-letter on
Kafka) instead of being acknowledged as a duplicate, because acknowledging it would drop
the new data silently.

## 2. Outbox messages

Every row in `messaging."OutboxMessages"` carries `Type` and `SchemaVersion` (default 1).
The dispatcher's `OutboxSchemaVersions` lists the highest version it can read for each
type. A newer version is **dead-lettered, not misread**.

| Type | Version | Payload | Consumers (in-process) |
|---|---|---|---|
| `TransactionCreated` | 1 | `{ transactionId, customerId }` | analytics, notifications |
| `TransactionCategorized` | 1 | `{ transactionId, customerId, oldCategory, newCategory, isAutoCategorized }` | cache invalidation, analytics, notifications |
| `TransactionSynced` | 1 | `{ transactionId, customerId, syncSource }` | cache invalidation, analytics |
| `TransactionsExpired` | 1 | `{ customerIds[], count }` | cache invalidation |
| `DuplicateInboundDetected` | 1 | `{ level, sourceName, externalAccountId, duplicateExternalIds[], inboxMessageId?, customerId?, detectedAt }` | duplicate alert webhook |
| `AuditEvents` | 1 | `{ events: AuditEventRecord[] }` | Audit module (idempotent by `eventId`) |

All event names are past-tense facts. Payloads carry ids, not entities: consumers re-read
current state when they need it, so a delayed or replayed message never applies stale data.

## Evolution rules

1. **Additive changes stay in the same version.** A new optional field, for example.
   Readers ignore unknown fields (System.Text.Json default), and writers must not
   require new fields from old messages.
2. **Anything else is a new version.** That covers renames, removals, type or meaning
   changes, and newly required fields. Ship it in this order:
   1. Teach the reader the new version, keeping the old one readable. For inbound, add it
      to `SupportedSchemaVersions`; for outbox, raise `OutboxSchemaVersions`.
   2. Deploy the reader everywhere.
   3. Start writing the new version.
   4. Remove the old reader only once no message of that version can still exist (for
      the outbox, after dispatch plus the retry window; for inbound, after every
      provider has migrated).
3. **Never mutate a published message.** Corrections are new messages.
4. Contract tests pin the wire shapes: `Contract/ApiResponseContractTests`,
   `Unit/Application/Commands/InboundContractValidationTests` and
   `Unit/BackgroundServices/FailureClassificationTests` (newer outbox version → dead-letter).
