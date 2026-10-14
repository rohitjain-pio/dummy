<!-- pipeline-provenance
produced_by: ai-spec-agent (source-analyser)
plugin_version: 0.3.0 (sdlc-greenfield-v3)
model: claude-sonnet-5
generated: 2026-09-11
inputs:
  - .aidlc/01-brd-analysis/brd-analyst/final-brd.md
  - .aidlc/01-brd-analysis/brd-analyst/requirements.yaml (214 IDs: 75 FR active + 13 FR excluded, 66 BR, 33 NFR, 3 CMP, 17 ENT, 7 ROLE)
  - .aidlc/02-solution-designer/technical-design/final-tech-design.md
  - .aidlc/02-solution-designer/ui-designer/design-tokens.md
  - .aidlc/02-solution-designer/ui-designer/ui-design-final.md
-->

# 01 — Source Catalogue

**This agent validates `requirements.yaml` as the confirmed spine; it does not re-author it.**
Every active requirement (`priority: must`, `priority_source: confirmed`) below is placed into
exactly one feature slug and carries forward into `02-requirements-spec/`. Excluded IDs
(`status: excluded`) are listed at §5 for completeness and are never built.

## 1. Placement check — every active requirement placed

**Result: PASS.** All 75 active `FR-`, all 66 `BR-`, all 33 `NFR-`, all 3 `CMP-`, all 17 `ENT-`
and all 7 `ROLE-` IDs are placed into a feature slug or the cross-cutting shard below. Zero
unplaced IDs. One item (`FR-017`) is `status: deferred` in `requirements.yaml` but is **now
resolved** by Tech Design §3.8/T-003 (`ParentLinkCode`/`ParentStudentLink`) — placed as active
design in `academic-records`.

## 2. Feature slug map (frozen — used by every sharded concern)

| Slug | BRD section(s) | FR- range | BR- range | Primary ENT- | Primary ROLE- |
|---|---|---|---|---|---|
| `institutions` | §6.1 | FR-001–005 | BR-001–006 | ENT-001 | ROLE-001, ROLE-002 |
| `identity-access` | §6.2 | FR-006–010 | BR-007–016 | ENT-002 | ROLE-001–007 |
| `academic-records` | §6.3, §6.4, §6.5, §6.13 | FR-011–029, FR-074–075, FR-017 (T-003 resolved) | BR-017–029, BR-065–066 | ENT-003, ENT-004, ENT-005, ENT-006, ENT-007 | ROLE-002–007 |
| `attendance` | §6.6 | FR-030–037 | BR-030–035 | ENT-008 | ROLE-002–005 |
| `communication` | §6.7, §6.8 | FR-038–051 | BR-036–047 | ENT-009, ENT-010, ENT-011, ENT-012 | ROLE-001–007 |
| `operations` | §6.9, §6.10 | FR-052–062 | BR-048–056 | ENT-013, ENT-014 | ROLE-002–005 |
| `configuration-commercial` | §6.11, §6.12 | FR-063–073 | BR-057–064 | ENT-015, ENT-016, ENT-017 | ROLE-001–004 |
| `_shared` (cross-cutting, NFR/CMP) | §10, §11 | — | — | — | all |

**Six concerns never shard** (`04`, `08`, `09`, `12`, `15`, `03`) — they hold all feature content
in one file regardless of the map above; the map governs `02`, `05`, `06`, `07`, `10`, `11`, `14`
only.

## 3. Joined catalogue — SPEC- ID assignment

Every active `FR-`/`NFR-` gets exactly one `SPEC-FR-###` / `SPEC-NFR-###` with the **same
number** as its source ID (`SPEC-FR-030` ⇄ `FR-030`) — chosen deliberately so no second mapping
table is ever needed to resolve one from the other. Full statement text is **not repeated here**
(that would create a second copy to drift) — `requirements.yaml` remains the single place the
requirement's words live; this catalogue only assigns placement and the SPEC- number.

| Source ID | SPEC ID | Slug | Design trace (Tech Design §) |
|---|---|---|---|
| FR-001–005 | SPEC-FR-001–005 | institutions | §2.2, §3.3, §7 |
| FR-006–010 | SPEC-FR-006–010 | identity-access | §3.3, §4.5, §8.1–8.3 |
| FR-011–016 | SPEC-FR-011–016 | academic-records | §5.7, §7 |
| FR-017 | SPEC-FR-017 | academic-records | §3.8, §5.7, §5.10 (T-003) |
| FR-018–029 | SPEC-FR-018–029 | academic-records | §5, §7 |
| FR-030–037 | SPEC-FR-030–037 | attendance | §3.6, §5.3, §5.9, §9 |
| FR-038–051 | SPEC-FR-038–051 | communication | §3.4, §3.9, §6, §8.4 |
| FR-052–062 | SPEC-FR-052–062 | operations | §5.4, §9(A2), §7 |
| FR-063–073 | SPEC-FR-063–073 | configuration-commercial | §3.5, §3.7, §5.6, §9(A4) |
| FR-074–075 | SPEC-FR-074–075 | academic-records | §3.8, §5.7, §8.5 |
| NFR-001–033 | SPEC-NFR-001–033 | `_shared` | §4, §8, §9, §2.4 |
| CMP-001–003 | (carried as `CMP-` directly, no `SPEC-` re-mint — compliance obligations are referenced verbatim, never re-worded) | `_shared` | §3.5, §8.6 |

## 4. Architecture-relevant flags carried to the profiler (§03)

- **ASYNC = yes.** SignalR real-time transport (chat/presence/typing, T-001) and Hangfire
  background jobs (reports, notifications, rollover, retention sweep, T-005) both exist. `06-event-contracts.md`
  is in scope.
- **UI = yes.** 20 screens in `ui-design-final.md`. `11-ui-spec/` is in scope.
- **LEGACY_DATA = no.** FR-088 (data migration from an incumbent system) is explicitly `excluded`.
  No `13-data-migration-spec.md` is produced.
- **DATA_PIPELINE = no. ML = no.** No such capability appears anywhere in the three inputs.
- **Multi-service = no.** One ASP.NET Core Web API + one React SPA (§2.1) — a modular monolith,
  not physically separate services. No per-service shard axis; the feature-slug axis above is
  the only sharding axis used.

## 5. Excluded requirements (never built — carried for traceability only)

`FR-076`–`FR-088` (13 IDs) — guardian consent capture, fee/leave/homework access for Student/
Parent, homework-based performance metrics, and the eight blanket Out-of-Scope items (fee/payroll,
leave workflow, biometric attendance, learning-content/grading, transport/hostel/library/
inventory, payment gateway, native mobile, data migration). **No spec file in this suite
references these as a capability to build.** Any inline mention elsewhere in this suite is
strictly to explain why an adjacent capability stops where it does (e.g. "Leave" attendance
outcome ≠ leave-workflow, FR-082).

**Two additional exclusions carried from the UI design hand-off — validation-required mockup
artifacts, not requirements, and therefore never SPEC'd:**

- Reports screen's "Student Satisfaction" and "Course Completion" gauges (`ui-design-final.md`
  §11, operator decision 2026-09-11) — no FR/BR traces to either metric; Q-H3 explicitly dropped
  engagement/homework-style metrics. **Omitted entirely from `11-ui-spec.md`'s Reports entry.**
- Pricing screen's (`ui-design-final.md` §1.3) per-tier feature-list copy in the plan cards and
  comparison table — the tier/capability *model* is BR-061/062 (built), but the specific feature
  claims in that copy are not cited anywhere in this suite as a specification of entitlement
  behaviour. The Pricing screen's *structure* (plan cards, toggle, comparison table, FAQ) is still
  built in `11-ui-spec.md` §UI-004 — only the copy's individual claims are excluded as evidence.

## 6. Open items surfaced during this pass

None Blocking. See `_questions.md` for non-blocking provisional choices carried forward into the
suite (each marked inline `[NEEDS ANSWER — Q-###]` where it appears).
