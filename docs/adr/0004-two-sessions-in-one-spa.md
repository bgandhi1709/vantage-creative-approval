# 0004 — The console is a nested app, not a mode

**Status:** accepted

## Context

Two audiences share one SPA. A client reviewer arrives from an emailed link, holds a cookie scoped
to a single round, and has no account. A producer signs in with a work address, holds a bearer
token, and sees every campaign for a client — including internal notes.

One browser routinely holds both: a producer opening a client's link to check what they are seeing
is the normal case.

## Decision

The console is mounted as a nested application with its own Redux store, its own HTTP client, and
its own storage namespace, rather than as a flag on the shared ones.

- Storage keys are namespaced `vantage.console.*` and `vantage.reviewer.*`, and a test asserts the
  two sets stay disjoint.
- The console's client sends a bearer token and, on 401, raises a window event its own shell
  listens for instead of navigating. A redirect would discard whatever the producer was editing.
- The error types differ (`ConsoleHttpError` vs `BffHttpError`) so one side's failure can never be
  handled by the other's boundary.
- Console routes are registered before the locale-prefixed client routes, so `/console` can never
  be swallowed by the catch-all.

Server-side, the two schemes are composed per endpoint via
`[Authorize(AuthenticationSchemes = ...)]`, and the notes endpoint returns internal notes only to
the bearer-token caller — filtered in storage, not after the read.

## Consequences

- Some duplication between the two HTTP clients. It is deliberate: every line that differs is a
  line that would otherwise be a conditional on a shared client, and those conditionals are how
  one audience ends up seeing the other's data.
- A shared React Query client is fine, because it holds no identity.
