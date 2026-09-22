# 0006 — Enums are serialized by name, everywhere

**Status:** accepted

## Context

The same value crosses three boundaries: an HTTP payload to and from the upstream campaign system,
a table row, and a blob snapshot that is read back much later — sometimes by a person.

`System.Text.Json` writes enums as ordinals by default. Table rows here are written by name. Mixing
the two is not a bug until somebody inserts a member into the middle of an enum, at which point
every previously written ordinal means something else, and nothing fails loudly.

## Decision

One representation, by name, at every boundary: a `JsonStringEnumConverter` on the MVC serializer,
on the snapshot serializer, and on the contract enum itself; `ToString()` on the way into a table
row and a tolerant `Enum.TryParse` on the way out.

Enum members are appended, never reordered, and the comment on the status enum says so.

## Consequences

- Payloads are marginally larger and considerably more readable.
- A snapshot written today still reads correctly after the enum grows.
- A tolerant parse means an unknown stored value degrades to a documented default rather than
  throwing on read. That is the right trade for an audit artefact.
