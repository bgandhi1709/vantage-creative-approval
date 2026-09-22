# 0001 — The submit path's side-effect order is a contract

**Status:** accepted

## Context

Sending a round to a client touches four places: blob storage, table storage, an SMTP relay, and
the upstream campaign system. Nothing spans them transactionally, and nothing will: two are Azure
storage services, one is a mail relay, one is someone else's HTTP API.

Any order "works" on a good day. The orders differ only in what they leave behind on a bad one.

## Decision

`CreativeReviewService.SubmitAsync` runs exactly these steps, in this order, and the order is part
of the method's contract rather than an implementation detail:

1. **Read** the round. Nothing is written until the state is known to be legal.
2. **Snapshot the notification payload** to blob storage — before it is sent.
3. **Update the table row** to `AwaitingDecision` with the sent timestamp.
4. **Snapshot the round as sent.**
5. **Send the notification.**
6. **Advance the campaign stage**, per campaign, idempotently.

Two rules fall out of it:

- The only externally visible effect (the email) happens after everything that a retry can safely
  redo. A crash before step 5 leaves a round that looks sent with a snapshot proving what would
  have been sent; a crash after it leaves a sent message that is fully recorded. Neither leaves a
  client holding an email the system has no record of.
- No step is wrapped in a swallow-and-continue handler. A failure propagates to the caller, who
  retries, and every step is safe to repeat.

A unit test asserts the sequence by name. If the code changes, that test fails, and the failing
test is the prompt to re-read this document rather than to update the assertion.

## Consequences

- A retry can re-send a notification the client already received. That is the accepted failure: a
  duplicate email is a nuisance, an unrecorded approval is a dispute.
- Step 6 must be idempotent for any of this to hold, which is why `StageTransitionService` re-reads
  the upstream state and skips a stage that is already open.
- Reordering these steps is a change to the system's guarantees, not a refactor.
