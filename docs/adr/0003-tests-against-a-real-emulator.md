# 0003 — Repository tests run against Azurite, not a fake

**Status:** accepted

## Context

An in-memory dictionary behind the repository interfaces would make the storage tests fast, quiet
and dependency-free. It would also pass unconditionally, because the bugs this layer actually
produces are produced by the *service*, not by the code around it.

Three of them:

- `DateTimeOffset` values come back normalized to UTC. A local-time write round-trips to a
  different value than it was given.
- Partition and row keys are compared case-sensitively, unlike a relational `uniqueidentifier`. A
  GUID written in one format is simply not found when read in another.
- ETag concurrency is enforced by the service, not by the client library.

A dictionary honours none of those and reports success for all three.

## Decision

`AzuriteTableFixture` starts a real `azurite-table` process per test run: a free TCP port, a temp
data directory, a start-up probe with a 20 second deadline, and a process-tree kill on dispose. The
repository tests run against it, and CI installs Azurite so they run there too rather than being
the tests that only ever pass locally.

`TableKeys` exists for the same reason: one formatter for every key, with the case-sensitivity note
attached to it, and a test that reads a row back using a differently-cased GUID.

## Consequences

- The test run needs `azurite` on PATH. The fixture says so in its failure message.
- A test run costs a process start, a few hundred milliseconds. Worth it.
- Service-level tests still use mocks — the boundary being tested there is orchestration, not
  storage semantics.
