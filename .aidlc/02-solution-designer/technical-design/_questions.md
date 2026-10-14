<!-- pipeline-provenance
produced_by: tech-design-agent
plugin_version: 0.3.0 (sdlc-greenfield-v3)
model: claude-sonnet-5
generated: 2026-09-11
phase: 1 (worksheet — Decide/Spike items requiring architect input before Phase 3)
-->

# Tech Design Worksheet — Decide / Spike Items

Phase 1 output. Everything decidable myself is in `_decisions.md` (Stated Decisions) and will
not be re-asked here. What follows are the **genuine forks** — items that shape the data model,
the auth/permission model, an integration posture, or carry a real cost/vendor commitment — plus
one item (`T-003`) that the BRD itself explicitly hands to Tech Design (FR-017).

**Count:** 6 Decides (`T-001`–`T-006`), 1 Spike (`SP-001`, which gates `T-006`). Well under the
~15 re-audit threshold — this reflects a real full-stack build with a genuinely small number of
external forks, not under-escalation.

Each item below states my recommended default. Answer inline (accept the recommendation, or
override) and I will carry the answer verbatim into `final-tech-design.md`.

**Reformatted 2026-09-11** from the old sdlc-greenfield-v3 layout's free-form heading/bold-field
convention (`## T-001 — ...` / `**Answered by:**`) into the current `### Q-NNN · Class · Topic` /
lowercase-field convention, so `questions-sheet.mjs` can parse it. No question, recommendation, or
answer text was altered — only the structural markup changed. Each block's original short ID
(T-001, SP-001, ...) is kept in its Topic field for traceability.

---

### Q-001 · Blocking · T-001 — Real-time transport for chat, presence and live dashboard/attendance updates
severity: high
status: answered
answered_by: Jayesh Idnani
question: >
  Accept SignalR + Azure SignalR Service, or specify an alternative.
why: >
  company.md §2 explicitly states messaging/real-time is not org-standardized and must be proposed
  as a Decide/Spike — this is a direct instruction, not my own hedge. It shapes the backend's
  application-logic layer (FR-045–050: messaging, presence, typing indicators, join/leave notices)
  and the integration contracts a media/meeting vendor (T-006) will sit behind.
recommendation: >
  SignalR (ASP.NET Core's native real-time library) for chat messages, typing indicators, presence
  and join/leave events (FR-045–050), backed by Azure SignalR Service in "Default" mode for the
  multi-instance/scale-out backplane (so App Service can scale horizontally under NFR-022's
  attendance-day traffic peaks without sticky-session issues).
context: >
  Native fit for ASP.NET Core (no separate protocol/library to integrate); Azure SignalR Service
  removes the need to self-host a Redis backplane; a raw WebSocket implementation or a message
  broker (RabbitMQ/Azure Service Bus) would duplicate what SignalR already provides for this
  specific need (small, in-app real-time fan-out), and none is otherwise required by the BRD (no
  other async/eventing need was found).
answer: >
  Accepted as recommended — SignalR + Azure SignalR Service.

---

### Q-002 · Blocking · T-002 — Multi-tenancy data isolation model
severity: high
status: answered
answered_by: Jayesh Idnani
question: >
  Accept shared-DB + discriminator column, or require per-tenant database/schema isolation (e.g.
  if a future compliance regime is anticipated).
why: >
  This is the single most expensive-to-unwind decision in the whole design — it shapes every table
  in the data model (§5) and is the mechanism behind NFR-016's "absolute isolation" guarantee.
  Getting it wrong is not cheap to fix once tables and queries exist across 201 requirements' worth
  of entities.
recommendation: >
  Shared database, discriminator-column multi-tenancy — every tenant-owned table carries an
  institution_id (except the small set of genuinely platform-global tables: Subscription Plan
  catalogue, the public directory projection). Enforced structurally via EF Core global query
  filters keyed off the signed-in user's institution_id claim (see D4 in _decisions.md), not
  per-query discipline. The Platform Operator role is the only one permitted to bypass the filter,
  and only via an explicitly-marked cross-tenant query path (BR-013).
context: >
  Database-per-tenant or schema-per-tenant give stronger physical isolation but do not match this
  system's shape — NFR-021 states institutions range from under-200-learner schools to large
  districts, i.e. many small tenants, which is the classic case where per-tenant databases become
  an operational/cost burden (migrations, connection pooling, cross-tenant platform reporting for
  FR-053) without a stated compliance driver (project.md §4: no named regulatory regime) that would
  demand physical separation. Shared-DB with a structural query filter is the standard, defensible
  choice at this tenant profile.
answer: >
  Accepted as recommended — shared database, institution_id discriminator column, EF Core global
  query filters.

---

### Q-003 · Blocking · T-003 — Parent-to-Student account association mechanism (resolves FR-017)
severity: high
status: answered
answered_by: Jayesh Idnani
question: >
  Accept this linking-code mechanism, or specify a different one (e.g. parent self-registration by
  matching student register number + date of birth, which is weaker and guessable; or an admin-only
  manual link with no parent-facing code at all, which removes self-service but is simpler).
why: >
  The BRD explicitly defers this to Tech Design (FR-017, §15) — it is not a re-opened business
  question, it is the one item the BRD itself assigned to me. It is squarely data-model- and
  auth-shaping (who can read which student's records), so per the router's Step B it is escalated
  rather than quietly assumed.
recommendation: >
  A linking-code mechanism, mirroring the pattern the BRD already uses for room passcodes
  (FR-043/044): (1) when institution staff enrol a student (FR-011) or at any later point, staff can
  generate a single-use Parent Link Code against that student record (a short, random, time-limited
  token — e.g. 8 characters, 7-day validity, regenerable if unused/expired); (2) staff communicate
  the code to the parent out-of-band (the same pattern already accepted for room passcodes — no new
  communication channel invented); (3) a user who self-registers with role = Parent (FR-006) enters
  the code during or after registration; the platform validates it (unused, unexpired, matches an
  institution) and creates a ParentStudentLink (many-to-many — one parent can link multiple
  children, and BR-066 already anticipates "linked children" plural); (4) a consumed code cannot be
  reused; staff can revoke an unconsumed code or unlink a parent-student pair later (institution
  staff retain control, consistent with BR-010's staff ownership of student records); (5) no
  additional identity-proofing (e.g. government ID matching) is introduced — this stays inside the
  BRD's existing "self-register + immediate access, no approval step" posture (FR-006) rather than
  adding a safeguarding-adjacent verification the BRD never asked for.
context: >
  Reuses an access-control pattern the BRD already ratified (passcode-gated access) rather than
  inventing a new one; keeps the mechanism entirely inside the platform (no email/SMS verification
  vendor dependency beyond what's already needed for T-004); is symmetrical with FR-016/FR-023's
  "optionally link" pattern for staff-initiated linkage of teacher/student records to user accounts.
answer: >
  Accepted as recommended — staff-generated, single-use, time-limited Parent Link Code mechanism,
  mirroring the room-passcode pattern.

---

### Q-004 · Blocking · T-004 — Notification delivery vendor(s) for email and SMS (resolves FR-065)
severity: high
status: answered
answered_by: Jayesh Idnani
question: >
  Accept Azure Communication Services for email + SMS, or name a different vendor (e.g. SendGrid
  for email / Twilio for SMS, if there's an existing commercial relationship or deliverability
  requirement I'm not aware of).
why: >
  company.md §2 has no standardized notification vendor; this is a real external vendor/cost
  commitment (per-message SMS cost scales with institution count and is a genuine budget line), not
  a code-only choice.
recommendation: >
  Azure Communication Services for both email and SMS channels, with in-platform alerts delivered
  via the SignalR channel from T-001.
context: >
  Single vendor, same Azure billing/identity surface as the rest of the stack (no separate
  SendGrid/Twilio account, API key, and compliance review to manage); ACS Email and ACS SMS are
  both GA services suitable for transactional (non-marketing) send, which matches FR-065's use case
  (announcements, attendance/report notifications) rather than marketing email.
answer: >
  Accepted as recommended — Azure Communication Services for both email and SMS.

---

### Q-005 · Blocking · T-005 — Background/async job framework
severity: high
status: answered
answered_by: Jayesh Idnani
question: >
  Accept Hangfire in-process, or require Azure Functions/queue-based processing (e.g. if
  institution/report volume is expected to be far higher than NFR-021 implies).
why: >
  Several BRD-required operations are inherently batch/long-running rather than request/response:
  report generation (FR-054/055), notification dispatch (FR-065), academic-year rollover
  (FR-067/068 — promoting an entire student roster), and the CMP-001/002 retention/deletion sweep.
  Choice of mechanism affects the backend's Infrastructure layer and Azure hosting topology (App
  Service always-on vs a separate worker).
recommendation: >
  Hangfire, self-hosted inside the same ASP.NET Core App Service process (SQL Server storage
  provider — no extra infrastructure to stand up), for all four job types above.
context: >
  Azure Functions would add a second deployable, a second CI/CD target and a second set of
  secrets/config to manage for a workload that isn't bursty enough to need serverless elasticity
  (rollover is once a year, per-institution; reports and notifications are modest volume at this
  tenant profile per NFR-021). Hangfire keeps everything in one deployable and one SQL Server
  instance already in the stack.
answer: >
  Accepted as recommended — Hangfire in-process, SQL Server storage provider.

---

### Q-006 · Blocking · T-006 — Live audio/video meeting vendor (gated by SP-001)
severity: high
status: answered
answered_by: Jayesh Idnani
question: >
  Confirm ACS Calling, or select an alternative surfaced by the spike (e.g. Twilio Video, Daily.co,
  Vonage Video API, or a self-hosted SFU such as mediasoup/LiveKit if cost-at-scale or ACS
  participant-count limits make it unsuitable). Do not design past this until SP-001/Q-007 reports
  back, per the router's stop condition on an unanswered Decide gated by a Spike. (Note: the spike
  itself was waived by explicit operator decision — see the answer below.)
why: >
  project.md §7 explicitly flags this as unresolved and needing identification at Tech Design; it
  is a real commercial vendor commitment (per-minute or per-participant billing) with meaningfully
  different cost, browser-support and recording/compliance postures across candidates. This cannot
  be decided the same way as T-001 (pure library choice) because it carries a running cost and a
  capability trade-off that a spike should verify before committing.
recommendation: >
  Leaning candidate (to validate in the spike, not yet decided): Azure Communication Services
  Calling SDK — same-vendor consistency with T-004's ACS choice and the rest of the Azure-hosted
  stack; browser-based (matches NFR-026: "browsers providing standard camera/microphone access," no
  native app required, consistent with "no native mobile apps" being out of scope).
answer: >
  Accepted as the leaning candidate — Azure Communication Services Calling SDK — without running
  the full research spike. The operator elected to commit now rather than delay on research; the
  five SP-001/Q-007 validation points (participant cap, tier-pricing fit, no-recording posture,
  browser compatibility, reconnection behavior) are carried forward into final-tech-design.md as
  explicit assumptions/risks to validate during implementation, not as closed findings.

---

### Q-007 · Blocking · SP-001 — Spike: live meeting vendor evaluation (gates T-006/Q-006)
severity: high
status: deferred
answered_by: Jayesh Idnani
question: >
  Investigation needed: (1) confirm Azure Communication Services Calling SDK's supported
  concurrent-participant count per meeting against NFR-009 ("conversational quality for the
  supported participant count") — the BRD does not state a hard cap, so establish a realistic
  default (e.g. a per-room participant cap) and confirm ACS supports it at acceptable quality; (2)
  confirm pricing model (per-participant-minute) against the three subscription tiers
  (BR-061/062) — verify meeting minutes can plausibly be included in or metered against tier
  allowances without the platform operator absorbing unbounded cost from the entry tier; (3)
  confirm recording/consent posture is not silently introduced — the BRD's collaboration section
  (§6.8) describes live meetings but does not mention recording; default to no recording capability
  in this release unless the spike finds it's unavoidably bundled and must be disabled; (4) confirm
  browser compatibility matches NFR-024/026 (current major desktop browsers, standard camera/mic
  APIs) without a required native SDK/app; (5) sanity-check reconnection behavior against NFR-012
  (interrupted sessions reconnect automatically and restore conversation history) — this applies
  cleanly to the SignalR-based chat (T-001) already; confirm it also holds for the media session
  itself, or state the gap.
why: >
  Gates T-006/Q-006 — a real commercial vendor commitment with cost, browser-support and
  recording/compliance trade-offs that a spike would normally verify before committing.
owner: >
  Architect (Jayesh Idnani), before Phase 3 finalizes §6 (Integration Contracts) and §4
  (Frontend meeting UI, since the SDK choice shapes the client-side meeting component).
answer: >
  Waived by explicit operator decision on 2026-09-11 — see T-006/Q-006's Final Answer. Not run as a
  live research spike; its five points are carried into final-tech-design.md as named, owned
  assumptions/risks (owner: Architect) to validate during Stage 3/4, not resolved here.

---

## Notes on what is deliberately NOT on this worksheet

- **Password/session/message-length figures (BR-014/015/045):** decided in `_decisions.md`
  (D1–D3) — conventional, cheap-to-change technical figures, not forks.
- **Multi-tenancy enforcement mechanism, RBAC claims model:** decided in `_decisions.md` (D4) —
  the *isolation model* itself (`T-002`/Q-002) is the fork; the claims-based enforcement mechanism
  once that model is chosen is not.
- **Frontend framework/build tooling, backend layering, report formats, audit logging shape:**
  all conventional defaults for this stack — see `_decisions.md` D5–D14.
- **Compliance regime / data-retention duration:** already settled at BRD level (CMP-001/002/003)
  — not re-opened here.
- **Deployment topology, CI/CD, scaling, DR, IaC:** out of scope for Tech Design per the
  charter; will appear in the Final doc's §15 "Out of Scope — Deployment & Operations" with a
  named owner, not designed here.

## Open item carried forward (not a Decide/Spike — a dependency notice)

`_questions.md` from Stage 1 (BRD analysis) left the BRD's own priority-table confirmation open
(BRD §15, first bullet). That is a Gate 1 matter for the orchestrator/BA, not a Tech Design item —
noting it here only so it isn't lost, not asking it again.
