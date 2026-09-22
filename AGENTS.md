# AGENTS.md

The reference behind `CLAUDE.md`: how a request actually travels, what each flow does, how it
deploys, and what is knowingly unfinished.

## Flows

### A producer sends a round

1. `POST /auth/producer-token` — mints a bearer token for an address inside the allowed domain.
   The domain gate is the whole authorization model for the console; a real deployment moves it
   into an identity provider (`ISessionVerifier` and `IProducerTokenFactory` are the seams).
2. `POST /v1/{clientCode}/reviews` — `CreativeReviewService.CreateDraftAsync` reads the by-campaign
   index for the previous version, writes the review row at `version + 1`, then writes the
   assignment (which syncs both indexes). Status `Draft`.
3. `POST /v1/{clientCode}/reviews/{id}/submit` — the ordered path. Snapshot the notification →
   update the row → snapshot the round → send → advance the stage. See `docs/adr/0001`; the order
   is asserted by `SubmitAsync_RunsItsSideEffectsInTheContractedOrder`.

The review URL in the email is built from `Request.Scheme` and `Request.Host`, which is why
`UseForwardedHeaders` is enabled and why the nginx template keeps inbound `X-Forwarded-*` headers
when an edge proxy set them. Get that wrong and the emailed link names the container.

### A client answers

1. The link lands on `/{locale}/{clientCode}/review/{reviewId}`. The page exchanges the review id
   for a session cookie (`POST /auth/reviewer-session`), scoped to that one round.
2. `GET /v1/{clientCode}/reviews/{id}` — the controller rejects a client code that does not match
   the round, so a valid cookie cannot read another client's work by editing the URL.
3. `POST /v1/{clientCode}/reviews/{id}/decision` — the controller checks the cookie's review claim
   against the route before delegating. Then: snapshot the decision → write the decision row →
   update the review row → close the assignment when approved → advance the stage.

Approval sets `IsActive = false` on both the review and its assignment. Only the second one is
visible to the console's work list, which is why it has its own test.

### Stage transitions

`StageTransitionService` re-reads the upstream state for every campaign, skips a stage that is
already open, and skips (with a warning, not an exception) a campaign whose predecessor stage is
missing — the upstream system owns its own lifecycle, and a campaign somebody moved by hand should
not fail a request here. This idempotence is what makes the un-transactional ordering above safe to
retry.

## Storage

Four aggregates, six tables.

| Table | Partition key | Row key | Notes |
| --- | --- | --- | --- |
| `reviews` | review id | review id | Point reads only. Assets stored as JSON. |
| `reviewdecisions` | review id | inverted ticks + decision id | Newest first without a sort. |
| `reviewnotes` | review id | ticks + note id | Internal notes filtered server-side. |
| `reviewassignments` | assignment id | assignment id | Canonical row. |
| `reviewassignmentsbycampaign` | campaign id | inverted version | Index: latest round per campaign. |
| `reviewassignmentsbyclient` | client code | assignment id | Index: the console's work list. |

Blob snapshots live at `{clientCode}/{reviewId}/v{version}/{notification,review,decision}.json`.
Forward slashes only — blob names are flat strings, and a backslash produces a path that looks
nested in code and is not nested anywhere else.

Tables are created lazily at the head of each repository method, so a cold environment works with
no provisioning step.

## Local stack

`docker/docker-compose.yml` runs Azurite, MailDev, an nginx stub for the campaign system, the API,
and the portal. The portal container uses `network_mode: service:api`, so nginx reaches the API
over real loopback — the same shape as the deployed revision, which is the only way the forwarded
header handling behaves the same in both.

Ports: portal and API on 8081 (both published from the API container because of the shared
namespace), API directly on 8080, MailDev UI on 1080, Azurite on 10000/10002.

## Deployment

`infra/container-apps/main.bicep` provisions Log Analytics, Application Insights, a Container Apps
environment, and one app with two containers in a single revision. The storage account is
*referenced*, not created: it outlives the app, and its keys are resolved at deploy time with
`listKeys()` so no connection string is ever passed in as a parameter value. Secrets are `@secure()`
parameters read from the environment by the `.bicepparam` files.

Readiness points at `/health/ready`, which asserts every required configuration key. A revision
missing a setting never takes traffic.

There is no deploy pipeline in this repository — see `docs/adr/0007`. CI builds, tests, compiles
the Bicep (template and every parameter file) and runs a filesystem vulnerability scan, on pull
requests as well as `main`.

## Known gaps

| Gap | Why it is acceptable here | What it would take |
| --- | --- | --- |
| No optimistic concurrency on the review row | One producer edits a round at a time | ETag round-tripping through the repository contract |
| Notes have no pagination | A round collects a handful of notes | Continuation tokens through `IReviewNoteRepository` |
| The campaign-system stub always reports `InProduction` | Enough to exercise the real transition logic locally | A richer stub, or a contract test against the real service |
| No end-to-end browser test | The walkthrough in the README is manual | Playwright against the Compose stack in CI |
| Asset previews are paths, not files | Keeps the demo free of binary handling | Blob upload plus SAS-scoped reads |

Add to this table rather than leaving a gap undocumented; an unlisted gap reads as an oversight.
