<!-- pipeline-provenance
produced_by: ai-spec-agent (test-spec-writer)
generated: 2026-09-11
revised: 2026-09-15 — spec_suite_version 1.0.4 (narrow patch, U11). **TC-410's boundary corrected**
  from day 30 to day 31 (it asserted the wrong side of the `>30` lock and would have failed correct
  code); TC-409 extended to day 30; TC-413/414 added for the twice-enforced lock and `Superseded`.
  All four describe behaviour CT-202 already built and tested.
revised: 2026-09-15 — spec_suite_version 1.0.5 (narrow patch, m1). **Text-only:** TC-406's trace
  still read "day 1–29", the window U11 corrected to 1–30. No `TC-` added, renumbered or
  re-scoped; no code change is implied and no `CT-` becomes stale.
-->

# 14 — Test Spec — `attendance`

| TC ref | Layer | Given | When | Then | Trace | Adversarial? |
|---|---|---|---|---|---|---|
| TC-401 | Integration | Fresh roster, 30 students | `GET` roster for today | Every row `currentOutcome: null` (NotYetMarked) | BRL-034 | **Yes** — fails against an implementation that defaults to "Absent" (the mockup's own defect, corrected) |
| TC-402 | Integration | Teacher marks 30 students in one bulk request | `PUT /attendance` | One transaction, 30 rows created, one audit row per entry | SPEC-FR-030, NFR-028 | — |
| TC-403 | Unit | Existing record for (student, today) | Same (student, today) submitted again with a different outcome | Row updated in place, no duplicate row | BRL-031, D10 | **Yes** — fails against an implementation using plain INSERT |
| TC-404 | Integration | Teacher submits attendance for tomorrow's date | `PUT /attendance` | `ERR-403` | VAL-014 | — |
| TC-405 | Unit | Record dated today | Teacher self-corrects | Direct update, no `CorrectionPending` created | State machine, day-0 branch | — |
| TC-406 | Unit | Record dated 5 days ago | Teacher requests correction | `CorrectionPending` created, not applied yet | State machine, day 1–30 branch (**corrected 1.0.5, m1** — U11 moved the correction window to 1–30 in `07-business-logic/attendance.md` and in TC-409/410, and missed this trace) | **Yes** — fails against an implementation that applies the Teacher's day-5 edit directly |
| TC-407 | Integration | `CorrectionPending` request exists | Principal approves | Record updated, request closed | API-034 | — |
| TC-408 | Unit | Record dated 31 days ago | Any role attempts correction | `ERR-401`, no `CorrectionPending` created | BRL-035, CFG-004 | **Yes** — fails against an implementation using `>=30` instead of `>30`, or that creates a pending request anyway |
| TC-409 | Unit | Records dated exactly 29 **and exactly 30** days ago | Non-leadership requests correction | `Pending` request created in **both** cases (still within the window) | Decision table boundary, Q-214 | **Yes** — boundary test, fails against an off-by-one implementation. *Day 30 added at 1.0.4 (U11): the window is 1–30, not 1–29.* |
| TC-410 | Unit | `Pending` request open on a record that reaches **day 31** | Daily `attendance-lock-sweep` runs | Request `AutoRejected`, record Locked, **record outcome unchanged**. At **day 30** the same sweep leaves the request alone and the record correctable | Q-206, Q-214, PF-007 | **Yes** — **corrected at 1.0.4 (U11).** The prior wording said "reaches day 30", which asserts the *wrong* boundary: the lock is `> 30`, so day 30 is still correctable and **day 31** is the first locked day. A test written to the old wording would have failed correct code. Also fails against an implementation with no sweep. |
| TC-413 | Unit | `Pending` request open, record **just** past the lock, **sweep has not run** | Leadership approves | `ERR-401` **and the request is auto-rejected in that same call** — the lock is re-checked at the moment of action, so a missed sweep cannot admit a stale approval | Q-212 ("both"), E2 | **Yes** — added 1.0.4 (U11). Fails against an implementation that relies on the sweep alone |
| TC-414 | Unit | A Teacher's request is `Pending` | Leadership writes a **direct** correction to the same record | The request closes as **`Superseded`**; a later approve attempt cannot overwrite leadership's newer outcome | E4, §2.6a | **Yes** — added 1.0.4 (U11). Fails against an implementation that leaves the request `Pending` |
| TC-411 | Unit | Two correction requests submitted for the same record before the first resolves | Second submitted | Rejected — only one `CorrectionPending` per record | E3 | **Yes** |
| TC-412 | Integration | Leadership submits a day-5 correction on their own recorded entry | Submitted | Applied immediately per Q-205 (confirmed final rule, no self-approval loop) | Decision table | — |
