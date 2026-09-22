# 0002 — The queries are written at write time

**Status:** accepted

## Context

Table Storage has one index: partition key, then row key. There are no joins, no secondary
indexes, and a query that does not name a partition scans the table.

The product asks two questions constantly:

- "What is the latest round for these campaigns?" — the console's work list.
- "What is outstanding with this client?" — the client's own view.

Neither is answerable from the review table without a scan, because that table is keyed by review
id for point reads.

## Decision

Keep the canonical assignment row keyed by its own id, and maintain two index tables whose
partitions are shaped like the questions: one partitioned by campaign, one by client code. Each
index row carries a copy of the version, sent date and active flag, so the answer never requires a
second read. The by-campaign index uses an inverted version as its row key, so "latest" is the
first row of the partition.

Every write to an assignment goes through `AssignmentIndexSync`, and the canonical row is written
before the indexes.

## Consequences

- The reads the product makes are single-partition and bounded. They stay that way as the table
  grows.
- The cost is a class that must not be bypassed. A write that skips it leaves an index disagreeing
  with the row it describes — invisible in the data, visible later as a campaign that never leaves
  the work list. That failure is real enough that the decision path has its own test.
- Row first, indexes second, so an interrupted write leaves an index stale rather than pointing at
  a row that does not exist. The next write repairs it.
