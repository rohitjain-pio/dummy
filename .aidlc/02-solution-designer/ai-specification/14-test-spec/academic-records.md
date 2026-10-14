<!-- pipeline-provenance
produced_by: ai-spec-agent (test-spec-writer)
generated: 2026-09-11
-->

# 14 — Test Spec — `academic-records`

| TC ref | Layer | Given | When | Then | Trace | Adversarial? |
|---|---|---|---|---|---|---|
| TC-301 | Integration | Valid student payload, existing class | `POST /students` | 201 | SPEC-FR-011 | — |
| TC-302 | Unit | Existing SR number in institution | New student submitted with same SR number | `ERR-301` | BRL-018 | **Yes** — fails against an implementation that checks roll number only, not SR number |
| TC-303 | Integration | Teacher is class-teacher of Class A | Assigned as class-teacher of Class B (without releasing A) | `ERR-303` | BRL-023 | **Yes** — fails against an implementation that allows multiple class-teacher assignments |
| TC-304 | Integration | Teacher holds an active class-teacher assignment | `POST /teachers/{id}/remove` | `ERR-304`, teacher not deactivated | BRL-025 | **Yes** — fails against an implementation that deactivates and orphans the assignment |
| TC-305 | Unit | Code issued, unconsumed, unexpired | Parent submits correct code, matching institution | `ParentStudentLink` created, code marked consumed | PF-005 happy path | — |
| TC-306 | Unit | Code already consumed | Same code submitted again | `ERR-310` | Decision table (academic-records.md) | **Yes** — fails against an implementation with no consumed-state check |
| TC-307 | Unit | Code issued 8 days ago (`CFG-005` = 7-day expiry) | Parent submits it | `ERR-308` | T-003 | **Yes** — fails against an implementation with no expiry check |
| TC-308 | Integration | Code issued by Institution A | Parent registered under Institution B submits it | `ERR-309` | T-003, institution-match guard | **Yes** — fails against an implementation that skips the institution-match check |
| TC-309 | E2E | Staff enrols student, issues code, parent registers and redeems it | Full PF-005 flow | Parent's Dashboard/Attendance screens now show the linked child | FR-017, FR-075 | — |
