<!-- pipeline-provenance
produced_by: brd-analyst-agent
plugin_version: 0.3.0 (sdlc-greenfield-v3)
model: claude-sonnet-5
generated: 2026-09-10T00:00:00Z
inputs:
  - path: .aidlc/00-input/SMS-BRD(RJ) 2.md
    sha256: 4c1c753552d1d7e42520590e850180ef2e7c6a4b50fbf91d5af013d6297a0897
  - path: .aidlc/01-brd-analysis/brd-analyst/_questions.md
    sha256: unavailable
-->

# Business Requirements Document — Final

## 0. Document Control

- **Project:** School Management System
- **Source document analysed:** `.aidlc/00-input/SMS-BRD(RJ) 2.md` — the **entire** BRD, delivered as one scope (see `.aidlc/01-brd-analysis/brd-scope/scope-decision.md`: the "Whole BRD as one delivery" option was chosen; `brd-scope-planner` was not run; there is no `00-scope/Scoped_BRD.md`).
- **Gap analysis:** `.aidlc/01-brd-analysis/brd-analyst/_questions.md` — 5 Critical, 7 High, 5 Medium questions, all answered and named (**Jayesh Idnani**); 4 Low items handed off to Tech Design.
- **This document's status:** Gate 1 content unblocked — all Critical/High/Medium questions resolved. **MoSCoW priority on every requirement in `requirements.yaml` is still `priority_source: derived`, not yet human-confirmed in chat** — see §15 and the hand-off note. This is the one open item standing between this document and a fully passed Stage 1 gate.
- **Approvals recorded:** Business-decision answers in `_questions.md` — Jayesh Idnani. Priority table — **not yet confirmed** (see §15).
- **Config files read:** none found (`company.md`, `project.md` not present in this workspace at time of writing).
- **Areas of the source that were thin:** none — `_questions.md` recorded all 12 functional areas as fully covered (Purpose, Key Capabilities, Business Rules, User Actions present throughout). The gaps found were internal contradictions and missing mechanisms, not thin sections.

## 1. Executive Summary

This document finalizes the Business Requirements for a cloud-based, subscription-delivered, multi-tenant School Management platform. It replaces fragmented manual record-keeping (spreadsheets, paper registers, informal messaging) with one authoritative institutional record per school, covering student and staff records, class/subject structure, daily attendance, announcements, in-platform collaboration and live meetings, operational dashboards and reporting, scheduling, institution configuration, and a tiered commercial subscription model. [src: BRD "Product Goal"]

Five Critical and seven High-severity contradictions/gaps identified during gap analysis have been resolved by the named business owner (Jayesh Idnani); the resolutions are integrated throughout §6–§11 below and are individually traceable to `_questions.md`.

## 2. Background & Current State

Educational institutions currently manage learner records, staff records, class structures and daily attendance across disconnected spreadsheets, paper registers and informal messaging channels. [src: BRD "Problem Statement"] This produces: duplicated and inconsistent institutional data; slow, late-noticed attendance and absence patterns; no single current view for leadership of enrolment, staffing and presence; ad hoc communication with no institutional record; meetings dependent on external tools outside the school's access rules; access to sensitive learner/staff data not systematically restricted by role; and no simple commercial path to a managed, growing solution. [src: BRD "Problem Statement"]

## 3. Scope

### 3.1 In Scope

The product delivers the following business capabilities as a single release (no phasing applied — see `.aidlc/01-brd-analysis/brd-scope/scope-decision.md`): [src: BRD "Scope"]

1. Institution (school) onboarding, profile management and lifecycle control.
2. Multi-institution operation with strict separation of each institution's data.
3. User account registration, authentication, session management and role-based access control.
4. Student records management, including enrolment identity, demographics, class placement and guardian details.
5. Teaching staff records management, including employment details and class/subject assignment.
6. Class and section management, including class-teacher assignment.
7. Subject definition and assignment of subjects to teachers and classes.
8. Daily student attendance capture, correction and retrieval.
9. Daily staff attendance capture, correction and retrieval.
10. Institutional announcements with authoring, publication, revision and withdrawal.
11. Operational dashboards summarising enrolment, staffing and daily presence.
12. Reporting on attendance, student performance, course activity and staff performance — **trimmed to indicators actually captured by in-scope capabilities** (see §6.9, C1/H3 resolution).
13. Secure collaboration spaces (rooms) supporting real-time text messaging with retained history.
14. Live audio/video meetings within a collaboration room, with participant presence and media controls.
15. Class and examination scheduling.
16. Institution-level configuration of academic settings, notification preferences and access policies.
17. Public product presentation, subscription plan information and enquiry capture.
18. Subscription plan assignment and entitlement management per institution.

### 3.2 Out of Scope

**Unchanged from the source BRD — confirmed as standing exactly as written (C1 resolution explicitly reaffirms this):** [src: BRD "Out of Scope"]

- Fee collection, payroll, salary disbursement and any financial transaction processing (planned as a subsequent release).
- Leave application, approval workflow and leave-balance tracking (planned as a subsequent release).
- Biometric and hardware-driven attendance capture.
- Learning content authoring, assignment submission, grading workflows and examination marking.
- Transport, hostel, library and inventory management.
- Payment gateway integration and invoicing to institutions.
- Native mobile applications.
- Data migration from any specific incumbent system.

**Additionally excluded following gap resolution** (see `requirements.yaml` for the full excluded list with reasons):

- Guardian consent capture at enrolment (privacy notice, processing, photo/media, emergency-contact, communication consent, dated/versioned/attributable records) — dropped in full; enrolment completion is **not** gated on consent capture in this release. `[src: Q-C3]`
- Fee summaries/fee information, leave-request submission, and homework/assignment access, specifically as they were proposed for Student/Parent access — trimmed for consistency with the Out-of-Scope items above. `[src: Q-C1]`
- Homework completion rate, assignment submission rate, participation indicators and behavioural records as performance-reporting metrics — no capturing mechanism exists for them in this release. `[src: Q-H3]`

## 4. Stakeholders

### 4.1 Primary Users

- **ROLE-002/003/004 — School Administrator / Principal / School In-charge** — owns the institution's configuration, master data, staff and student records, class structure and reporting. `[src: BRD "Users and Stakeholders"]`
- **ROLE-005 — Teacher** — records daily attendance, views assigned classes and student rosters, publishes announcements and participates in meetings. `[src: BRD "Users and Stakeholders"]`

### 4.2 Secondary Users

- **ROLE-006 — Student** — accesses attendance records, class timetable, examination schedules, school announcements, and academic reports based on operational indicators. `[src: Q-C1 — trimmed from the source BRD's broader proposed list]`
- **ROLE-007 — Parent / Guardian** — accesses the same categories of information, limited to their linked children. `[src: Q-C1]`
- **ROLE-001 — Platform Operator (Super Administrator)** — onboards and maintains institutions, manages subscriptions and oversees the platform across all tenants. `[src: BRD "Users and Stakeholders"]`

### 4.3 Other Stakeholders

School owners, trustees and governing boards; platform commercial and sales functions; support and onboarding teams; data protection and compliance owners; product and engineering delivery team. `[src: BRD "Users and Stakeholders"]`

## 5. Business Objectives & Success Criteria

**Objectives** `[src: BRD "Product Goal"]`:
- Replace fragmented, manual record-keeping across student, staff, class and attendance data with one authoritative institutional record.
- Give school leadership real-time visibility of enrolment, staffing and daily attendance.
- Improve communication between school leadership, teaching staff, learners and families through announcements and live collaboration.
- Support a commercial multi-tenant model in which many independent institutions are served from one platform under tiered subscription plans.

**Success Criteria** `[src: BRD "Success Criteria"]`:
- An institution can be onboarded and made fully operational — profile, classes, staff, students and user accounts — within one working day.
- Daily attendance for a full class can be captured and submitted by a teacher in under two minutes.
- At least 90% of active institutions record student attendance on each working day within three months of go-live.
- Leadership dashboards reflect enrolment, staffing and same-day presence figures accurately and without manual reconciliation.
- 100% of access to student and staff personal data is governed by the user's assigned role and institution.
- No user is able to view or modify data belonging to another institution (subject to the bounded, deliberate exception at NFR-016 — the public directory).
- Announcements reach their intended audience within the platform on publication, with a complete institutional history retained.
- Measurable reduction in administrative time spent on record-keeping and attendance reporting, evidenced by institutional feedback within two terms.
- Subscription renewal rate of institutions exceeding 85% year on year.

## 6. Business Requirements (Functional)

Every requirement below carries a stable ID (`FR-`/`BR-`), which is frozen and never renumbered downstream. Full detail — priority, priority source, quote, status — is in `requirements.yaml`, the machine-readable spine this section summarizes. Requirements dropped from scope are listed at the end of each subsection and in the excluded set of `requirements.yaml`, never deleted.

### 6.1 Institution Management (ENT-001)

**Purpose:** Establish and maintain each subscribing educational institution as the top-level business entity of the platform. `[src: BRD §1]`

- **FR-001** The Platform Operator can register an institution with its official registration number, name, contact details and full postal address.
- **FR-002** An Institution Administrator can maintain and update their own institution's details.
- **FR-003** The Platform Operator can assign, view and change an institution's subscription plan and subscription start date.
- **FR-004** The Platform Operator can withdraw an institution from active service while preserving its historical record.
- **FR-005** The platform provides a searchable public directory of institutions, exposing **institution name and city/region only** (no contact details or full postal address), to support user self-registration. `[src: Q-H7]`

**Business rules**
- **BR-001** Only the Platform Operator may create or withdraw an institution.
- **BR-002** Institution administrators may update their own institution's details.
- **BR-003** A new institution registration is rejected as a duplicate only when **all three** of name, contact email and postal code match an existing institution. `[src: Q-M1]`
- **BR-004** Contact email must be valid, telephone must be a valid contact number, and postal code must be a valid six-digit value.
- **BR-005** Institution withdrawal is a reversible deactivation; institutional records are retained and excluded from active operation, never destroyed.
- **BR-006** Every other business record in the platform belongs to exactly one institution.

### 6.2 Identity, Access and Security (ENT-002, ROLE-001…007)

**Purpose:** Ensure every user is authenticated, associated with one institution, and permitted to see and do only what their role allows. `[src: BRD §2]`

- **FR-006** A prospective user can self-register a user account against a chosen institution and role, and is granted **immediate access with no additional approval step**. `[src: Q-H6]`
- **FR-007** A registered user can sign in, establish a session, and sign out.
- **FR-008** A signed-in user can retrieve their own identity, institution and roles.
- **FR-009** Every business function and navigation destination is authorised by the requesting user's role.
- **FR-010** The platform informs a user clearly when they lack permission for an action they attempted.

**Business rules**
- **BR-007** The supported roles are Platform Operator (Super Administrator), Administrator, Principal, School In-charge, Teacher, Student and Parent.
- **BR-008** Every user account is bound to exactly one institution at registration and inherits that institution's data boundary permanently.
- **BR-009** All business data access is filtered by the requesting user's institution; cross-institution access is prohibited for all roles other than the Platform Operator.
- **BR-010** Administrator, Principal and School In-charge may create, amend and remove students, teachers, classes, attendance and announcements within their institution.
- **BR-011** Teachers may view students and classes, record and amend attendance, and publish announcements; they may not create, amend or remove student, teacher or class master records.
- **BR-012** Students and Parents have read access to announcements and their own relevant information, and may participate in collaboration rooms.
- **BR-013** Only the Platform Operator may operate across institutions.
- **BR-014** Passwords must meet the platform's minimum complexity policy; credentials are never displayed or transmitted in readable form. **The complexity policy itself is a technical figure — hand-off to Tech Design (`L1`), not decided here.**
- **BR-015** Sessions expire after a defined period of validity and must be re-established by signing in again. **The expiry duration is a technical figure — hand-off to Tech Design (`L2`).**
- **BR-016** Sign-out immediately terminates the session.

### 6.3 Student Records Management (ENT-003, ENT-004)

**Purpose:** Maintain a single, authoritative record for every enrolled learner. `[src: BRD §3]`

- **FR-011** Institution staff (Administrator/Principal/School In-charge) can enrol a student with school register number, roll number, name, date of birth, gender, contact email and class placement.
- **FR-012** Institution staff can record parent/guardian details — father's name, mother's name, contact email and telephone — against the student.
- **FR-013** Institution staff can amend student details and change class placement.
- **FR-014** Institution staff can withdraw a student record; withdrawal is a **reversible deactivation**, consistent with institution withdrawal, and historical attendance and class-assignment records remain intact and attributable regardless of the student's active/withdrawn status. `[src: Q-M2]`
- **FR-015** Institution staff and Teachers can retrieve a student individually, as a full institutional list, or as a class roster.
- **FR-016** Institution staff can optionally link a student record to a platform user account.
- **FR-017 [DEFERRED — owner: Tech Design]** The BRD includes Parent and Student roles but does not define the parent-student association process. The implementation approach is outside the scope of the current BRD and will be determined during solution design. `[src: Q-C5, revised]`

**Business rules**
- **BR-017** A student belongs to exactly one institution and is placed in exactly one class at any time.
- **BR-018** School register number and roll number must be unique within the institution.
- **BR-019** Every student must have a class placement before attendance can be recorded for them.
- **BR-020** Student records are visible only to authorised staff of the same institution.
- **BR-021** Guardian contact details are treated as sensitive personal data and follow the same access rules as the student record.

### 6.4 Teaching Staff Management (ENT-005)

**Purpose:** Maintain a single, authoritative record for every member of teaching staff and their teaching responsibilities. `[src: BRD §4]`

- **FR-018** Institution staff can register a teacher with name, contact email, telephone, gender, address and joining date.
- **FR-019** Institution staff can amend teacher details and remove a teacher from active service; removal is a **reversible deactivation**, consistent with institution withdrawal, and historical attendance and class-assignment records remain intact and attributable regardless of the teacher's active/removed status. `[src: Q-M2]`
- **FR-020** Institution staff can retrieve a teacher individually or as a full institutional list.
- **FR-021** Institution staff can assign a teacher as class teacher of a class.
- **FR-022** Institution staff can assign a teacher to teach a given subject to a given class.
- **FR-023** Institution staff can optionally link a teacher record to a platform user account.

**Business rules**
- **BR-022** A teacher belongs to exactly one institution.
- **BR-023** A class may have at most one class teacher, and a teacher may be class teacher of at most one class.
- **BR-024** A teacher may be assigned to multiple subject-and-class combinations.
- **BR-025** Removing a teacher must not orphan class or attendance history; existing assignments must be reassigned or released first.

### 6.5 Class, Section and Subject Management (ENT-006, ENT-007)

**Purpose:** Define the institution's academic structure so that learners, staff, attendance and scheduling can be organised against it. `[src: BRD §5]`

- **FR-024** Institution staff can create classes with a class name and section identifier.
- **FR-025** Institution staff can amend and remove classes.
- **FR-026** Institution staff can assign a class teacher to each class.
- **FR-027** Institution staff and Teachers can retrieve classes individually or as a full institutional list, with the assigned class teacher shown.
- **FR-028** Institution staff can maintain the institution's subject catalogue.
- **FR-029** Institution staff can map subjects to teachers and classes.

**Business rules**
- **BR-026** The combination of class name and section must be unique within an institution.
- **BR-027** A class belongs to exactly one institution.
- **BR-028** A class must not be removed while students remain placed in it.
- **BR-029** Class structure is maintained by institution leadership; teachers have read access only.

### 6.6 Attendance Management (ENT-008)

**Purpose:** Capture and maintain accurate daily presence records for learners and staff, and make them available for oversight and reporting. `[src: BRD §6]`

- **FR-030** A teacher can select a class and mark daily attendance for its complete roster in a single session.
- **FR-031** An attendance outcome of Present, Absent, Late or Leave can be recorded for each learner. **"Leave" is a plain attendance-outcome marker only** — it carries no underlying leave-request, approval-workflow, or leave-balance-tracking mechanics, which remain out of scope. `[src: Q-M3]`
- **FR-032** Institution leadership can record daily attendance for teaching staff using the same outcome set.
- **FR-033** A teacher may self-correct their own attendance entries until the end of the attendance day, without approval. `[src: Q-H2]`
- **FR-034** A correction made from the day following the attendance date onward requires approval from an authorised academic leader (Principal, Vice Principal, or Academic Administrator). `[src: Q-H2]`
- **FR-035** An attendance record becomes locked to further correction after **30 calendar days**; this lock period is a **fixed platform-wide constant**, not configurable per institution. `[src: Q-H2]`
- **FR-036** Institution leadership can remove an erroneous attendance entry.
- **FR-037** Attendance can be retrieved individually, for a class, for a date, and as an institutional list.

**Business rules**
- **BR-030** Attendance is recorded against a specific person and a specific calendar date.
- **BR-031** Only one attendance outcome may exist per person per date; a second submission updates the existing record.
- **BR-032** Attendance may only be recorded for learners and staff of the user's own institution.
- **BR-033** Student attendance may be recorded by teachers and by institution leadership; staff attendance is recorded by institution leadership only.
- **BR-034** When a class roster is opened for marking, every learner defaults to a not-present state until explicitly marked, so no learner is silently omitted.
- **BR-035** Attendance history is retained for reporting and may be corrected, with corrections traceable to the user who made them.

### 6.7 Announcements and Communication (ENT-009)

**Purpose:** Provide an authoritative institutional channel for notices to staff, learners and families. `[src: BRD §7]`

- **FR-038** Leadership and Teachers can create an announcement with a title, detailed content and an effective date.
- **FR-039** The platform records the author of every announcement.
- **FR-040** Leadership and Teachers can amend the content of a published announcement.
- **FR-041** Institution leadership can withdraw an announcement.
- **FR-042** All authenticated users can view all current announcements for the institution, and view an individual announcement in full.

**Business rules**
- **BR-036** Announcements are visible to every authenticated user of the publishing institution and to no one outside it.
- **BR-037** Announcements may be created and amended by institution leadership and by teachers.
- **BR-038** Announcements may only be withdrawn by institution leadership.
- **BR-039** Every announcement retains its author and date for institutional accountability.

### 6.8 Collaboration, Messaging and Live Meetings (ENT-010, ENT-011, ENT-012)

**Purpose:** Enable staff, learners and families to hold conversations and live sessions inside the platform, under the institution's access rules. `[src: BRD §8]`

- **FR-043** An authenticated user can create a named collaboration room with a description and an access passcode. The passcode is **shared out-of-band by the room's creator** and is **not** displayed in the room list to browsing users. `[src: Q-M4]`
- **FR-044** An authenticated user can browse available rooms and join a room by providing the correct passcode.
- **FR-045** Participants can exchange real-time text messages within a room, delivered to all participants present.
- **FR-046** The platform retains message history and presents it to participants on joining.
- **FR-047** Participants can see who is currently present in a room, and see typing and join/leave activity as it happens.
- **FR-048** A participant can start or join a live audio/video meeting associated with a room, with multiple simultaneous participants.
- **FR-049** A participant can control their own microphone and camera during a meeting, with the state visible to other participants.
- **FR-050** A participant can leave a room or a meeting, with other participants notified.
- **FR-051** A room's creator or institution leadership can remove a room when it is no longer required.

**Business rules**
- **BR-040** Only authenticated users may create, join or participate in rooms.
- **BR-041** A room may only be joined by presenting the correct passcode.
- **BR-042** A room may be removed by its creator or by institution leadership; no other participant may remove it.
- **BR-043** Message history is retained for the life of the room and remains attributable to its author.
- **BR-044** A message may be revised or withdrawn by its author; withdrawn messages remain accounted for in the record rather than erased.
- **BR-045** Message length is bounded to keep conversations legible and storage predictable. **The specific bound is a technical figure — hand-off to Tech Design (`L3`).**
- **BR-046** Meeting participation is limited to users present in the associated room.
- **BR-047** Collaboration rooms and meetings are governed **solely** by authentication, passcode-gated access, and the existing role-based access model. **No additional child-safeguarding-specific rule** (moderation, supervision, leadership audit-access over Student participation) is introduced for this release. `[src: Q-C4]`

### 6.9 Dashboards, Reporting and Analytics (ENT-013)

**Purpose:** Give leadership and the platform operator current, decision-ready visibility of operations. `[src: BRD §9]`

- **FR-052** Institution leadership can view an institution dashboard presenting total students, total teachers, total classes, students present today and teachers present today.
- **FR-053** The Platform Operator can view a platform-wide summary presenting total institutions, students, teachers and classes for public presentation.
- **FR-054** Institution leadership and Teachers can generate standard reports covering class attendance, student performance, course activity and teacher performance.
- **FR-055** A user can view when each report was last generated and access previously generated reports; report snapshots are **retained indefinitely**, subject to the platform's general data retention rules (§11, CMP-001/002). `[src: Q-M5]`
- **FR-056** The dashboard presents headline operational indicators such as average attendance.
- **FR-057** Student performance reporting is based **solely on attendance percentage**. Homework completion, assignment submission, participation and behavioural indicators are dropped — no capturing mechanism exists for them in this release. `[src: Q-H3, following Q-C1]`
- **FR-058** Teacher performance reporting is based **solely on attendance compliance and timeliness of attendance submission**. Assignment-management activity, homework-publication activity, student-engagement indicators and administrative-responsibility completion are dropped for the same reason. `[src: Q-H3]`

**Business rules**
- **BR-048** Institution dashboards are scoped strictly to the signed-in user's institution.
- **BR-049** Platform-wide summary figures are aggregate and non-identifying, and may be presented publicly.
- **BR-050** Presence figures always reflect the current calendar date.
- **BR-051** Reports are available to institution leadership; teachers may access reports covering their own classes.
- **BR-052** Report outputs must be exportable for offline distribution.

### 6.10 Scheduling (ENT-014)

**Purpose:** Publish and maintain the institution's timetable so staff and learners know where they are expected to be. `[src: BRD §10]`

- **FR-059** Institution leadership can maintain a weekly timetable of periods by day and time slot.
- **FR-060** Institution leadership can record the class, subject, teaching room and assigned teacher for each period.
- **FR-061** Institution leadership can publish class and examination schedules to staff and learners.
- **FR-062** Staff and learners can view the timetable by class and by teacher.

**Business rules**
- **BR-053** A teacher may not be scheduled to two periods in the same time slot.
- **BR-054** A class may not be scheduled to two periods in the same time slot.
- **BR-055** A teaching room may not be double-booked in the same time slot.
- **BR-056** Schedules are maintained by institution leadership and are read-only to teachers and learners.

### 6.11 Institution Configuration (ENT-016, ENT-017)

**Purpose:** Let each institution tailor the platform to its own academic calendar, communication preferences and access policy. `[src: BRD §11]`

- **FR-063** Institution leadership can maintain general institution settings including academic year, time zone and language preference.
- **FR-064** Institution leadership can maintain user roles, access permissions, account settings and password policy.
- **FR-065** Institution leadership can configure notification preferences across email, SMS and in-platform alerts.
- **FR-066** Institution leadership can maintain academic settings including grading scale, course categories, assessment types and report card format.
- **FR-067** At academic year-end, the platform **automatically promotes the full student roster by default**, with institution leadership able to **override and hold back individual students** before or after the automatic rollover. `[src: Q-H1]`
- **FR-068** The platform generates new class rosters for the new academic year, while historical class allocations, enrolment, roster and attendance data remain preserved and available **read-only**. `[src: Q-H1]`

**Business rules**
- **BR-057** Configuration applies only to the institution in which it is set.
- **BR-058** Only institution leadership may change configuration.
- **BR-059** Configuration changes take effect for all users of the institution from the point of change and are recorded for audit, under the platform's general audit-retention rule (`L4` — no separate business decision needed).
- **BR-060** Academic year rollover creates new academic records while preserving all historical academic, attendance and enrolment data for reporting and audit purposes. `[src: Q-H1]`

### 6.12 Commercial Presentation and Subscription (ENT-015)

**Purpose:** Present the product to prospective institutions, capture demand, and govern each institution's entitlements. `[src: BRD §12]`

- **FR-069** A prospective customer can browse public product information, subscription plans, capabilities comparison, and submit a sales/support enquiry.
- **FR-070** A prospective customer can start a **30-day trial** without payment details. `[src: Q-H5]`
- **FR-071** If no purchase decision is made by the end of the trial, entitlements **automatically revert to the entry (lowest-cost) tier**, rather than auto-billing for the trialled plan. `[src: Q-H5]`
- **FR-072** The Platform Operator can assign and amend an institution's subscription plan and subscription date.
- **FR-073** The platform **blocks** an action that would exceed the institution's tier learner capacity or storage allowance (e.g. refuses a new enrolment) until the subscription plan is upgraded; **no grace overage** is provided. `[src: Q-H4]`

**Business rules**
- **BR-061** Three commercial tiers are offered: an entry tier for small institutions, a mid tier for growing institutions, and a bespoke tier for large institutions and districts.
- **BR-062** Each tier defines the institution's learner capacity, storage allowance, support level and available capabilities; capabilities not included in a tier are unavailable to that institution.
- **BR-063** Advanced analytics, parent access and custom reporting are reserved for higher tiers.
- **BR-064** Only the Platform Operator may assign or change an institution's subscription.

### 6.13 Student and Parent Access (cross-cutting; trims §6.3/§6.9)

`[src: Q-C1]` — resolves the contradiction between the source BRD's proposed Student/Parent access list and the Out-of-Scope section.

- **FR-074** Students can access: their own attendance records, class timetable, examination schedules, school announcements, and their own academic reports (per §6.9).
- **FR-075** Parents can access, for their linked children only: attendance records, class timetable, examination schedules, school announcements, and academic reports (per §6.9).

**Business rules**
- **BR-065** Fee summaries/fee information, leave-request submission, and homework/assignment access are **excluded** from Student and Parent access, consistent with the Out-of-Scope section, which stands unamended.
- **BR-066** Students and parents access only information directly associated with the learner(s) for whom access has been granted — their own record for a student, linked children for a parent.

### 6.14 Requirements dropped from this release

Kept here, and in `requirements.yaml` with `status: excluded`, so "was this considered or forgotten" always has an answer:

- Guardian consent capture at enrolment (all forms) — `[src: Q-C3]`.
- Fee summaries/fee information for Student/Parent access — `[src: Q-C1]`.
- Parent leave-request submission — `[src: Q-C1]`.
- Homework/assignment access for Student/Parent — `[src: Q-C1]`.
- Homework/assignment/participation/behavioural performance metrics — `[src: Q-H3]`.
- The eight Out-of-Scope items listed at §3.2 (fee/payroll processing, leave workflow, biometric attendance, learning-content/grading, transport/hostel/library/inventory, payment gateway/invoicing, native mobile apps, data migration) — `[src: BRD "Out of Scope"]`.

## 7. Business Processes (To-Be)

**7.1 Student enrolment (as-is → to-be)**
1. Institution staff (Administrator/Principal/School In-charge) enrol the student with core identity and demographic data and a class placement (FR-011).
2. Staff record parent/guardian contact details against the student record (FR-012).
3. Staff optionally link the student record to a platform user account (FR-016). The mechanism for associating a Parent account with the student is not defined in this BRD and is deferred to Tech Design (FR-017).
4. The student becomes eligible for attendance capture only once a class placement exists (BR-019).

*(No guardian-consent step is part of this process in this release — `[src: Q-C3]`.)*

**7.2 Daily attendance**
1. A teacher opens their class roster for the current date; every learner defaults to not-present until marked (BR-034).
2. The teacher marks each learner Present, Absent, Late or Leave and submits (FR-030, FR-031).
3. The teacher may self-correct any entry until end of day without approval (FR-033).
4. From the next day, a correction requires approval from Principal, Vice Principal or Academic Administrator (FR-034).
5. After 30 calendar days the record locks permanently (FR-035).

**7.3 Academic year rollover**
1. At year-end, the platform promotes the full roster automatically by default (FR-067).
2. Leadership may override and hold back specified individual students, before or after the automatic rollover (FR-067).
3. New class rosters are generated for the new year; all prior years' enrolment, roster, class-allocation and attendance data remain preserved and read-only (FR-068, BR-060).

**7.4 Subscription lifecycle**
1. A prospective customer browses plans and starts a 30-day trial without payment details (FR-070).
2. If no purchase decision is made by trial end, entitlements revert to the entry tier (FR-071).
3. Once subscribed, an institution operating at or near its tier's learner-capacity or storage allowance has further capacity-consuming actions blocked until it upgrades (FR-073).

## 8. Data Requirements (business view)

| ID | Entity | Business meaning | Source |
|---|---|---|---|
| ENT-001 | Institution | The top-level subscribing school/tenant. | `[src: BRD §1]` |
| ENT-002 | User Account | A signed-in identity bound to exactly one institution and role. | `[src: BRD §2]` |
| ENT-003 | Student | An enrolled learner and their identity/demographic record. | `[src: BRD §3]` |
| ENT-004 | Guardian (Parent) | The parent/guardian contact and, where linked, account associated with a student. | `[src: BRD §3]` |
| ENT-005 | Teacher | A member of teaching staff and their assignments. | `[src: BRD §4]` |
| ENT-006 | Class (Section) | A named, sectioned group of students with one class teacher. | `[src: BRD §5]` |
| ENT-007 | Subject | An academic subject in the institution's catalogue, mapped to teachers and classes. | `[src: BRD §5]` |
| ENT-008 | Attendance Record | One outcome (Present/Absent/Late/Leave) for one person on one date. | `[src: BRD §6]` |
| ENT-009 | Announcement | A published institutional notice with author, content and effective date. | `[src: BRD §7]` |
| ENT-010 | Collaboration Room | A named, passcode-gated space for messaging and meetings. | `[src: BRD §8]` |
| ENT-011 | Message | A retained, attributable text message within a room. | `[src: BRD §8]` |
| ENT-012 | Meeting | A live audio/video session associated with a room. | `[src: BRD §8]` |
| ENT-013 | Report | A generated snapshot (attendance, performance, course activity). | `[src: BRD §9]` |
| ENT-014 | Timetable / Period | A scheduled class, subject, room and teacher for a day/time slot. | `[src: BRD §10]` |
| ENT-015 | Subscription Plan | A commercial tier defining capacity, storage, support and capability entitlements. | `[src: BRD §12]` |
| ENT-016 | Institution Configuration | Institution-level academic, notification and access settings. | `[src: BRD §11]` |
| ENT-017 | Academic Year / Term | The academic-year and term/semester structure a rollover operates against. | `[src: Q-H1]` |

## 9. Integration Requirements (business view)

No external system integrations are described in the source BRD. Notification delivery to email and SMS channels is an in-platform configuration concern (FR-065, ENT-016) rather than a named external system; the source gives no integration partner, data-exchange format or mechanism, and none is invented here. **No `INT-` requirements are raised in this release.**

## 10. Non-Functional Requirements (business view)

`[src: BRD "Non-Functional Requirements"]`

**Usability**
- **NFR-001** Navigation is consistent and role-aware, presenting only the destinations a user is entitled to use.
- **NFR-002** Core daily tasks — marking a class register, publishing an announcement, joining a meeting — are completable in no more than three steps.
- **NFR-003** The platform gives clear confirmation before destructive actions, and explicit, non-technical feedback on success and failure.
- **NFR-004** The platform presents meaningful empty and loading states so users always know the system's condition.
- **NFR-005** The platform is accessible to WCAG 2.1 AA.

**Performance**
- **NFR-006** Standard list and detail views return within 2 seconds under normal load.
- **NFR-007** Dashboard summaries render within 3 seconds.
- **NFR-008** Chat messages are delivered to all present participants within 1 second.
- **NFR-009** Live meeting audio and video maintain conversational quality for the supported participant count.

**Reliability**
- **NFR-010** The platform maintains 99.5% availability during the institutional working day.
- **NFR-011** No submitted attendance, announcement or message data is lost.
- **NFR-012** Interrupted collaboration sessions reconnect automatically and restore conversation history.
- **NFR-013** Daily backup is performed with a documented recovery objective.

**Security**
- **NFR-014** Authentication is required for all business functions; only public product information and non-identifying aggregate figures are open without authentication.
- **NFR-015** Role-based authorisation is enforced on every function.
- **NFR-016** Each institution's data is absolutely isolated from every other institution's, with the sole **bounded, deliberate** exception of the public directory exposing institution name and city/region only. `[src: Q-H7]`
- **NFR-017** Session credentials are protected against theft and misuse in the browser and transmitted only over encrypted connections.
- **NFR-018** Personal data of learners, guardians and staff is encrypted in transit and at rest, and handled in line with applicable data-protection obligations (see §11 CMP-001/002).
- **NFR-019** Access to collaboration rooms is gated by passcode.

**Scalability**
- **NFR-020** The platform supports many independent institutions concurrently.
- **NFR-021** The platform supports institutions from under 200 learners to unlimited enrolment as defined by subscription tier.
- **NFR-022** The platform absorbs attendance-capture peaks at the start of the school day without degradation.
- **NFR-023** Storage grows with institutional volume within tier allowances.

**Compatibility**
- **NFR-024** The platform is fully usable on current versions of major desktop browsers.
- **NFR-025** The platform is responsive across desktop, tablet and mobile screen sizes.
- **NFR-026** Live meetings are supported on browsers providing standard camera and microphone access.
- **NFR-027** Reports are exportable in commonly used document formats.

**Auditability**
- **NFR-028** Creation, amendment and removal of student, staff, class, attendance and announcement records are attributable to a user with a timestamp.
- **NFR-029** Announcement authorship and date are retained permanently.
- **NFR-030** Messages remain attributable to their author, including after revision or withdrawal.
- **NFR-031** Sign-in, sign-out and permission denials are logged.
- **NFR-032** Institution withdrawal and subscription changes are recorded and reversible.
- **NFR-033** Audit history is retained for the institution's defined retention period and is not editable by end users.

## 11. Compliance & Regulatory

`[src: Q-C2]`

- **CMP-001** Student and Guardian personal records are retained for **7 years** after the student leaves the institution; Staff personal records are retained for **7 years** after employment termination.
- **CMP-002** On subscription end, institutional data remains accessible for export during a **90-day grace period**, then is archived, then is securely deleted or anonymised once the applicable retention period completes (unless a specific legal obligation requires longer). **No specific named data-protection regulation or jurisdiction is attached to this rule** — it is adopted as a general good-practice business rule, per the answer recorded against Q-C2.
- **CMP-003** Personal information is securely deleted or anonymised when retention obligations expire; audit logs may be retained in anonymised form; all deletion activities are recorded for audit.

## 12. Business Benefits

`[src: BRD "Product Goal"]` Reduced administrative effort per institution; faster and more accurate daily operations; better-informed leadership decisions through real-time dashboards; stronger school-to-community communication via announcements and collaboration; and a recurring-revenue commercial model that scales from small schools to large districts.

## 13. Implementation, Pilot, UAT, Go-Live

The source BRD does not define an implementation plan, pilot approach, UAT process or go-live criteria beyond the Success Criteria already captured at §5. **No implementation/pilot/UAT plan is invented here** — this is a gap for a later stage (Tech Design / delivery planning) to fill, not a business-requirements omission to paper over.

## 14. Risks & Mitigations

| Risk | Source | Mitigation adopted in this BRD |
|---|---|---|
| Public institution directory could be read as conflicting with "absolute data isolation." | `[src: Q-H7]` | Directory scope bounded explicitly to name + city/region only (NFR-016). |
| Guardian consent / data-protection posture was under-specified. | `[src: Q-C3, Q-C2]` | Consent capture dropped from this release; retention/export/deletion rule adopted explicitly (CMP-001/002/003). |
| Performance-reporting metrics referenced capabilities (homework/assignment) not in scope. | `[src: Q-H3, Q-C1]` | Metrics trimmed to attendance-only for both students and teachers. |
| No stated mechanism for Parent-to-Student linking; the Parent access capability (FR-075) is committed but its association mechanism is undefined in this BRD. | `[src: Q-C5, revised]` | Not resolved at BRD level. FR-017 explicitly defers the mechanism to Tech Design rather than asserting a business rule the BRD does not support. |
| Minors participate in live meetings/messaging with no safeguarding-specific rule. | `[src: Q-C4]` | Confirmed as accepted for this release: existing authentication + passcode + RBAC only; no new rule invented. Recorded as a conscious business decision, not an oversight. |

## 15. Open Items

All Critical, High and Medium content questions are resolved (see `_questions.md`). Two items remain genuinely open at the close of this Phase 2 pass:

- **[NEEDS CONFIRMATION] MoSCoW priority table.** Every requirement in `requirements.yaml` currently carries `priority: must` with `priority_source: derived` — derived from the fact that the whole BRD was accepted as a single, unphased release (`.aidlc/01-brd-analysis/brd-scope/scope-decision.md`), so every one of the 18 Scope-section capabilities and their supporting NFRs is treated as committed for this release. **This has not yet been confirmed by a named human in chat**, and per `shared-traceability-ids`/`brd-analyst-requirement-writer`, an unconfirmed `must` blocks the Stage 1 gate (`verify-gate.sh` check C2). **Action needed:** the business owner (proposed: Jayesh Idnani, who confirmed all content answers) reviews and confirms — or amends — the derived priority table before Gate 1 is declared passed.
- **[DEFERRED — owner: Tech Design]** L1 (password complexity policy), L2 (session-expiry duration), L3 (message-length bound) — figures intentionally left unset here; Tech Design decides and states them. L4 (configuration audit retention) needs no separate decision — it is governed by the platform-wide audit rule (NFR-033).
- **[DEFERRED — owner: Tech Design]** FR-017 (Parent-to-Student association mechanism) — the BRD confirms Parent and Student roles and the Parent access capability (FR-075) but does not define how a Parent account becomes associated with a Student record. An earlier pass to this BRD had proposed a specific staff-initiated-at-enrolment business rule; that rule has been withdrawn on revision, and the mechanism is now explicitly left to solution design rather than asserted here. `[src: Q-C5, revised]`

No section has been left `[DEFERRED — owner: <name>]` for business-content reasons; all Critical/High/Medium content questions were answered, not deferred.

## 16. Glossary

One term, one meaning. Business vocabulary only — technical/infrastructure terms are out of scope here and arrive with Tech Design.

| Term | Definition | Also seen as (retired) | Source |
|---|---|---|---|
| Institution | A subscribing school/tenant operating on the platform as the top-level business entity. | "school", "tenant" (retired — "Institution" is the BRD's own consistent term) | `[src: BRD §1]` |
| Platform Operator | The platform-side role (Super Administrator) that onboards institutions, manages subscriptions and operates across all tenants. | "Super Administrator" (kept as the parenthetical, not retired — both used interchangeably in source) | `[src: BRD "Users and Stakeholders", §1]` |
| Institution Administrator | Institution-side leadership role governing that institution's configuration, master data and reporting. | "Administrator" (same role, institution-scoped — distinguished from Platform Operator by context) | `[src: BRD §1, §2]` |
| Guardian | A student's parent or guardian, holder of the contact/consent-relevant details recorded against a student. | "Parent" (used interchangeably in source; "Guardian" chosen as canonical where the retention/data-protection rule (§11) is discussed, "Parent" where the access role (§4/§4.2) is discussed — both are the same person/relationship) | `[src: BRD §3, §4 "Users and Stakeholders", Q-C2]` |
| Class | A named, sectioned group of students taught together, with at most one class teacher. | "Section" (a class's identifying attribute, not a separate concept) | `[src: BRD §5]` |
| Attendance outcome | One of Present, Absent, Late or Leave, recorded against one person for one calendar date. | — | `[src: BRD §6, Q-M3]` |
| Leave (attendance outcome) | A plain status marker recorded directly on an attendance record; carries no request/approval/balance mechanics. | "leave request" (retired in this context — that meaning belongs to the out-of-scope Leave workflow, a different concept entirely) | `[src: Q-M3]` |
| Collaboration Room | A named, passcode-gated space supporting real-time messaging and, optionally, a live meeting. | "room" | `[src: BRD §8]` |
| Trial | A 30-day, no-payment-details period of platform access that reverts to the entry tier if no purchase decision is made. | — | `[src: Q-H5]` |
| Subscription Tier | One of three commercial plans (entry, mid, bespoke) defining an institution's learner capacity, storage allowance, support level and available capabilities. | "plan" | `[src: BRD §12]` |

**One word, two meanings — flagged, not resolved by fiat:** none identified in this pass. "Account" and "record" are each used consistently for a single meaning throughout the source BRD.
