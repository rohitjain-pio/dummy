# ADR-0009 — Aggregate invariants are serialised, not constrained

- **Status:** Accepted
- **Date:** 2026-09-15
- **Phase / task:** P7, CT-701 (closing U29)
- **Supersedes / amends:** nothing; extends `04-data-model.md` §4 (concurrency policy)

## Context

`04-data-model.md` §4 documents four concurrency races and resolves every one of them the same
way: a unique or partial-unique index, with the application check kept only to produce a clear
error message. The section states this explicitly — "under concurrent writes, the DB constraint is
the actual source of truth".

The learner-capacity gate (BRL-073, `ERR-801`) does not fit that pattern, and §4 does not mention
it. The rule is `COUNT(active students in institution) <= plan.LearnerCapacity`. That is an
invariant over an **aggregate**, not over a single row's identity, and no index or check
constraint in SQL Server can express it. So the pattern §4 relies on everywhere else is
unavailable here, and the code was left as a plain check-then-act: count, decide, insert.

U29 was raised against this during P5 and carried open through the P5 and P6 gates. It was never
detected by a test because every test host in the suite runs on the EF Core InMemory provider,
which has no isolation levels and no locking — the race cannot occur there and therefore cannot be
observed there.

Measured on real SQL Server at CT-701, with an Entry-plan institution holding 199 of its 200 seats
and eight concurrent admissions: **all eight were created, leaving 207 active learners against a
capacity of 200**, with no error raised to anyone. The tier ceiling is a commercial boundary, so
this was a silent seven-seat giveaway, repeatable on demand.

## Decision

**An invariant over an aggregate is enforced by serialising the check with the write it guards.**

`ISerializableTransaction` (Application) / `SerializableTransaction` (Infrastructure) runs the
capacity check and the insert inside one `IsolationLevel.Serializable` transaction. Under
SERIALIZABLE, SQL Server holds range locks for the `COUNT`, so a competing admission cannot insert
into the counted range until the first transaction commits. Applied to both paths that consume a
seat: `StudentService.EnrolAsync` and `StudentService.ReactivateAsync`.

Three consequences were accepted deliberately:

1. **Deadlocks are expected, and retried here.** SERIALIZABLE buys phantom-safety with deadlocks,
   and the first real run produced one immediately: two admissions each hold a range S lock and
   each then wants an X lock to insert, so SQL Server picks a victim (error 1205). A deadlock
   victim is not a business answer — the caller asked whether there was room, and "you were
   deadlocked" does not answer that — so the victim is retried, up to 5 attempts with a short
   linear backoff, clearing the change tracker between attempts.
2. **The retry is scoped to this class, not to the connection.** `EnableRetryOnFailure` on the
   `UseSqlServer` call would have been the idiomatic EF answer and would also cover transient cloud
   SQL faults, but it changes retry behaviour for *every* query in the product. That is a
   deployment-wide decision belonging to `12-infrastructure-spec.md` and its named owner, and
   enabling it as a side effect of closing U29 would have been a silent architectural change.
   **Routed upstream instead.**
3. **It is a no-op on a non-relational provider.** On InMemory the work runs directly, because
   there is nothing to serialise. This is why U29's proof is a real-SQL-Server test
   (`CapacitySerialisationTests`) rather than another InMemory one — an InMemory test here would
   pass against both the fixed and the broken code and would prove nothing.

## Consequences

- The rule now holds under concurrency: 8 concurrent admissions at the last seat produce exactly
  1 created, 7 × `ERR-801`, and a final count of exactly 200. Verified by mutation — remove the
  serialisation and the same test reports 8 created and 207 active.
- Admission and reactivation now take a stricter lock than the rest of the product. Contention is
  per-institution and confined to the two seat-consuming operations, so it does not touch the
  NFR-022 attendance peak path.
- **A pattern, not a one-off:** any future rule of the form "count/sum across rows must stay within
  a limit" belongs in this transaction, not behind a constraint. The storage-allowance gate
  (FR-073, unbuilt, U32) is the next one that will need it.
- `04-data-model.md` §4 should gain a fifth bullet describing aggregate invariants. Raised as
  upstream feedback rather than edited here, because the data model is a Gate-2 artifact.
