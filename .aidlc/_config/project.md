# Project Config — School Management System

> **Per-project config.** Org-wide standards live in `company.md` — only this project's facts
> and any *explicit deviations* are recorded here.

---

## 1. Project identity
- **Name:** School Management System
- **One-line purpose:** Cloud-based, subscription-delivered, multi-tenant platform for educational institutions to manage students, staff, classes, attendance, communication and academics from one role-aware workspace.
- **Status / target date:** In BRD/design phase — Stage 1 (BRD) complete, Gate 1 pending approval.
- **Repository:** D:\Projects\Migration\ClaudeCode\AIDLC\Test Plugin Manual\School Management System (no remote configured yet)

## 1a. Team mode  *(decides how the gates behave)*
> The pipeline's strongest safeguard is that the reviewer is not the author. With one person
> doing every stage that safeguard has to be recreated deliberately, so state which case you
> are in — the coordinator changes its behaviour accordingly.

- **team_mode:** `team`

| | `team` | `solo` |
|---|---|---|
| Stage gates | approved by the named role in §5 | you approve your own work — the coordinator will say so out loud at each gate and list what it could *not* independently check |
| Code review | may run in the same session | **must** run in a fresh session with no build context — the reviewer gets the diff, the specs and the CT row, never the conversation that produced the code |
| Phase close | reviewer + operator | you, after `scripts/verify-gate.sh` passes — the script substitutes for the second pair of eyes on the mechanical checks |

## 2. Archetype (what the application IS)
> Drives how every agent reasons. Pick the closest and describe it. Choose this project's stack from
> `company.md` §2's approved radar — anything outside it is a Tech Design Decide/Spike, not a silent add.
- **Type:** Full-stack app — builds its own frontend, backend, database and identity/authorization from scratch.
- **Stack (this project's slice of the radar):** C# / .NET (ASP.NET Core Web API) backend; React (TypeScript) frontend; SQL Server as the primary datastore; hosted on Microsoft Azure. Messaging/real-time mechanism (needed for chat and live meetings, per BRD §8) is not yet chosen — Tech Design proposes it as a Decide/Spike.
- **What it BUILDS vs what it CONNECTS to:** Builds everything end-to-end — institution/user/student/staff/class/attendance/announcement data model, RBAC and authentication, dashboards and reporting, collaboration rooms/messaging, and live audio/video meetings. Does not connect to any pre-existing institutional system (BRD explicitly excludes data migration from an incumbent system). Live audio/video meetings will likely connect to a third-party WebRTC/media provider — to be decided at Tech Design.
- **Key components:** Institution management, Identity/Access (RBAC), Student records, Staff records, Class/Section/Subject management, Attendance, Announcements, Collaboration (rooms/messaging/meetings), Dashboards/Reporting, Scheduling, Institution configuration, Commercial/Subscription management.

## 3. Domain glossary
> Project-specific terms, acronyms, entities. (Org-wide terms are in `company.md` §9.)

| Term | Meaning |
|------|---------|
| Institution | A subscribing school/tenant operating on the platform as the top-level business entity (also referred to as "school" or "tenant" in early drafts — "Institution" is the canonical term). |
| Platform Operator | The platform-side role (also called "Super Administrator") that onboards institutions, manages subscriptions and operates across all tenants. |
| Institution Administrator | Institution-side leadership role (Administrator / Principal / School In-charge) governing that institution's configuration, master data and reporting. |
| Room | A named collaboration space (passcode-gated) supporting real-time messaging and live audio/video meetings. |
| Roll number / register number | A student's institution-scoped and class-scoped unique identifiers respectively. |

*(Full BRD glossary lives in `.aidlc/01-brd-analysis/brd-analyst/final-brd.md` §16 — this table carries only the terms most load-bearing for design decisions.)*

## 4. Compliance regime
> `company.md` §8 sets *no* org-wide regime — declare this project's here, or it defaults to "treat all client data as confidential."
- **Applicable regime(s):** None specifically declared. Per the BRD's own resolution (Q-C2), no named data-protection regulation/jurisdiction governs this release; falls back to `company.md` §8's "treat all client data as confidential" default.
- **Sensitive data handled:** PII of learners, guardians and staff (names, contact details, date of birth, addresses, employment details). No payment/financial data is processed in this release (fee/payroll processing is out of scope per the BRD).
- **Project-specific data rules:** 7-year retention for student/guardian and staff personal records after departure/termination; 90-day export grace period on subscription end, then archival, then secure deletion/anonymisation — per `.aidlc/01-brd-analysis/brd-analyst/final-brd.md` §11 (CMP-001/002/003).

## 5. Stakeholders & roles
> Names map to pipeline roles; the role→assignee map for the tracker is in §6.

| Role | Person | Notes |
|------|--------|-------|
| BA (Stage 1 BRD) | Jayesh Idnani | |
| Architect (Stage 2 Tech Design) | Jayesh Idnani | |
| Lead (Stage 3 AI Specs) | Jayesh Idnani | |
| Dev (Stage 4 Build) | Jayesh Idnani | |
| Reviewer (Stage 5 Code Review) | Jayesh Idnani | Same person as author in a `team_mode: team` config — flagged here for visibility; consider a second reviewer before the Code Review gate if genuine independent review is wanted. |

## 6. Tracker binding  *(required for sync — choose ONE)*
> Set `tracker.type` to `jira`, `azuredevops`, or `deferred`. This must match
> `aidlc-state.json.tracker.type`. Until bound, work items accumulate in
> `.aidlc/_state/work-items.jsonl` and nothing is pushed (that is safe).

**tracker.type:** `azuredevops`

### Status lifecycle (canonical — used by both trackers)
The ledger speaks a tracker-agnostic status vocabulary; each sync skill maps it to that
board's native workflow states. Defaults below; override per board in the `status_map`
of your chosen binding if your board uses custom states.

| Canonical (ledger) | Meaning | Jira (default) | Azure DevOps Agile | DevOps Scrum |
|--------------------|---------|----------------|--------------------|--------------|
| `new`         | created, not started        | To Do        | New        | New        |
| `in_progress` (a.k.a. `active`) | actively being worked | In Progress | Active | Committed |
| `hold`        | blocked / paused            | In Progress *(+ `blocked` label/flag)* | Active *(+ `blocked` tag)* | Committed *(+ `blocked` tag)* |
| `future`      | planned, not yet in scope   | To Do *(+ `future` label)* | New *(+ `future` tag)* | New *(+ `future` tag)* |
| `completed`   | done & gate-approved        | Done         | Closed     | Done       |
| `removed`     | cancelled / dropped         | Won't Do     | Removed    | Removed    |

> Jira and DevOps Agile have no native *Hold* / *Future* state, so those are represented
> as the nearest active/new state **plus a tag/label** — unless your board defines custom
> states, in which case set them in `status_map`.

### 6b. Azure DevOps
- **Organization:** zvoo1
- **Project:** Test AI Framework
- **Process:** Agile
- **Epic id:** *(leave blank — the sync skill creates it and writes it back)*
- **Work item type map** — only two types are used (Agile process):
  ```yaml
  work_item_types:
    epic:  Epic
    story: User Story
  ```
- **Role → assignee map** (UPNs / emails, not display names):
  ```yaml
  role_assignees:
    BA:        jayesh.idnani@programmers.ai
    Architect: jayesh.idnani@programmers.ai
    Lead:      jayesh.idnani@programmers.ai
    Dev:       jayesh.idnani@programmers.ai
    Reviewer:  jayesh.idnani@programmers.ai
  ```
- **Status map:** *(not overridden — using Agile defaults from the canonical table above)*
  ```yaml
  status_map:
    new:         New
    in_progress: Active
    hold:        Active        # + the sync stamps a `blocked` tag
    future:      New           # + the sync stamps a `future` tag
    completed:   Closed
    removed:     Removed
  ```

## 7. External integrations & vendor sandboxes
> Live vendors are sandbox-only in non-production (`company.md` §8). List each vendor + its sandbox.

| Vendor / system | Used for | Sandbox / test environment details |
|-----------------|----------|------------------------------------|
| n/a | — | No third-party vendor integration is confirmed yet. Live audio/video meetings (BRD §8) will likely require a WebRTC/media provider — to be identified and sandboxed at Tech Design. |

## 8. Approved deviations from company defaults
> Every deviation from `company.md` must be recorded here with a reason, or it's a review finding.

| company.md section | Default | This project does instead | Reason / approver |
|--------------------|---------|---------------------------|-------------------|
| — | — | No deviations recorded yet. | — |

## 9. Other project notes
- Source BRD: `docs/SMS-BRD(RJ) 2.md` (copied in, retaining its original filename, as `.aidlc/00-input/SMS-BRD(RJ) 2.md`).
- UI reference mockup: `docs/school-management-ui(RJ) 2.html` (copied in, retaining its original filename, as `.aidlc/00-input/mockups/school-management-ui(RJ) 2.html`) — input for the UI-enrichment stage.
- Stage 1 (`brd-analyzer`) ran as a single unphased delivery — no scope slicing (`.aidlc/01-brd-analysis/brd-scope/scope-decision.md`). `BRD_Final.md` and `requirements.yaml` are produced; all Critical/High/Medium gap questions answered and attributed to Jayesh Idnani (`.aidlc/01-brd-analysis/brd-analyst/_questions.md`). Gate 1 is pending formal approval through the orchestrator.
- One BRD item (parent-to-student account association mechanism, FR-017) is explicitly deferred to Tech Design rather than decided at BRD level — see `.aidlc/01-brd-analysis/brd-analyst/final-brd.md` §15.
