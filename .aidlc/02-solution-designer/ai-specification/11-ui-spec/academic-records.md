<!-- pipeline-provenance
produced_by: ai-spec-agent (ui-spec-writer)
generated: 2026-09-11
-->

# 11 — UI Spec — `academic-records`

## UI-007 — Classes (`/dashboard/classes`)

**Roles:** leadership (full CRUD) + Teacher (read only — FR-027 correction).
**Layout:** card grid, one `ClassCard` per class (class-teacher avatar+name, room, student count,
attendance-rate, sparkline, `radius-lg` card, `elevation-card`). **For Teacher role: View/Edit/
Delete icon actions and "+ Add New Class" are hidden entirely**, not merely disabled
(improved-beyond-mockup — the mockup doesn't gate these by role at all; BR-029 makes this a real
authorization gap, not cosmetic). "Take Attendance"/"View Roster" quick actions shown to both.
**Modal (`ClassModal`):** name, section (VAL fields per FR-024), class-teacher assignment
(FR-026). Delete uses the shared confirmation pattern (destructive-action modal, NFR-003).
**State matrix:** empty ("No classes yet") · loading (skeleton cards) · success · error (BRL-026
duplicate on save → inline field error `ERR-305`; BRL-028 on delete → toast `ERR-306`).
**API:** API-026–028. **Trace:** FR-024–027, BR-026, BR-028, BR-029.

## UI-008 — Teachers (`/dashboard/teachers`)

**Roles:** leadership only (matches the mockup's own gating — no BRD grant to Teacher for a peer
directory).
**Layout:** staff-by-subject bar chart + presence donut → searchable/filterable (name/email
search, subject filter) `DataTable` (avatar, name+email, subject, phone, gender, address, status
pill, view/edit/delete) with pagination + CSV export.
**Modal (`TeacherModal`):** name, email, phone, gender, address, joining date (FR-018) +
class-teacher/subject assignment (FR-021/022).
**State matrix:** empty ("No teachers found — adjust your search or filters") · loading (skeleton
rows) · success · error (BRL-025 on remove with active assignments → `ERR-304`).
**API:** API-020–025. **Trace:** FR-018–023, BR-022–025.

## UI-009 — Students (`/dashboard/students`)

**Roles:** leadership (full CRUD), Teacher (view only).
**Layout:** enrolment-by-class bar chart + status-breakdown donut → searchable
(name/email/SR-number) + class-filterable `DataTable` (SR number, roll number, name+email, class,
DOB, gender, status pill, view/edit-if-leadership/delete-if-leadership) with pagination + export.
**Modal (`StudentModal`):** SR number, roll number, name, DOB, gender, email, class placement
(FR-011) + guardian block (father's/mother's name, guardian phone/email — BR-021 sensitivity,
same access rule as the row itself, so the guardian block is never rendered to a role that
couldn't see the student row in the first place).
**Parent-link affordance:** a "Generate parent link code" action on the student detail view
(leadership only) surfaces the code + expiry (FR-017/T-003, PF-005) — code is shown once,
copyable, with a "regenerate" action that invalidates the prior one.
**State matrix:** empty ("No students found...") · loading · success · error (BRL-018 duplicate
→ `ERR-301`).
**API:** API-011–019. **Trace:** FR-011–017, FR-074, BR-017–021.

## Cross-cutting — Student/Parent access boundary (all three screens above)

Student and Parent roles never reach these three screens at all (not present in their nav subset,
`11-ui-spec/public-auth.md` §UI-006) — their equivalent views are the Dashboard's narrower variant
(`11-ui-spec/operations.md` §UI-015) and the Attendance screen's own/linked-children scope
(`11-ui-spec/attendance.md`).
