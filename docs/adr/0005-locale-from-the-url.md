# 0005 — The review page's locale comes from the URL

**Status:** accepted

## Context

A campaign runs in a market. The reviewer opens their link from an email, often on a device set to
a different language, sometimes with no session at all — the link *is* the credential, and it is
the first request the portal ever sees from that person.

Browser language detection would render a German campaign in English for a reviewer whose laptop
is English, which is exactly backwards: the campaign's market is the fact, the device's setting is
noise.

## Decision

The locale is a route segment (`/de-DE/northwind/review/{id}`), and `useReviewLocale` resolves it
from the route, normalizing case because the tag may be hand-typed. Unsupported locales fall back
to the default rather than failing — a campaign in a market the portal does not serve still has to
render something.

i18next does the translating, with two bundles.

## Consequences

- The emailed link fully determines what the reviewer sees. It can be forwarded, re-opened weeks
  later, or pasted into a different browser, and it renders identically.
- Adding a market is a bundle plus an entry in `SUPPORTED_LOCALES`.
- The session's stored locale is a convenience for the UI shell only; it never overrides the route.
