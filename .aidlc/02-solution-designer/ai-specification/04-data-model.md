<!-- pipeline-provenance
produced_by: ai-spec-agent (data-model-writer)
generated: 2026-09-11
inputs:
  - .aidlc/02-solution-designer/technical-design/final-tech-design.md §5
  - .aidlc/01-brd-analysis/brd-analyst/requirements.yaml (ENT-001..017)
revised: 2026-09-15 — spec_suite_version 1.0.4 (narrow patch, authorised by Rashmi Mehta at the
  Gate 4 CHANGES-REQUIRED verdict). Two kinds of change, kept distinct:
  (a) **NEW** — §2.12a `LeadershipCode` (Q-238 / U38 Critical), not yet built.
  (b) **WRITTEN BACK FROM THE BUILD** — §2.6a `CorrectionRequest` (U11), §2.7 `created_at_utc`,
      §2.8 Message revision/withdrawal/client-id and `Meeting` many-per-room, §2.8a
      `RoomMembership` (U15), §2.9 `ReportRun` lifecycle and `TimetablePeriod` publish state
      (U20), §2.1 trial/subscription fields, §2.5 `Class.next_class_id`, §2.10 auditing,
      §2.11 AcademicYear rollover state and `RolloverHoldBack` (U26). **These describe schema that
      P2–P5 already built, migrated and tested; the code is the source of truth for them and no
      code change is implied.** The §1 ERD is updated for both.
  **No ENT- was minted.** `LeadershipCode`, like `ParentLinkCode` and `RoomMembership`, is a
  mechanism table derived from a design decision, not a business entity — `ENT-` belongs to
  Stage 1 and is not this agent's to assign.
  **§4's optimistic-concurrency bullet is deliberately untouched** (U41/REV-003 are out of scope
  for this patch and have no named owner).
revised: 2026-09-15 — spec_suite_version 1.0.5 (narrow patch, authorised by Rashmi Mehta, closing
  the Mode D Round-2 audit against 1.0.4). Three changes, two classes:
  (a) **DEFECT FIXES (U45)** — §2.12a's `code_lookup` row is now `char(43)` and points at the new
      **§2.12b**, which specifies the dedicated lookup secret (`CFG-SEC-06`), the exact
      HMAC/Base64url computation, and what rotating that secret does to outstanding codes.
      §2.12a's "the mechanism is copied, it is already built and tested" framing is corrected:
      `ParentLinkCode.Code` is stored **plaintext**, so hash + keyed lookup is **new, unbuilt
      machinery** and CT-801 is sized as such.
  (b) **ADDITION, flagged as such** — `UserAccount.provisioned_by_code_id` (nullable FK), added on
      the orchestrator's judgement from the auditor's U43 recommendation, **not** to close a
      defect. It is boxed and marked in §2.2 and is subject to the operator striking it.
  **Still no ENT- minted, and nothing renumbered.**
-->

# 04 — Data Model

**Never shards** (entities are shared across every feature). This file is the single physical
schema reference. No `13-data-migration-spec.md` exists — LEGACY_DATA is not triggered
(`03-system-design.md` §1).

## 1. Entity-Relationship Diagram (carried from Tech Design §5.8, authoritative here)

```mermaid
erDiagram
    INSTITUTION ||--o{ USER_ACCOUNT : "employs/enrols"
    INSTITUTION ||--o{ STUDENT : has
    INSTITUTION ||--o{ TEACHER : has
    INSTITUTION ||--o{ CLASS : has
    INSTITUTION ||--o{ SUBJECT : has
    INSTITUTION ||--o{ ANNOUNCEMENT : publishes
    INSTITUTION ||--o{ ROOM : hosts
    INSTITUTION ||--o{ ACADEMIC_YEAR : defines
    INSTITUTION ||--|| INSTITUTION_CONFIGURATION : configures
    INSTITUTION }o--|| SUBSCRIPTION_PLAN : "subscribes to"
    USER_ACCOUNT }o--|| INSTITUTION : "bound to (BR-008)"
    USER_ACCOUNT ||--o| STUDENT : "optionally linked (FR-016)"
    USER_ACCOUNT ||--o| TEACHER : "optionally linked (FR-023)"
    USER_ACCOUNT ||--o{ PARENT_STUDENT_LINK : "parent of"
    STUDENT ||--o{ PARENT_STUDENT_LINK : "linked via"
    STUDENT }o--|| CLASS : "placed in (BR-017)"
    STUDENT ||--o{ ATTENDANCE_RECORD : has
    STUDENT ||--o{ PARENT_LINK_CODE : "issued for"
    STUDENT ||--o{ ENROLLMENT : "year-scoped roster row"
    TEACHER ||--o| CLASS : "class teacher of (BR-023)"
    TEACHER ||--o{ ATTENDANCE_RECORD : has
    TEACHER }o--o{ SUBJECT : "assigned to teach (BR-024)"
    CLASS ||--o{ STUDENT : contains
    CLASS ||--o{ TIMETABLE_PERIOD : scheduled
    CLASS }o--o{ SUBJECT : "mapped (FR-029)"
    ROOM ||--o{ MESSAGE : contains
    ROOM ||--o{ MEETING : "hosts (many; at most one live, §2.8)"
    ROOM ||--o{ ROOM_MEMBERSHIP : "joined via passcode"
    USER_ACCOUNT ||--o{ ROOM_MEMBERSHIP : "member of"
    ROOM }o--|| USER_ACCOUNT : "created by"
    INSTITUTION ||--o{ LEADERSHIP_CODE : "provisions staff via"
    LEADERSHIP_CODE ||--o| USER_ACCOUNT : "redeemed to create"
    ATTENDANCE_RECORD ||--o{ CORRECTION_REQUEST : "may have (one Pending)"
    CLASS ||--o| CLASS : "next class level (rollover)"
    ACADEMIC_YEAR ||--o{ ROLLOVER_HOLD_BACK : "holds back"
    ACADEMIC_YEAR ||--o{ TERM : contains
    ACADEMIC_YEAR ||--o{ ENROLLMENT : "rollover creates"
    ANNOUNCEMENT }o--|| USER_ACCOUNT : "authored by"
    AUDIT_LOG }o--|| USER_ACCOUNT : "actor"
    REPORT_RUN }o--|| INSTITUTION : "scoped to"
```

## 2. Per-entity attributes, indexes, constraints

### 2.1 Institution (ENT-001)

| Column | Type | Constraint |
|---|---|---|
| `id` | uniqueidentifier | PK |
| `name` | nvarchar(200) | required |
| `registration_number` | nvarchar(50) | required |
| `contact_email` | nvarchar(254) | required, VAL-001 |
| `telephone` | nvarchar(20) | required, VAL-003 |
| `postal_address` | nvarchar(400) | required |
| `postal_code` | nchar(6) | required, VAL-004 |
| `city`, `region` | nvarchar(100) each | required — feeds `PublicInstitutionDirectoryProjection` |
| `is_active` | bit | default 1 (D8) |
| `deactivated_at_utc` | datetime2 | nullable |
| `subscription_plan_id` | uniqueidentifier | FK → SubscriptionPlan, required (never null — auto-set to Entry/Free tier at creation, BRL-074) |
| `subscription_start_date` | date | nullable — *added 1.0.4 (U26, migration `20260914140924_SubscriptionAndTrialFields`)*. API-064's `startDate` had no column to land in |
| `trial_plan_id` | uniqueidentifier | FK → SubscriptionPlan, nullable — *added 1.0.4 (Q-230, U26)*. The plan a running trial grants (Mid). **Entitlement is the trial plan while the trial is unexpired, otherwise `subscription_plan_id`** — so entitlements drop at expiry even before the nightly reversion job runs, and the job merely makes stored state match |
| `trial_started_at_utc`, `trial_expires_at_utc` | datetime2 | nullable — *added 1.0.4*. `trial_started_at_utc` is **retained after reversion**, so "has this school already had its one trial?" is answerable (a trial may be started **once**, and only from the Entry plan) |
| `created_at_utc` | datetime2 | required |

**`Institution` is `IAuditable`** — *recorded at 1.0.4 (U26)*. NFR-032 covers it; the entity was
not in the governed set until CT-503 added it.

**Indexes:** unique composite `(name, contact_email, postal_code)` supports BRL-003's duplicate
check (evaluated in application logic against this index for candidate matches, not a hard DB
unique constraint, since the rule is "duplicate only when all three match" — a DB-level unique
constraint on the triple is in fact the correct enforcement, since that's exactly BR-003's
condition). Non-clustered index on `(city, region)` for directory search (D12).

### 2.2 UserAccount (ENT-002)

| Column | Type | Constraint |
|---|---|---|
| `id` | uniqueidentifier | PK |
| `institution_id` | uniqueidentifier | FK, required, immutable after insert (BRL-008) |
| `username` | nvarchar(100) | unique (platform-global, VAL-006) |
| `email` | nvarchar(254) | unique (platform-global) |
| `password_hash` | nvarchar(max) | required, never selected in any list query |
| `role` | nvarchar(30) | required, CHECK constraint ∈ 7 closed values (BR-007) |
| `student_id` | uniqueidentifier | FK, nullable (FR-016) |
| `teacher_id` | uniqueidentifier | FK, nullable (FR-023) |
| `is_active` | bit | default 1 |
| `deactivated_at_utc` | datetime2 | nullable |
| `provisioned_by_code_id` | uniqueidentifier | **nullable**, FK → `LeadershipCode.id`, `NO_ACTION`. *Added 1.0.5 — see the flagged note below.* Set on every account created by redeeming a sign-up code (BRL-077). **`NULL` means "not attributable to a code"**, which covers every account created before 1.0.5 **and** every legitimate `Student`/`Parent` self-registration |

**Indexes:** unique on `username`, unique on `email`. Non-clustered on `institution_id` (every
list-by-institution query). Non-clustered on `provisioned_by_code_id` (filtered
`WHERE provisioned_by_code_id IS NOT NULL`) — small, and it makes the attribution query below an
index seek rather than a table scan.

> ### ⚠ `provisioned_by_code_id` is an ADDITION, not a defect fix — 1.0.5
>
> **Flagged explicitly so the operator can strike it.** Every other change in patch 1.0.5 closes a
> defect an independent auditor found. **This one does not.** It was recommended by that auditor
> under **U43** and is added **on the orchestrator's judgement**, on the reasoning that it is far
> cheaper to specify now, while `LeadershipCode` and its migration are being written in CT-801,
> than to retrofit onto a populated table later.
>
> **Why it earns its place.** After CT-801 an account's provenance lives **only** in an `AuditLog`
> row — a JSON snapshot in the very table the CMP-001/002/003 retention sweep is designed to
> prune. This column makes *"which privileged accounts cannot be accounted for?"* a durable
> one-line query that stays true for the full seven years:
>
> ```sql
> SELECT id, username, role, institution_id FROM UserAccount
> WHERE role NOT IN ('Student','Parent') AND provisioned_by_code_id IS NULL;
> ```
>
> After CT-801 every privileged account **must** have a non-null value, so any row this returns is
> either a pre-1.0.5 account (the U38 exposure-window set, which stays permanently distinguishable
> instead of merely absent from a log) or a genuine defect. It gives the human doing U43's review
> something to actually run.
>
> **Scope is deliberately one nullable FK and its migration note. It must not grow.** No
> back-fill is attempted or implied — a back-fill would be a guess, and the whole value of the
> column is that `NULL` means *"we do not know"* rather than *"nobody"*. No API returns it; no
> `UI-` displays it. **Striking it costs nothing but this note and one line in CT-801.**

### 2.3 Student (ENT-003) / Guardian fields (ENT-004, embedded, not a separate table)

| Column | Type | Constraint |
|---|---|---|
| `id` | uniqueidentifier | PK |
| `institution_id` | uniqueidentifier | FK, required |
| `sr_number` | nvarchar(30) | required, unique within institution (BR-018) |
| `roll_number` | nvarchar(30) | required, unique within institution (BR-018) |
| `name` | nvarchar(200) | required |
| `date_of_birth` | date | required, VAL-010 |
| `gender` | nvarchar(20) | required |
| `email` | nvarchar(254) | required |
| `class_id` | uniqueidentifier | FK, required once enrolled (BR-019 gates attendance, not enrolment itself) |
| `guardian_father_name`, `guardian_mother_name` | nvarchar(200) each | optional |
| `guardian_contact_email`, `guardian_phone` | nvarchar(254)/nvarchar(20) | optional but validated if present (BR-021 sensitivity — same access rule as the row itself, not a separate table with its own ACL) |
| `is_active` | bit | default 1 |
| `deactivated_at_utc` | datetime2 | nullable |

**Indexes:** unique composite `(institution_id, sr_number)`, unique composite
`(institution_id, roll_number)`. Non-clustered `(institution_id, class_id)` (roster queries).

### 2.4 Teacher (ENT-005)

| Column | Type | Constraint |
|---|---|---|
| `id` | uniqueidentifier | PK |
| `institution_id` | uniqueidentifier | FK, required |
| `name`, `contact_email`, `telephone`, `gender`, `address` | as named | required per FR-018 |
| `joining_date` | date | required |
| `is_active` | bit | default 1 |
| `deactivated_at_utc` | datetime2 | nullable |

### 2.5 Class (ENT-006) / Subject (ENT-007)

| Column | Type | Constraint |
|---|---|---|
| `Class.id` | uniqueidentifier | PK |
| `Class.institution_id` | uniqueidentifier | FK, required |
| `Class.name`, `Class.section` | nvarchar(50) each | required; unique composite `(institution_id, name, section)` (BR-026) |
| `Class.class_teacher_id` | uniqueidentifier | FK → Teacher, nullable, unique (partial index `WHERE class_teacher_id IS NOT NULL` enforces BR-023's "at most one class teacher of at most one class" from the class side; the teacher side enforced by the same index read the other direction) |
| `Class.next_class_id` | uniqueidentifier | FK → Class, **nullable — null means final year** (Q-229, U26, migration `20260914135942_AcademicYearRollover`). This is how rollover knows where a class's students go; FR-067's "next class level" had no representation before. `FK_Classes_Classes_NextClassId` is **NO_ACTION**: deleting a class clears its predecessors' links in application code, so the predecessor simply becomes a final year rather than the delete failing |
| `Subject.id`, `Subject.institution_id`, `Subject.name` | as named | required |
| `SubjectTeacherClassMapping` (join table) | `subject_id`, `teacher_id`, `class_id` | composite PK — implements FR-022/FR-029/BR-024 |

### 2.6 AttendanceRecord (ENT-008)

| Column | Type | Constraint |
|---|---|---|
| `id` | uniqueidentifier | PK |
| `institution_id` | uniqueidentifier | FK, required |
| `person_id` | uniqueidentifier | required (Student or Teacher id) |
| `person_type` | nvarchar(10) | required, ∈ {Student, Teacher} (D10 discriminator) |
| `attendance_date` | date | required |
| `outcome` | nvarchar(10) | required, ∈ {Present, Absent, Late, Leave} (VAL-013), nullable state represented as row-absence (NotYetMarked = no row exists yet, not a stored value — BRL-034) |
| `recorded_by_user_id` | uniqueidentifier | required |
| `recorded_at_utc` | datetime2 | required |
| `correction_status` | nvarchar(20) | nullable, ∈ {None, CorrectionPending} — see attendance state machine, `07-business-logic/attendance.md` |
| `locked` | bit | computed/derived at read time from `attendance_date` + `CFG-004`, not a stored flag that could drift out of sync with the clock |

**Indexes:** **unique composite `(person_id, attendance_date)`** — this is the upsert key (D10,
BRL-031); a second submission for the same key updates, never inserts. Non-clustered
`(institution_id, class_id_via_student, attendance_date)` for class/date retrieval (FR-037).

#### 2.6a `CorrectionRequest` (written back at 1.0.4 — Q-213, built in CT-202, U11)

**The code is the record here.** §2.6 originally carried only a `correction_status` column on
`AttendanceRecord`; the build asked (Q-213) and the operator chose a **separate table**, because a
request has an author, a reason, a resolver and a lifecycle of its own that a status enum cannot
hold. Migration `20260914095538_AddCorrectionRequest`.

| Column | Type | Constraint |
|---|---|---|
| `id` | uniqueidentifier | PK |
| `institution_id` | uniqueidentifier | FK, required, tenant-owned. **`Restrict`, not cascade** — a second cascade path from Institution is rejected by SQL Server |
| `attendance_record_id` | uniqueidentifier | FK → AttendanceRecord, **cascade** |
| `requested_outcome` | nvarchar(10) | required, ∈ {Present, Absent, Late, Leave} (VAL-013) |
| `reason` | nvarchar(max) | required on request; **required on reject** too (PF-007 step 4) |
| `status` | nvarchar(20) | required, ∈ **{Pending, Approved, Rejected, AutoRejected, AppliedDirectly, Superseded}** |
| `requested_by_user_id`, `requested_at_utc` | as named | required |
| `resolved_by_user_id`, `resolved_at_utc` | as named | nullable until resolved |

**Index:** **filtered unique on `attendance_record_id WHERE status = 'Pending'`**
(`IX_CorrectionRequests_AttendanceRecordId`) — edge case E3 ("only one open request per record")
enforced in the database, not only in application logic.

**The two statuses that are not obvious, and why they exist:**
- **`AutoRejected`** — an open request that the daily sweep finds past the `CFG-004` lock. Q-206's
  "pending requests expire at the lock" is *auto-rejected*, never auto-approved.
- **`Superseded`** — leadership writes a direct correction while a Teacher's request is open. The
  request closes as superseded; otherwise a later approval would overwrite leadership's newer
  outcome with a stale one.

`CorrectionRequest` is `IAuditable`, so every transition writes an `AuditLog` row (BRL-035).
**Deletion of the parent record removes its requests explicitly in application code, each
audited** — the FK cascade remains only as a backstop, because a database-side cascade deletes
rows the audit interceptor never sees (found and fixed in CT-203).

### 2.7 Announcement (ENT-009)

| Column | Type | Constraint |
|---|---|---|
| `id`, `institution_id` | as named | required |
| `title`, `content` | nvarchar(200)/nvarchar(max) | required (VAL-016) |
| `effective_date` | date | required (VAL-017) |
| `author_id` | uniqueidentifier | required, immutable |
| `created_at_utc` | datetime2 | required, **immutable** — *added at 1.0.4 (U15, migration `20260914111207_AddAnnouncementCreatedAt`)*. BRL-039 named this column as immutable but §2.7 never declared it, so CT-301 had to add it. Distinct from `effective_date`, which VAL-017 permits to be in the future |
| `is_withdrawn` | bit | default 0 |
| `withdrawn_at_utc` | datetime2 | nullable |

### 2.8 Room (ENT-010) / Message (ENT-011) / Meeting (ENT-012)

| Column | Type | Constraint |
|---|---|---|
| `Room.id`, `Room.institution_id` | as named | required |
| `Room.name`, `Room.description` | as named | required/optional |
| `Room.passcode_hash` | nvarchar(max) | required, never selected into any list/detail DTO |
| `Room.created_by_user_id` | uniqueidentifier | required |
| `Message.id`, `Message.room_id`, `Message.author_id`, `Message.content`, `Message.sent_at_utc` | as named | required; `content` ≤ 4000 chars (VAL-020) |
| `Message.is_withdrawn` | bit | default 0 — content blanked at read time when set, row retained (BRL-044) |
| `Message.withdrawn_at_utc` | datetime2 | nullable — *added 1.0.4 (U15)*. BRL-044 named it; §2.8 did not declare it |
| `Message.revised_at_utc` | datetime2 | nullable — *added 1.0.4 (U15)*. Set by API-046's revise, so a reader can see that text was edited (NFR-030) |
| `Message.client_message_id` | nvarchar(64) | nullable — *added 1.0.4 (U15)*. Client-generated, carries EVT-001's idempotency. **Unique filtered index `(room_id, author_id, client_message_id) WHERE client_message_id IS NOT NULL`** (`IX_Messages_RoomId_AuthorId_ClientMessageId`): a retried send is a no-op returning the original `message_id`, and the partition by author stops one client's id space colliding with another's |
| `Meeting.id`, `Meeting.room_id`, `Meeting.started_at_utc`, `Meeting.ended_at_utc` | as named | `ended_at_utc` nullable while live. **Many meetings per room** — *corrected at 1.0.4 (Q-220, U15)*: §2.8 and the §1 ERD both modelled this one-to-one (`ROOM \|\|--o\| MEETING`), which cannot represent a room's second meeting. **Unique filtered index on `room_id WHERE ended_at_utc IS NULL`** (`IX_Meetings_RoomId`) enforces **at most one *live* meeting per room**, which is what the one-to-one was actually trying to say. Migration `20260914113858_MeetingManyPerRoom` |

#### 2.8a `RoomMembership` (written back at 1.0.4 — Q-219, built in CT-302, U15)

Migration `20260914111747_AddRoomMembership`. The spec had no membership table at all: API-043's
"session token for the SignalR hub group" implied membership lived in a token. It does not — it is
a row, because a token cannot be revoked when a room is removed and cannot survive a reconnect.

| Column | Type | Constraint |
|---|---|---|
| `room_id`, `user_id` | uniqueidentifier | **composite PK** — a concurrent double-join therefore cannot duplicate |
| `institution_id` | uniqueidentifier | FK, required, tenant-owned |
| `joined_at_utc` | datetime2 | required |

**Lifecycle:** created by a **correct-passcode join** (API-043) and removed only by an explicit
leave or room removal. **A disconnect changes presence to offline and does not remove membership**,
so auto-reconnect needs no passcode (NFR-012). The room's creator is its first member — they set
the passcode, so a separate join would verify nothing. Joining re-verifies the passcode even for
an existing member. Room soft-delete (D8) removes its memberships; message history is retained
(BR-043).

### 2.9 Report/ReportRun (ENT-013), Timetable/TimetablePeriod (ENT-014)

| Column | Type | Constraint |
|---|---|---|
| `ReportRun.id`, `.institution_id`, `.report_type`, `.generated_by_user_id` | as named | required (D11). `report_type` is an enum stored **as a string** |
| `ReportRun.status` | nvarchar(20) | required, ∈ **{Pending, Ready, Failed}** — *added 1.0.4 (U20, migration `20260914130535_ReportRunLifecycle`)*. §2.9 declared no lifecycle at all, yet API-050 returns `202 Accepted` and the job completes later; there was literally no column in which to record "not finished yet" |
| `ReportRun.generated_at_utc`, `.blob_uri` | datetime2 / nvarchar(max) | **nullable** — *corrected 1.0.4*. Both were `required`, which is unsatisfiable while a run is `Pending`: there is no file and no completion time yet |
| `ReportRun.failure_reason` | nvarchar(max) | nullable — set when Hangfire's retries are exhausted and the run is marked `Failed` (NFR-011's "no submitted data is lost", extended to the async path: a run that never completes must be *visibly* failed) |
| `ReportRun.format` | nvarchar(10) | required, ∈ {CSV, PDF} per VAL-022. **`PDF` is currently refused at the API with `ERR-901` (Q-226) — a recorded D7/NFR-027 deviation, U21, still open.** The column accepts it so the deferral is data-compatible |
| `ReportRun.from_date`, `.to_date`, `.scope_class_id`, `.scope_teacher_user_id`, `.requested_at_utc` | as named | the request parameters, retained — a run retained indefinitely (FR-055) is not interpretable without the scope it was run for. `scope_*` nullable (null = whole school) |
| `TimetablePeriod.id`, `.institution_id`, `.class_id`, `.subject_id`, `.teacher_id`, `.room` (text field, see `07-business-logic/operations.md` E2), `.day_of_week`, `.time_slot` | as named | required; three partial-unique indexes: `(teacher_id, day_of_week, time_slot)`, `(class_id, day_of_week, time_slot)`, `(room, institution_id, day_of_week, time_slot)` — implement BR-053/054/055 at the DB layer as a second line of defense behind the application-level decision table. `time_slot` is `nvarchar(450)` so it is indexable; it is a validated 24-hour `HH:mm` **start** time (Q-225) |
| `TimetablePeriod.is_published`, `.published_at_utc` | bit / datetime2 | *added 1.0.4 (Q-225, U20, migration `20260914131138_TimetablePublishState`)*. Publish state is **per period**, not per timetable — so API-054's `{id}` is a **period id**. Non-leadership callers see only published periods; a draft is invisible to Teacher and Student until published. An edit preserves the publish state; publishing twice is a no-op. `TimetablePeriod` is `IAuditable` (NFR-028 names schedule changes) |

### 2.10 SubscriptionPlan (ENT-015, platform-global, no `institution_id`)

| Column | Type | Constraint |
|---|---|---|
| `id`, `name` (Entry/Mid/Bespoke) | as named | required, exactly 3 seed rows (BR-061) |
| `learner_capacity`, `storage_allowance_gb` | int | required |
| `support_level` | nvarchar(50) | required |
| `capabilities` | nvarchar(max) JSON array | e.g. `["AdvancedAnalytics","ParentAccess","CustomReporting"]` — read by the tier-gate check (`07-business-logic/configuration-commercial.md`) |

**`SubscriptionPlan` is `IAuditable`** — *recorded at 1.0.4 (U26)*. It became mutable at CT-503:
per **Q-227** the PlatformOperator edits each plan's `learner_capacity`, `storage_allowance_gb` and
`capabilities` (`PUT /subscription-plans/{id}`), because **the seeded numbers have no source in
the BRD, the Tech Design or this suite** — they were CT-001 placeholders. A commercially
load-bearing value that an operator can change must be audited. `learner_capacity = null` means
unlimited. **U28 (the numbers are still unsourced placeholders) stays open and is NOT resolved by
this patch** — it is now *enforced* placeholders, which is a live consequence, not a fix.

### 2.11 InstitutionConfiguration (ENT-016), AcademicYear/Term (ENT-017), Enrollment

| Column | Type | Constraint |
|---|---|---|
| `InstitutionConfiguration.institution_id` | uniqueidentifier | PK+FK, one row per institution |
| `.academic_year`, `.time_zone`, `.language`, `.grading_scale`, `.course_categories`, `.assessment_types`, `.report_card_format`, `.notification_preferences` (JSON) | as named | per FR-063/065/066 |
| `AcademicYear.id`, `.institution_id`, `.label`, `.start_date`, `.end_date` | as named | required |
| `AcademicYear.rollover_requested_at_utc`, `.rolled_over_at_utc`, `.next_academic_year_id` | datetime2 ×2 / uniqueidentifier | nullable — *added 1.0.4 (Q-229, U26)*. **A year rolls over exactly once**: a second API-059 returns `409 ERR-901` and a second job run is a no-op. `next_academic_year_id` is created by the rollover if no year starts the day after this one ends; its label is derived (`2026-27` → `2027-28`) and it spans the same length |
| `RolloverHoldBack.id`, `.academic_year_id`, `.student_id`, `.created_at_utc` | as named | *new table, 1.0.4 (Q-229, U26)*. `FK_RolloverHoldBacks_AcademicYears` CASCADE, `FK_RolloverHoldBacks_Students` NO_ACTION. A hold-back applied **before** the rollover keeps the student in their class; applied **after**, it **appends** a new next-year `Enrollment` in the prior class and never edits the promotion row — `Enrollment` is append-only (BR-060, D14) and rollover must not become the one thing that violates it |
| `Term.id`, `.academic_year_id`, `.label`, `.start_date`, `.end_date` | as named | required |
| `Enrollment.id`, `.academic_year_id`, `.student_id`, `.class_id`, `.created_at_utc` | as named | **append-only** — never updated after insert (BR-060); a class-placement change creates a new `Enrollment` row, the prior one is retained as history |

### 2.12 Cross-cutting: ParentLinkCode, LeadershipCode, ParentStudentLink, AuditLog

| Column | Type | Constraint |
|---|---|---|
| `ParentLinkCode.id`, `.student_id`, `.code` (8 chars), `.issued_by_user_id`, `.issued_at_utc`, `.expires_at_utc`, `.consumed_at_utc`, `.consumed_by_user_id`, `.revoked_at_utc` | as named | `code` unique while `status='Issued'` |
| **`LeadershipCode`** *(added 1.0.4, Q-238 — see below)* | | |
| `ParentStudentLink.parent_user_id`, `.student_id`, `.created_at_utc` | as named | composite PK (many-to-many, BR-066 "linked children" plural) |
| `AuditLog.id`, `.actor_user_id`, `.institution_id`, `.action`, `.entity_type`, `.entity_id`, `.before_snapshot`, `.after_snapshot`, `.timestamp_utc`, `.correlation_id` | as named | append-only, no `UPDATE`/`DELETE` endpoint exists (NFR-033) |

#### 2.12a `LeadershipCode` — privileged-role provisioning (added 1.0.4, Q-238 / U38)

**Deliberately the same table shape as `ParentLinkCode`** — `granted_role` + `institution_id`
replace `student_id`, because this code confers a role at an institution rather than a link to a
learner.

> **Corrected at 1.0.5 (U45d). What is copied, and what is new — stated precisely, because 1.0.4
> overstated it.** 1.0.4 described this whole mechanism as "copied rather than invented because
> that one is built, tested and audited (CT-103, T-003)". That is true of **the lifecycle shape
> only** — issue / redeem / revoke / expiry, the terminal-state discipline, the filtered-unique
> index pattern, and the `IgnoreQueryFilters()` precedent (ADR-0004) are all genuinely proven in
> `ParentLinkCode`. It is **not** true of the storage scheme: **`ParentLinkCode.Code` is stored
> PLAINTEXT** (`src/SchoolSync.Domain/Entities/ParentLinkCode.cs`). **`code_hash` + `code_lookup`
> is new, unbuilt, untested machinery** with no precedent anywhere in this codebase, and it is the
> highest-risk part of CT-801 precisely because 1.0.4's framing invited a builder to treat it as a
> transcription. CT-801 is sized and gated on the understanding that it is **building something
> new**, not copying something tested.

| Column | Type | Constraint |
|---|---|---|
| `id` | uniqueidentifier | PK |
| `institution_id` | uniqueidentifier | FK, required. **Tenant-owned** (`ITenantOwned`) — the global query filter applies (T-002), with one named exception below |
| `code_hash` | nvarchar(max) | required. **The plaintext code is never stored.** Hashed with the same `IPasswordHasher` (BCrypt) the room passcode uses — SEC-004's discipline applied to a second secret |
| `code_lookup` | **`char(43)`, ASCII** | required, **unique while `status='Issued'`**. A deterministic, non-reversible lookup key so a redemption is a single indexed read rather than a table scan of BCrypt comparisons. *A plain hash of an 8-character alphanumeric code is brute-forceable offline, which is why this is keyed, not a bare digest.* **Fully specified at 1.0.5 (U45a/c) — see §2.12b.** |
| `granted_role` | nvarchar(30) | required, ∈ {Administrator, Principal, SchoolInCharge, Teacher} (VAL-028). **`PlatformOperator`, `Student` and `Parent` are not valid values** — CHECK constraint |
| `status` | nvarchar(20) | required, ∈ {Issued, Consumed, Revoked} — `Expired` is **derived from `expires_at_utc` at read time**, never a stored value that could drift out of sync with the clock (the same choice as `AttendanceRecord.locked`, §2.6) |
| `issued_by_user_id` | uniqueidentifier | nullable — **null when issued by a PlatformOperator at institution create (API-001/API-065)**, because at that moment no account of this institution exists. Non-null for every API-066 issue |
| `issued_at_utc`, `expires_at_utc` | datetime2 | required; `expires_at_utc = issued_at_utc + CFG-010` |
| `consumed_at_utc`, `consumed_by_user_id` | datetime2 / uniqueidentifier | nullable; both set together on redemption |
| `revoked_at_utc`, `revoked_by_user_id` | datetime2 / uniqueidentifier | nullable |

**Indexes:** unique on `code_lookup` filtered `WHERE status = 'Issued'` — the same filtered-unique
pattern as `ParentLinkCode.code`, so a revoked or consumed code's key may be reused by a later
issue without collision. Non-clustered `(institution_id, status)` for API-068's list.

**The one named tenant-filter exception.** Redemption (API-006) is **anonymous** — the caller has
no institution claim yet, so the code must be looked up with `IgnoreQueryFilters()`, exactly as
`ParentLinkRepository` already does for `ParentLinkCode` under **ADR-0004**. That is the *whole*
bypass: a single lookup by `code_lookup`, returning one row, from which the institution is then
derived. **Record it as a new ADR in the same shape as ADR-0004 and add it to the audited list of
`IgnoreQueryFilters()` call sites** — the Gate 4 review enumerated every existing one and found no
undocumented exception, and that property must survive this patch.

**Audit:** `LeadershipCode` is `IAuditable` (NFR-028/032). Issue, revoke and consume each write an
`AuditLog` row, and the account-create row carries the code id — so *"how did this Administrator
come to exist?"* is answerable from the audit trail alone, which it was not before.

**Concurrency:** two simultaneous redemptions of the same code are resolved by the filtered unique
index plus a conditional update (`UPDATE … WHERE status='Issued'`); the loser sees 0 rows affected
and returns `ERR-206`. **One code creates exactly one account, never two.** This is the same
check-then-act hazard class as CT-701's capacity race (ADR-0009), so it gets the same treatment:
the invariant is enforced in the database, and the test asserts the invariant, not the status code.

#### 2.12b `code_lookup` — the key, the encoding, and what rotation does (added 1.0.5, U45)

1.0.4 specified a keyed lookup and never said which key, how the result is encoded, or how wide
the column must be to hold it. All three are settled here. **This is new machinery (see the
correction above); none of it is inherited from `ParentLinkCode`.**

**(a) The key — `CFG-SEC-06`, a dedicated secret.** The lookup key is
`LEADERSHIP_CODE_LOOKUP_KEY` (`12-infrastructure-spec.md` §6, `09-security-spec.md` §7), a
**32-byte random secret of its own**, sourced exactly like every other secret in this product (CI
secret store / .NET user-secrets locally; never hardcoded, never logged). 1.0.4 said
"the JWT-signing-key-class secret", which names a *class* of secret rather than a secret — and if
a builder resolved that the obvious way, by reusing the JWT signing key itself, then **rotating
that key (routine, and mandatory after a suspected token leak) would silently make every
outstanding leadership code fail with `ERR-206`**, with no error message anywhere connecting the
two. **The two secrets are therefore explicitly separate: rotating the JWT signing key has no
effect whatsoever on outstanding codes, and rotating `CFG-SEC-06` has no effect on sessions.**

**(b) The computation and the encoding.** `code_lookup = BASE64URL( HMAC-SHA256( CFG-SEC-06,
normalise(code) ) )`, where `normalise` is VAL-027's rule — trim, then upper-case invariantly.
**Base64url without padding, no truncation.** HMAC-SHA256 is 32 bytes → **43 characters**, hence
`char(43)`. 1.0.4's `nvarchar(16)` could not hold the value it described, so two honest
implementations would have silently truncated to different lengths over different uniqueness
domains. **We do not truncate at all** — 43 ASCII characters is a trivial index key, so there is
no width to buy and therefore no collision argument to have to make. `char`, not `nvarchar`: the
alphabet is ASCII by construction and a fixed-width, non-Unicode column halves the index page
cost and makes the length a constraint rather than a convention.

**(c) Rotating `CFG-SEC-06` — what happens to outstanding codes.** The plaintext code is **not
stored** (that is the point of `code_hash`), so `code_lookup` **cannot be recomputed under a new
key**. Rotation therefore invalidates every outstanding `Issued` code. This is stated, not
discovered at 3 a.m.:

| Step | Rule |
|---|---|
| 1 | **Rotation is a deliberate, operator-initiated action**, never an automatic or scheduled one. There is no dual-key read window in this release — a second "previous key" read path doubles the lookup surface for a brute-force attempt and buys at most 7 days of convenience (`CFG-010`). |
| 2 | **Before rotating**, every `status='Issued'` code **must be revoked** (API-067) so no row is left in the filtered unique index whose key belongs to a dead epoch. A revoked row's `code_lookup` is excluded from the filtered index, so the index never spans two key epochs. |
| 3 | **After rotating**, re-issue (API-065/API-066) and re-deliver. The affected population is bounded and small by construction: only codes issued in the last `CFG-010` (7 days) and not yet redeemed. |
| 4 | **The cheap alternative, and usually the right one:** because every code expires within `CFG-010`, an operator may instead schedule the rotation for a moment ≥ 7 days after issuing is paused, and let the outstanding set drain to zero naturally. |
| 5 | **A code that fails lookup after a rotation returns `ERR-206`**, exactly as an unknown code does — deliberately indistinguishable, because a distinguishable "key rotated" response is an oracle. The **operational** signal is the revocation batch in step 2 and its `AuditLog` rows, not the API response. |

**Not a substitute for revocation.** Rotating the key is a blunt, all-tenant instrument; API-067
remains the control for a single leaked code (`10-process-flows/identity-access.md` PF-014,
tested by TC-215).

## 3. Cross-cutting policies (decided once, applied everywhere)

| Policy | Rule |
|---|---|
| **Multi-tenancy** | Every tenant-owned table carries `institution_id`; EF Core global query filter applied via shared convention (T-002). Platform-global tables (no `institution_id`): `SubscriptionPlan`, `PublicInstitutionDirectoryProjection`, `AuditLog` (has the column, but is cross-tenant-queryable only via the PlatformOperator path). |
| **Soft delete** | `IsActive` + `DeactivatedAtUtc` uniform pattern on Institution, Student, Teacher, Room, UserAccount (D8). No hard delete path for these five. |
| **Audit** | `SaveChanges` interceptor writes `AuditLog` for every tracked entity change on the governed set (NFR-028); sign-in/out/denial logged directly from auth middleware. |
| **Timestamps** | All `*_at_utc` columns stored UTC; attendance-day boundary logic (attendance.md) converts using `InstitutionConfiguration.time_zone` at the application layer, never storing a local-time column. |
| **IDs** | `uniqueidentifier` (GUID) PKs throughout — avoids cross-tenant sequential-ID enumeration risk (ties to `09-security-spec.md` §6). |

## 4. Concurrency policy

- **Attendance upsert (D10):** unique `(person_id, attendance_date)` constraint + `MERGE`/upsert
  semantics at the application layer; a race between two concurrent submissions for the same key
  resolves last-write-wins with the DB unique constraint preventing a duplicate row (not a
  duplicate-then-reconcile pattern).
- **Class-teacher assignment (BR-023):** the partial-unique index on `Class.class_teacher_id`
  and the teacher-side check both execute inside the same transaction as the assignment write —
  a concurrent double-assignment attempt fails at the DB constraint, surfaced as `ERR-303`.
- **Schedule conflict checks (BR-053–055):** application-level check + DB partial-unique indexes
  as a second line of defense (§2.9) — under concurrent writes, the DB constraint is the actual
  source of truth; the application check exists to return a clear multi-conflict error message,
  not to be the only safeguard.
- **Optimistic concurrency:** all mutable entities carry a `rowversion`/`xmin`-equivalent
  concurrency token (SQL Server `rowversion`) to detect lost-update races on ordinary
  edit-in-place operations (institution profile, teacher/student edits, configuration).

## 5. Per-entity volume notes (informs indexing/partitioning decisions, not a capacity plan)

| Entity | Expected volume driver |
|---|---|
| Student, Teacher | Bounded by `SubscriptionPlan.learner_capacity` per institution (NFR-021: <200 to unlimited) |
| AttendanceRecord | Highest write-volume table — one row per (person, school day); grows linearly with (students+teachers) × school days; the peak-write window is the start of each school day across all institutions (NFR-022) |
| Message | Grows with room activity; retained for the room's life (BR-043), no compaction in this release |
| AuditLog | Grows fastest of all tables in aggregate (every governed mutation); retention/anonymisation sweep (CMP-001–003) is the only volume control |
| ReportRun + Blob | Retained indefinitely (FR-055); Blob Storage cost scales with report count × size, not row count |
