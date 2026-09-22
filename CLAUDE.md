# CLAUDE.md

Working rules for coding agents in this repository. `AGENTS.md` holds the longer reference —
request flows, deployment shape, and the open list. Read this for the rules and the commands; read
that when a task needs the detail behind them.

## Working model

Understand, change the smallest thing that solves it, prove it, write down what changed.

1. **Define** — restate the problem and name the layer it belongs to: portal, console, API,
   storage library, or infrastructure.
2. **Investigate** — read the code that runs, plus its configuration. Evidence over assumption.
3. **Trace** — follow the whole path: SPA → `/api` → controller → service → repository → storage,
   plus blob, notification and upstream call.
4. **Plan** — find the cause, not the symptom, and decide how you will prove the fix before you
   type any of it.
5. **Check in** before any of:
   - a route, request/response contract, or persisted shape changes
   - the submit or decision side-effect order changes (see `docs/adr/0001`)
   - authentication, authorization, or session scoping changes
   - a table, partition scheme, or index changes
   - a dependency is added or upgraded
   - anything destructive: history rewrites, bulk deletes, deployments

   Everything else — small, reversible, inside the task you were given — just do.
6. **Implement** — the smallest reviewable slice.
7. **Validate** — run the thing. A green build is not evidence that the behaviour changed.
8. **Document** — update this file, `AGENTS.md`, or an ADR in the same change when behaviour,
   ownership, or a repeated rule moves.

## Commands

```bash
# .NET
dotnet build VantageApprovals.slnx -c Debug
dotnet test  VantageApprovals.slnx -c Debug              # needs `npm i -g azurite` on PATH
dotnet test  apps/approval-api/Vantage.Approvals.Api.Tests/Vantage.Approvals.Api.Tests.csproj \
  --filter "FullyQualifiedName~CreativeReviewServiceTests"

# Portal (Node 22 — see .nvmrc)
cd apps/approval-portal
npm ci && npx tsc -b --noEmit && npm test && npm run build
npm run dev                                              # Vite on :5173, proxying /api

# Local stack
docker compose -f docker/docker-compose.yml up --build
docker compose -f docker/docker-compose.yml down -v

# Infrastructure
az bicep build --file infra/container-apps/main.bicep --stdout > /dev/null
```

## Layering rules

- Controllers validate and delegate. Orchestration lives in `Services/`, never in a controller and
  never in the portal.
- The storage library is a data access layer, not an orchestration layer. `Core` holds models and
  repository contracts with no infrastructure dependency; `Repository` holds the Table Storage
  implementations, and it is the only assembly that knows storage exists.
- Repository methods are named for the question they answer. No generic `IRepository<T>`, no
  `IQueryable` crossing the boundary, no unit of work.
- Portal feature `api/` modules are thin `bffClient` wrappers: no React, no state, no rules. Pages
  orchestrate; components take props and raise typed callbacks.
- The producer console keeps its own store, HTTP client, error type and storage namespace. Never
  reuse a client-facing storage key there, or the reverse.

## Invariants

- The submit and decision side-effect order is a contract. `docs/adr/0001` explains each step; a
  test asserts the sequence. Changing it is a decision, not a refactor.
- Every campaign list is iterated. Never index into the first element.
- Every identifier used as a storage key goes through `TableKeys`. Table Storage compares keys
  case-sensitively.
- Every write to an assignment goes through `AssignmentIndexSync`, or an index will disagree with
  the row it describes.
- Enums are serialized by name at every boundary, and enum members are appended, never reordered.
- The review page's locale comes from the route, never from the browser or the session.
- A void call can answer 204 or 200 with nothing in it. `bffClient` reads both as success and
  only parses a body it actually received.
- Configuration is validated on start. A revision missing a setting must fail readiness, not its
  first request.
- No secret is committed. The local stack's keys are throwaway strings for local use, and they are
  the only ones in this repository.

## Validation

| Changed | Run at least | Also run when |
| --- | --- | --- |
| Portal or console | `npx tsc -b --noEmit`, `npm test`, `npm run build` | contracts, auth, or i18n change |
| API | `dotnet build` plus a targeted `dotnet test --filter` | contracts, side-effect order, or auth change |
| Storage library | `dotnet test` (Azurite-backed) | key scheme, entity shape, or an index changes |
| Containers or nginx | `docker compose up --build` plus the README walkthrough | the proxy, forwarded headers, or the topology change |
| Bicep | `az bicep build` on the template *and* every parameter file | a parameter is added or renamed |
| Docs only | read for stale links and claims | — |

Report failures honestly: quote the command, say whether it was already failing before your
change, and list what did pass.
