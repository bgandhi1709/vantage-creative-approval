# 0007 — What this project deliberately leaves out

**Status:** accepted

Omissions people would otherwise have to guess about. Each one is a decision, not an oversight.

| Left out | Why | What it would take |
| --- | --- | --- |
| A second product flow | One flow shows the layering; a second only shows it twice | A second service and controller pair alongside the first |
| More than two locales | Two proves the plumbing is real; four proves nothing extra | A bundle and one array entry per market |
| A real identity provider | The stack has to run with no vendor account | Implement `ISessionVerifier` against the provider; the handlers and controllers stay as they are |
| A real email provider | Same reason; the local stack catches mail instead | Replace `SmtpNotificationSender` — one class |
| A real upstream campaign system | It is someone else's service; the local stack stubs the two calls the API makes | Point `CampaignSystem__BaseUrl` at the real one |
| A deploy pipeline | The Bicep and the CI workflow are here and compile; deploying needs a subscription | A workflow that builds, pushes and runs `az deployment group create` |
| An asset pipeline | Reviews reference preview paths rather than storing binaries | Blob upload plus SAS-scoped reads |
| Optimistic concurrency on the review row | One producer edits a round at a time; ETag handling here would be ceremony | ETag round-tripping through the repository contract |

Two things that are *not* omissions, because they are the point: the ordered side effects
([0001](0001-side-effect-order.md)) and the emulator-backed storage tests
([0003](0003-tests-against-a-real-emulator.md)).
