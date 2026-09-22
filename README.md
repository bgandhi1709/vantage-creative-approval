# Vantage Works — creative approval

A small production-shaped system for getting client sign-off on campaign creative.

An agency producer assembles a round of creative for a campaign and sends it to the client. The
client opens a link from their email — no account, no password — reads the round in the campaign's
own language, and either approves it or asks for changes. Producers work the same campaigns from an
internal console. Every round that goes out and every answer that comes back is snapshotted to blob
storage, and the campaign's stage is advanced in the upstream system that schedules the work.

It is deliberately small: one product flow, two locales, one upstream system. What it is *for* is
the shape — a browser-facing BFF, a storage library with an explicit contract, orchestration whose
side-effect order is a documented promise, and tests that run against a real storage emulator.

The sibling service, [`vantage-freight-hub`](https://github.com/bg199117/vantage-freight-hub),
handles the same company's exhibition freight. The two share a brand and nothing else — no shared
database, no shared contract.

---

## Run it

Requires Docker. Nothing else, and no cloud account.

```bash
docker compose -f docker/docker-compose.yml up --build
```

| What | Where |
| --- | --- |
| Portal | http://localhost:8081 |
| Producer console | http://localhost:8081/console |
| Captured email | http://localhost:1080 |
| API health | http://localhost:8081/api/health/ready |

A full walkthrough, end to end:

```bash
B=http://localhost:8081/api

# 1. A producer signs in. Only addresses in the configured domain get a token.
TOKEN=$(curl -s -X POST $B/auth/producer-token -H 'Content-Type: application/json' \
  -d '{"email":"producer@vantage.test"}' | jq -r .accessToken)

# 2. Create round 1 for a campaign, in German.
REVIEW=$(curl -s -X POST $B/v1/northwind/reviews -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' -d '{
    "campaignId":"'$(uuidgen)'","campaignName":"Spring launch",
    "reviewerEmail":"client@northwind.test","reviewerName":"Client Reviewer",
    "locale":"de-DE","assets":[{"name":"Hero banner","format":"1080x1920","previewPath":"previews/hero.png"}]
  }' | jq -r .reviewId)

# 3. Send it. Watch http://localhost:1080 for the message.
curl -s -X POST $B/v1/northwind/reviews/$REVIEW/submit -H "Authorization: Bearer $TOKEN" | jq .status

# 4. Open the emailed link in a browser, or exchange it for a session by hand and approve.
curl -s -c jar -X POST $B/auth/reviewer-session -H 'Content-Type: application/json' \
  -d "{\"reviewId\":\"$REVIEW\"}"
curl -s -b jar -X POST $B/v1/northwind/reviews/$REVIEW/decision \
  -H 'Content-Type: application/json' -d '{"outcome":"Approved","comment":"","assetIds":[]}' | jq .status
```

Developing without containers:

```bash
dotnet test VantageApprovals.slnx                    # needs `npm i -g azurite` on PATH
cd apps/approval-portal && npm ci && npm run dev     # Vite on :5173, proxying /api
```

---

## How it fits together

```
 browser
    │  every call goes to /api on the same origin
    ▼
 nginx (web container)          ─── /healthz
    │  strips /api, forwards over 127.0.0.1
    ▼
 ASP.NET Core API (api container)
    │       │            │              │
    │       │            │              └── campaign system (stage advance, idempotent)
    │       │            └── SMTP (review notification)
    │       └── blob storage (immutable snapshots: notification, review, decision)
    └── table storage (4 aggregates + 2 denormalized indexes)
```

The two containers share one network namespace, in Compose and in the deployed Container App
revision alike, so the proxy hop is real loopback in both.

| Path | What lives there |
| --- | --- |
| `apps/approval-portal` | React 18 + Vite SPA: the client review page and, under `/console`, the producer console as a nested app with its own store and HTTP client |
| `apps/approval-api` | ASP.NET Core 10: controllers, orchestration, auth, the upstream client |
| `libs/approval-core` | `Core` — models and repository contracts; `Repository` — the Table Storage implementations |
| `docker` | Dockerfiles, the nginx template, the local stack |
| `infra/container-apps` | Bicep for the two-container revision and its monitoring |
| `docs/adr` | Why the load-bearing decisions are the way they are |

## Worth reading first

- `Services/CreativeReviewService.cs` — the submit path. Its six steps run in a fixed order, and
  the comment above them says why each one sits where it does. ([ADR 0001](docs/adr/0001-side-effect-order.md))
- `Services/StageTransitionService.cs` — why that un-transactional sequence is safe to retry.
- `libs/approval-core/.../AssignmentIndexSync.cs` — a table store has no joins, so the answer is
  written at write time. ([ADR 0002](docs/adr/0002-denormalized-indexes.md))
- `Vantage.Approvals.Api.Tests/TableStorage/AzuriteTableFixture.cs` — the repository tests run
  against a real emulator, and the doc-comment lists the three bugs a fake would hide.
  ([ADR 0003](docs/adr/0003-tests-against-a-real-emulator.md))
- `src/core/http/bffClient.ts` and the console's own client — two credentials, two error types, two
  storage namespaces, one SPA. ([ADR 0004](docs/adr/0004-two-sessions-in-one-spa.md))

## What it does not do

Listed in [ADR 0007](docs/adr/0007-what-this-demo-omits.md), so nobody has to guess whether an
omission was a decision or an oversight: one product flow, two locales, a local identity and mail
stub instead of vendors, one upstream system, and no deployment pipeline beyond the templates.

## Licence

MIT. See [LICENSE](LICENSE).
