# Documentation

- [Architecture](architecture.md) — system context, container/module diagram, event flow, ERD, key sequence diagrams, deployment diagram
- [Architecture Decision Records](adr/README.md) — why the modular monolith has no broker, no Saga, no partitioning, offset pagination, etc.
- [Threat model](threat-model.md) — STRIDE-based analysis of ingestion, the API, provider integrations, the database, Redis, and secrets
- [Failure scenarios](failure-scenarios.md) — detection/response/recovery/consistency/observability/user-impact for the 19 named failure modes
- [Data retention](data-retention.md) — open compliance/legal questions this system has not yet answered
- [Production readiness checklist](production-readiness-checklist.md) — must-have / should-have / future-enhancement status across security, reliability, performance, testing, deployment, and more
- [Performance testing](../perf/README.md) — k6 script, assumptions, and targets for the main read endpoints
