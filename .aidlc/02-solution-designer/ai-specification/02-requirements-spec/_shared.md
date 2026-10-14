<!-- pipeline-provenance
produced_by: ai-spec-agent (source-analyser)
generated: 2026-09-11
-->

# 02 — Requirements Spec — `_shared` (cross-cutting NFR/CMP)

Platform-wide, non-feature-scoped requirements. Every feature file in this folder references the
relevant rows here by ID rather than restating them.

## Usability

| SPEC ID | Source | Statement | Acceptance criteria |
|---|---|---|---|
| SPEC-NFR-001 | NFR-001 | Role-aware navigation, only entitled destinations shown | Given a signed-in user of role R, the rendered nav contains only routes R is entitled to per `09-security-spec.md` §2; no other route link is rendered, even hidden/disabled |
| SPEC-NFR-002 | NFR-002 | Core daily tasks completable in ≤3 steps | Marking a full class register = open roster → toggle/bulk-mark → save (3 steps); publishing an announcement = New → fill → save (3); joining a meeting = open room → Join → (optional) Start Video (≤3) |
| SPEC-NFR-003 | NFR-003 | Confirmation before destructive actions; explicit non-technical success/failure feedback | Every delete (institution withdraw, student/teacher/class/room/announcement) shows a named confirmation modal before proceeding; every mutation shows a toast on success or a plain-language inline/toast error on failure |
| SPEC-NFR-004 | NFR-004 | Meaningful empty/loading states | Every list/detail/dashboard screen has a designed empty state and loading state per `11-ui-spec/` — never a blank screen or infinite spinner |
| SPEC-NFR-005 | NFR-005 | WCAG 2.1 AA | Automated axe/Lighthouse a11y scan passes AA on every built screen; manual keyboard-nav + screen-reader spot check on Attendance grid, Auth modal, Video call controls |

## Performance

| SPEC ID | Source | Statement | Acceptance criteria (numeric target — `target_source: sourced`) |
|---|---|---|---|
| SPEC-NFR-006 | NFR-006 | List/detail views < 2s | p95 response time for `GET` list/detail endpoints < 2000ms under normal load (see `13-test-spec` performance scenarios) |
| SPEC-NFR-007 | NFR-007 | Dashboard summaries < 3s | p95 `GET /api/v1/dashboard` < 3000ms |
| SPEC-NFR-008 | NFR-008 | Chat delivery < 1s | p95 SignalR message fan-out to all present participants < 1000ms |
| SPEC-NFR-009 | NFR-009 | Meeting audio/video conversational quality for supported participant count | Manual QA + the 5 carried-forward ACS risks (Tech Design §13.1) validated before the meeting feature is marked done |

## Reliability

| SPEC ID | Source | Statement | Acceptance criteria |
|---|---|---|---|
| SPEC-NFR-010 | NFR-010 | 99.5% availability during institutional working day | Ops/SLO concern — see `12-infrastructure-spec.md` §SLO; not a code-level test |
| SPEC-NFR-011 | NFR-011 | No submitted attendance/announcement/message data lost | Every write is transactional; no fire-and-forget write path for these three entities; TC- adversarial fixture simulates a mid-write failure and asserts no partial/silent-drop row |
| SPEC-NFR-012 | NFR-012 | Interrupted collaboration sessions reconnect automatically, restore history | SignalR auto-reconnect + REST re-fetch of message history on reconnect (Tech Design §3.4); meeting UI shows a "reconnecting" state, never silently drops the tile |
| SPEC-NFR-013 | NFR-013 | Daily backup, documented recovery objective | Ops concern — `12-infrastructure-spec.md` §Backup/DR |

## Security

| SPEC ID | Source | Statement | Acceptance criteria |
|---|---|---|---|
| SPEC-NFR-014 | NFR-014 | AuthN required for all business functions except public info/aggregate figures | Every non-`/api/v1/public/*` endpoint returns 401 with no token; negative-authorization matrix in `09-security-spec.md` §5 |
| SPEC-NFR-015 | NFR-015 | RBAC enforced on every function | Every endpoint annotated with an authorization policy; no endpoint reachable by a role not in its policy (`09-security-spec.md` §2) |
| SPEC-NFR-016 | NFR-016 | Absolute tenant isolation except the bounded public-directory exception | Cross-tenant read/write blocked at the data layer (EF Core global query filter) for every role except PlatformOperator's named path; adversarial TC- proves cross-tenant fetch returns empty/404, not another tenant's row |
| SPEC-NFR-017 | NFR-017 | Session credentials protected in browser, encrypted transport | TLS 1.2+ enforced; JWT never stored in `localStorage` readable by injected script without HttpOnly/secure cookie or equivalent XSS-hardening — see `09-security-spec.md` §6 |
| SPEC-NFR-018 | NFR-018 | Personal data encrypted in transit/at rest | Azure TDE + Storage Service Encryption (ops-level, `12-infrastructure-spec.md`); TLS in transit (code-level, enforced at App Service) |
| SPEC-NFR-019 | NFR-019 | Collaboration rooms gated by passcode | `09-security-spec.md` §4, BR-041 |

## Scalability

| SPEC ID | Source | Statement | Acceptance criteria |
|---|---|---|---|
| SPEC-NFR-020 | NFR-020 | Many institutions concurrently | Load test at N=50 simulated institutions, no cross-tenant leakage, no shared-resource contention beyond expected DB load |
| SPEC-NFR-021 | NFR-021 | <200 to unlimited learners per tier | Tier capacity enforcement per BRL-062 blocks over-capacity enrolment (FR-073) |
| SPEC-NFR-022 | NFR-022 | Absorbs attendance-day peaks | Bulk-upsert attendance write (single batch per class, D10) validated under simulated peak load (see `14-test-spec` performance scenario) |
| SPEC-NFR-023 | NFR-023 | Storage grows within tier allowance | Storage-quota check on report/blob writes, same blocking pattern as FR-073 |

## Compatibility

| SPEC ID | Source | Statement | Acceptance criteria |
|---|---|---|---|
| SPEC-NFR-024 | NFR-024 | Usable on current major desktop browsers | Manual/automated cross-browser smoke pass (Chrome/Edge/Firefox/Safari current) |
| SPEC-NFR-025 | NFR-025 | Responsive desktop/tablet/mobile | Every screen tested at `bp-sm`/`bp-md`/`bp-lg`/`bp-xl` breakpoints per `design-tokens.md` §5 |
| SPEC-NFR-026 | NFR-026 | Meetings on browsers w/ standard camera/mic | ACS Calling SDK browser compatibility risk — Tech Design §13.1 item 4 |
| SPEC-NFR-027 | NFR-027 | Reports exportable in common formats | PDF + CSV export verified per report type (D7) |

## Auditability

| SPEC ID | Source | Statement | Acceptance criteria |
|---|---|---|---|
| SPEC-NFR-028 | NFR-028 | Create/amend/remove of student/staff/class/attendance/announcement attributable+timestamped | Every such mutation produces exactly one `AuditLog` row via the `SaveChanges` interceptor (D9); TC- adversarial fixture attempts a direct write bypassing the interceptor and fails/is structurally impossible |
| SPEC-NFR-029 | NFR-029 | Announcement authorship/date retained permanently | `AuditLog` + `Announcement.AuthorId`/`EffectiveDate` never nulled/overwritten |
| SPEC-NFR-030 | NFR-030 | Messages remain attributable after revision/withdrawal | `Message.AuthorId` immutable; revision/withdrawal creates a new state, never deletes the author link (BR-044) |
| SPEC-NFR-031 | NFR-031 | Sign-in/out/permission-denials logged | Auth middleware writes an `AuditLog` row (not via `SaveChanges` interceptor — no entity change) for each event |
| SPEC-NFR-032 | NFR-032 | Institution withdrawal/subscription changes recorded, reversible | `AuditLog` row + `IsActive`/`DeactivatedAtUtc` pattern (D8); reactivation path exists |
| SPEC-NFR-033 | NFR-033 | Audit history retained per institution retention period, not end-user-editable | No `UPDATE`/`DELETE` endpoint exists for `AuditLog`; retention sweep (Hangfire, §3.5 Tech Design) is the only process that ever removes rows, per CMP-001/002 |

## Compliance (carried verbatim — never re-worded)

| ID | Statement | Enforcement point |
|---|---|---|
| CMP-001 | 7-year retention: Student/Guardian post-departure; Staff post-termination | Retention sweep job (`12-infrastructure-spec.md` §Jobs); `07-business-logic/_shared.md` §Retention |
| CMP-002 | 90-day export grace period on subscription end → archive → delete/anonymise | Same job; triggered off `Institution.DeactivatedAtUtc` |
| CMP-003 | Secure deletion/anonymisation on retention expiry; audit logs may be anonymised, not deleted; deletion itself is audited | Same job; deletion event writes its own `AuditLog` row (satisfies NFR-033's "recorded" requirement even as data is removed) |

## Traceability note

Every SPEC- row above traces to exactly one `NFR-`/`CMP-` ID in `requirements.yaml` and is
referenced from feature files via the SPEC ID. No feature file restates NFR wording — it cites
the ID and, where relevant, adds a feature-specific enforcement note.
