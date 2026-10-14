# Business Requirements Document

## Product Goal

The product is a cloud-based, subscription-delivered School Management platform that allows educational institutions to run their day-to-day academic and administrative operations from a single, secure, role-aware workspace.

The business objectives are to:

- Replace fragmented, manual record-keeping across student, staff, class and attendance data with one authoritative institutional record.
- Give school leadership real-time visibility of enrolment, staffing and daily attendance.
- Improve communication between school leadership, teaching staff, learners and families through announcements and live collaboration.
- Support a commercial multi-tenant model in which many independent institutions are served from one platform under tiered subscription plans.

Expected value: reduced administrative effort per institution, faster and more accurate daily operations, better-informed leadership decisions, stronger school-to-community communication, and a recurring-revenue product that scales from small schools to large districts.

## Users and Stakeholders

### Primary Users

- **School Administrator / Principal / School In-charge** — owns the institution's configuration, master data, staff and student records, class structure and reporting.
- **Teacher** — records daily attendance, views assigned classes and student rosters, publishes announcements and participates in meetings.

### Secondary Users

- **Student** — accesses announcements, schedule information, meetings and personal academic information.
- **Parent / Guardian** — accesses announcements and information relating to their child.
- **Platform Operator (Super Administrator)** — onboards and maintains institutions, manages subscriptions and oversees the platform across all tenants.

### Stakeholders

- School owners, trustees and governing boards (institutional outcomes, cost of ownership).
- Platform commercial and sales functions (plans, pricing, trials, upgrades).
- Support and onboarding teams (institution setup, user provisioning, issue resolution).
- Data protection and compliance owners (handling of learner and staff personal data).
- Product and engineering delivery team.

## Problem Statement

Educational institutions manage learner records, staff records, class structures and daily attendance across disconnected spreadsheets, paper registers and informal messaging channels. This creates recurring business problems:

- Institutional data is duplicated, inconsistent and hard to trust for decision-making.
- Daily attendance for learners and staff is slow to capture and slower to summarise, so absence patterns are noticed late.
- Leadership has no single, current view of enrolment, staffing and presence.
- Communication to staff, learners and families is ad hoc, with no institutional record of what was announced, by whom and when.
- Meetings and remote sessions depend on external tools that sit outside the school's control and outside its access rules.
- Access to sensitive learner and staff information is not systematically restricted by role, creating privacy and governance exposure.
- Institutions have no simple way to adopt a managed solution that grows with them commercially.

## Scope

The product includes the following business capabilities:

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
12. Reporting on attendance, student performance, course activity and staff performance.
13. Secure collaboration spaces (rooms) supporting real-time text messaging with retained history.
14. Live audio/video meetings within a collaboration room, with participant presence and media controls.
15. Class and examination scheduling.
16. Institution-level configuration of academic settings, notification preferences and access policies.
17. Public product presentation, subscription plan information and enquiry capture.
18. Subscription plan assignment and entitlement management per institution.

## Out of Scope

- Fee collection, payroll, salary disbursement and any financial transaction processing (planned as a subsequent release).
- Leave application, approval workflow and leave-balance tracking (planned as a subsequent release).
- Biometric and hardware-driven attendance capture.
- Learning content authoring, assignment submission, grading workflows and examination marking.
- Transport, hostel, library and inventory management.
- Payment gateway integration and invoicing to institutions.
- Native mobile applications.
- Data migration from any specific incumbent system.

## Success Criteria

- An institution can be onboarded and made fully operational — profile, classes, staff, students and user accounts — within one working day.
- Daily attendance for a full class can be captured and submitted by a teacher in under two minutes.
- At least 90% of active institutions record student attendance on each working day within three months of go-live.
- Leadership dashboards reflect enrolment, staffing and same-day presence figures accurately and without manual reconciliation.
- 100% of access to student and staff personal data is governed by the user's assigned role and institution.
- No user is able to view or modify data belonging to another institution.
- Announcements reach their intended audience within the platform on publication, with a complete institutional history retained.
- Measurable reduction in administrative time spent on record-keeping and attendance reporting, evidenced by institutional feedback within two terms.
- Subscription renewal rate of institutions exceeding 85% year on year.

## Functional Requirements

### 1. Institution Management

**Purpose:** Establish and maintain each subscribing educational institution as the top-level business entity of the platform.

**Key capabilities**
- Register an institution with its official registration number, name, contact details and full postal address.
- Maintain and update institution details over time.
- Assign, view and change the institution's subscription plan and subscription start date.
- Withdraw an institution from active service while preserving its historical record.
- Provide a searchable public directory of institutions to support user self-registration.

**Business rules**
- Only the Platform Operator may create or withdraw an institution.
- Institution administrators may update their own institution's details.
- An institution must be uniquely identifiable; duplicate registrations of the same institution (same name, contact email and postal code) are rejected.
- Contact email must be valid, telephone must be a valid contact number, and postal code must be a valid six-digit value.
- Withdrawal is a reversible deactivation; institutional records are retained and excluded from active operation, never destroyed.
- Every other business record in the platform belongs to exactly one institution.

**User actions**
- Platform Operator: create institution, update institution, assign subscription, withdraw institution, view all institutions.
- Institution Administrator: view and update own institution profile.
- Prospective user: search the institution directory by name during registration.

### 2. Identity, Access and Security

**Purpose:** Ensure every user is authenticated, associated with one institution, and permitted to see and do only what their role allows.

**Key capabilities**
- Self-registration of a user account against a chosen institution and role.
- Sign-in, session establishment and sign-out.
- Retrieval of the signed-in user's identity, institution and roles.
- Role-based authorisation of every business function and navigation destination.
- Denial handling that informs a user clearly when they lack permission.

**Business rules**
- The supported roles are Platform Operator (Super Administrator), Administrator, Principal, School In-charge, Teacher, Student and Parent.
- Every user account is bound to exactly one institution at registration and inherits that institution's data boundary permanently.
- All business data access is filtered by the requesting user's institution; cross-institution access is prohibited for all roles other than the Platform Operator.
- Permission model:
  - Administrator, Principal and School In-charge may create, amend and remove students, teachers, classes, attendance and announcements within their institution.
  - Teachers may view students and classes, record and amend attendance, and publish announcements; they may not create, amend or remove student, teacher or class master records.
  - Students and Parents have read access to announcements and their own relevant information, and may participate in collaboration rooms.
  - Only the Platform Operator may operate across institutions.
- Passwords must meet the platform's minimum complexity policy; credentials are never displayed or transmitted in readable form.
- Sessions expire after a defined period of validity and must be re-established by signing in again.
- Sign-out immediately terminates the session.

**User actions**
- Register an account, sign in, sign out, view own profile and roles.
- Administrator: govern roles, access permissions, account settings and password policy for the institution.

### 3. Student Records Management

**Purpose:** Maintain a single, authoritative record for every enrolled learner.

**Key capabilities**
- Enrol a student with school register number, roll number, name, date of birth, gender, contact email and class placement.
- Record parent/guardian details — father's name, mother's name, contact email and telephone — against the student.
- Amend student details and change class placement.
- Withdraw a student record from active use.
- Retrieve a student individually, as a full institutional list, or as a class roster.
- Optionally link a student record to a platform user account.

**Business rules**
- A student belongs to exactly one institution and is placed in exactly one class at any time.
- School register number and roll number must be unique within the institution.
- Every student must have a class placement before attendance can be recorded for them.
- Student records are visible only to authorised staff of the same institution.
- Guardian contact details are treated as sensitive personal data and follow the same access rules as the student record.

**User actions**
- Administrator / Principal / School In-charge: enrol, view, amend and withdraw students.
- Teacher: view students and class rosters.

### 4. Teaching Staff Management

**Purpose:** Maintain a single, authoritative record for every member of teaching staff and their teaching responsibilities.

**Key capabilities**
- Register a teacher with name, contact email, telephone, gender, address and joining date.
- Amend teacher details and remove a teacher from active service.
- Retrieve a teacher individually or as a full institutional list.
- Assign a teacher as class teacher of a class.
- Assign a teacher to teach a given subject to a given class.
- Optionally link a teacher record to a platform user account.

**Business rules**
- A teacher belongs to exactly one institution.
- A class may have at most one class teacher, and a teacher may be class teacher of at most one class.
- A teacher may be assigned to multiple subject-and-class combinations.
- Removing a teacher must not orphan class or attendance history; existing assignments must be reassigned or released first.

**User actions**
- Administrator / Principal / School In-charge: register, view, amend and remove teachers; assign class teachers and subject allocations.
- Teacher: view own record and assignments.

### 5. Class, Section and Subject Management

**Purpose:** Define the institution's academic structure so that learners, staff, attendance and scheduling can be organised against it.

**Key capabilities**
- Create classes with a class name and section identifier.
- Amend and remove classes.
- Assign a class teacher to each class.
- Retrieve classes individually or as a full institutional list, with the assigned class teacher shown.
- Maintain the institution's subject catalogue.
- Map subjects to teachers and classes.

**Business rules**
- The combination of class name and section must be unique within an institution.
- A class belongs to exactly one institution.
- A class must not be removed while students remain placed in it.
- Class structure is maintained by institution leadership; teachers have read access only.

**User actions**
- Administrator / Principal / School In-charge: create, view, amend and remove classes; maintain subjects and assignments.
- Teacher: view classes and subject allocations.

### 6. Attendance Management

**Purpose:** Capture and maintain accurate daily presence records for learners and staff, and make them available for oversight and reporting.

**Key capabilities**
- Select a class and mark daily attendance for its complete roster in a single session.
- Record an attendance outcome of Present, Absent, Late or Leave for each learner.
- Record daily attendance for teaching staff using the same outcome set.
- Amend a previously recorded attendance entry to correct errors.
- Remove an erroneous attendance entry.
- Retrieve attendance individually, for a class, for a date, and as an institutional list.

**Business rules**
- Attendance is recorded against a specific person and a specific calendar date.
- Only one attendance outcome may exist per person per date; a second submission updates the existing record.
- Attendance may only be recorded for learners and staff of the user's own institution.
- Student attendance may be recorded by teachers and by institution leadership; staff attendance is recorded by institution leadership only.
- When a class roster is opened for marking, every learner defaults to a not-present state until explicitly marked, so no learner is silently omitted.
- Attendance history is retained for reporting and may be corrected, with corrections traceable to the user who made them.

**User actions**
- Teacher: open a class roster, mark daily student attendance, submit, and correct entries.
- Administrator / Principal / School In-charge: record and correct student and staff attendance, review attendance across the institution.

### 7. Announcements and Communication

**Purpose:** Provide an authoritative institutional channel for notices to staff, learners and families.

**Key capabilities**
- Create an announcement with a title, detailed content and an effective date.
- Record the author of every announcement.
- Amend the content of a published announcement.
- Withdraw an announcement.
- View all current announcements for the institution, and view an individual announcement in full.

**Business rules**
- Announcements are visible to every authenticated user of the publishing institution and to no one outside it.
- Announcements may be created and amended by institution leadership and by teachers.
- Announcements may only be withdrawn by institution leadership.
- Every announcement retains its author and date for institutional accountability.

**User actions**
- Leadership and Teachers: create, view and amend announcements; leadership additionally withdraws them.
- All authenticated users: view announcements.

### 8. Collaboration, Messaging and Live Meetings

**Purpose:** Enable staff, learners and families to hold conversations and live sessions inside the platform, under the institution's access rules.

**Key capabilities**
- Create a named collaboration room with a description and an access passcode.
- Browse available rooms and join a room by providing the correct passcode.
- Exchange real-time text messages within a room, delivered to all participants present.
- Retain message history and present it to participants on joining.
- See who is currently present in a room, and see typing and join/leave activity as it happens.
- Start or join a live audio/video meeting associated with a room, with multiple simultaneous participants.
- Control one's own microphone and camera during a meeting, with the state visible to other participants.
- Leave a room or a meeting, with other participants notified.
- Remove a room when it is no longer required.

**Business rules**
- Only authenticated users may create, join or participate in rooms.
- A room may only be joined by presenting the correct passcode.
- A room may be removed by its creator or by institution leadership; no other participant may remove it.
- Message history is retained for the life of the room and remains attributable to its author.
- A message may be revised or withdrawn by its author; withdrawn messages remain accounted for in the record rather than erased.
- Message length is bounded to keep conversations legible and storage predictable.
- Meeting participation is limited to users present in the associated room.

**User actions**
- Create room, browse rooms, join room, send and read messages, view participants, start or join a meeting, mute or disable camera, leave, delete room.

### 9. Dashboards, Reporting and Analytics

**Purpose:** Give leadership and the platform operator current, decision-ready visibility of operations.

**Key capabilities**
- Institution dashboard presenting total students, total teachers, total classes, students present today and teachers present today.
- Platform-wide summary presenting total institutions, students, teachers and classes for public presentation.
- Generate standard reports covering class attendance, student performance, course analytics and teacher performance.
- View when each report was last generated and access previously generated reports.
- Present headline operational indicators such as average attendance.

**Business rules**
- Institution dashboards are scoped strictly to the signed-in user's institution.
- Platform-wide summary figures are aggregate and non-identifying, and may be presented publicly.
- Presence figures always reflect the current calendar date.
- Reports are available to institution leadership; teachers may access reports covering their own classes.
- Report outputs must be exportable for offline distribution.

**User actions**
- View dashboard, select a report type, generate a report, view report history, export a report.

### 10. Scheduling

**Purpose:** Publish and maintain the institution's timetable so staff and learners know where they are expected to be.

**Key capabilities**
- Maintain a weekly timetable of periods by day and time slot.
- Record the class, subject, teaching room and assigned teacher for each period.
- Publish class and examination schedules to staff and learners.
- View the timetable by class and by teacher.

**Business rules**
- A teacher may not be scheduled to two periods in the same time slot.
- A class may not be scheduled to two periods in the same time slot.
- A teaching room may not be double-booked in the same time slot.
- Schedules are maintained by institution leadership and are read-only to teachers and learners.

**User actions**
- Leadership: create, amend and publish timetable entries.
- Teachers and learners: view the applicable timetable.

### 11. Institution Configuration

**Purpose:** Let each institution tailor the platform to its own academic calendar, communication preferences and access policy.

**Key capabilities**
- Maintain general institution settings including academic year, time zone and language preference.
- Maintain user roles, access permissions, account settings and password policy.
- Configure notification preferences across email, SMS and in-platform alerts.
- Maintain academic settings including grading scale, course categories, assessment types and report card format.

**Business rules**
- Configuration applies only to the institution in which it is set.
- Only institution leadership may change configuration.
- Configuration changes take effect for all users of the institution from the point of change and are recorded for audit.

**User actions**
- View and amend institution configuration; review platform status information.

### 12. Commercial Presentation and Subscription

**Purpose:** Present the product to prospective institutions, capture demand, and govern each institution's entitlements.

**Key capabilities**
- Public presentation of the product, its capabilities, customer outcomes and organisational values.
- Publication of tiered subscription plans with pricing, learner capacity, storage allowance, support level and a plan-by-plan capability comparison.
- Capture of sales and support enquiries, and initiation of a trial.
- Assignment of a subscription plan and subscription date to each institution.

**Business rules**
- Three commercial tiers are offered: an entry tier for small institutions, a mid tier for growing institutions, and a bespoke tier for large institutions and districts.
- Each tier defines the institution's learner capacity, storage allowance, support level and available capabilities; capabilities not included in a tier are unavailable to that institution.
- Advanced analytics, parent access and custom reporting are reserved for higher tiers.
- A trial may be started without payment details; entitlements revert to the selected plan when the trial ends.
- Only the Platform Operator may assign or change an institution's subscription.

**User actions**
- Prospective customer: browse product information and plans, compare capabilities, submit an enquiry, start a trial.
- Platform Operator: assign and amend institutional subscriptions.

## Non-Functional Requirements

**Usability**
- Consistent, role-aware navigation that presents only the destinations a user is entitled to use.
- Core daily tasks — marking a class register, publishing an announcement, joining a meeting — completable in no more than three steps.
- Clear confirmation before destructive actions, and explicit, non-technical feedback on success and failure.
- Meaningful empty and loading states so users always know the system's condition.
- Accessible to WCAG 2.1 AA.

**Performance**
- Standard list and detail views return within 2 seconds under normal load.
- Dashboard summaries render within 3 seconds.
- Chat messages are delivered to all present participants within 1 second.
- Live meeting audio and video maintain conversational quality for the supported participant count.

**Reliability**
- 99.5% availability during the institutional working day.
- No loss of submitted attendance, announcement or message data.
- Interrupted collaboration sessions reconnect automatically and restore conversation history.
- Daily backup with a documented recovery objective.

**Security**
- Authentication required for all business functions; only public product information and non-identifying aggregate figures are open.
- Enforced role-based authorisation on every function.
- Absolute isolation of each institution's data.
- Session credentials protected against theft and misuse in the browser and transmitted only over encrypted connections.
- Personal data of learners, guardians and staff encrypted in transit and at rest, and handled in line with applicable data-protection obligations.
- Access to collaboration rooms gated by passcode.

**Scalability**
- Supports many independent institutions concurrently on one platform.
- Supports institutions from under 200 learners to unlimited enrolment as defined by subscription tier.
- Absorbs attendance-capture peaks at the start of the school day without degradation.
- Storage grows with institutional volume within tier allowances.

**Compatibility**
- Fully usable on current versions of major desktop browsers.
- Responsive across desktop, tablet and mobile screen sizes.
- Live meetings supported on browsers providing standard camera and microphone access.
- Reports exportable in commonly used document formats.

**Auditability**
- Creation, amendment and removal of student, staff, class, attendance and announcement records are attributable to a user with a timestamp.
- Announcement authorship and date retained permanently.
- Messages remain attributable to their author, including after revision or withdrawal.
- Sign-in, sign-out and permission denials are logged.
- Institution withdrawal and subscription changes are recorded and reversible.
- Audit history is retained for the institution's defined retention period and is not editable by end users.

## Open Questions

1. What academic-year and term model must the platform support, and how should learner class placement, rosters and attendance history roll over between academic years?
2. What retention period applies to learner, guardian and staff personal data after a student leaves or an institution's subscription ends, and what deletion obligations follow?
3. Should attendance corrections after a defined cut-off require leadership approval, and what is that cut-off?
4. What is the definitive scope of Student and Parent access — which records, reports and communication channels are they entitled to see?
5. What are the source and calculation basis for student performance and teacher performance reporting, given that assessment and grading are outside the current scope?
6. Are data-protection consents from guardians required at enrolment, and should the platform capture and evidence them?


## Answers - Proposed Business Decisions

## 1. Academic Year and Term Model

### Proposed Decision

The platform shall support a hierarchical academic structure consisting of:

- Academic Year (e.g., 2026-2027)
- Terms/Semesters within an Academic Year

At the conclusion of an academic year:

- Learners shall be promoted through a controlled promotion process.
- New class rosters shall be generated for the new academic year.
- Historical class allocations shall remain preserved.
- Attendance records shall remain permanently associated with the academic year in which they were recorded.
- Historical enrollment, roster, and attendance information shall remain available in read-only mode.

### Business Rule

Academic year rollover shall create new academic records while preserving all historical academic, attendance, and enrollment data for reporting and audit purposes.

---

## 2. Learner, Guardian, and Staff Data Retention

### Proposed Decision

The system shall retain personal data for the following periods:

- Student and Guardian Records: 7 years after the student leaves the institution.
- Staff Records: 7 years after employment termination.

In the event an institution's subscription ends:

- Data shall remain accessible for export during a 90-day grace period.
- Data shall then be archived.
- Data shall be permanently deleted or anonymized upon completion of the applicable retention period unless legal obligations require longer retention.

### Deletion Requirements

- Personal information shall be securely deleted or anonymized when retention obligations expire.
- Audit logs may be retained in anonymized form.
- All deletion activities shall be recorded for audit purposes.

### Business Rule

Personal data shall only be retained for legitimate operational, reporting, and regulatory purposes and shall be securely disposed of after the applicable retention period.

---

## 3. Attendance Corrections and Approval Process

### Proposed Decision

Attendance correction rules shall be as follows:

- Teachers may modify attendance records on the same day without approval.
- Corrections made after the attendance date shall require approval from an authorized academic leader.
- Attendance records older than 30 calendar days shall become locked.

Authorized approvers may include:

- Principal
- Vice Principal
- Academic Administrator

### Business Rule

Attendance modifications made after the defined correction window shall require leadership approval and all changes shall be tracked in an audit log.

### Proposed Cut-Off

- Teacher self-correction: Until end of the attendance day.
- Leadership approval required: From the following day onward.
- Record lock period: 30 calendar days.

---

## 4. Student and Parent Access Scope

### Student Access

Students shall be allowed to access:

- Personal profile information
- Class timetable
- Attendance records
- Homework and assignments
- Examination schedules
- Academic reports
- School announcements
- Fee summaries
- School-issued documents

Students shall not be permitted to access:

- Other student records
- Staff records
- Administrative settings
- Institution-wide confidential information

### Parent Access

Parents shall be allowed to access:

- Information related to linked children
- Attendance records
- Academic reports
- Timetables
- Fee information
- Homework and assignments
- School announcements
- Communication history

Parents may also:

- Receive notifications
- Submit leave requests
- Communicate with authorized school representatives

Parents shall not be permitted to access:

- Unrelated student records
- Staff information
- Administrative data

### Business Rule

Students and parents shall only access information directly associated with the learner(s) for whom permission has been granted.

---

## 5. Student and Teacher Performance Reporting

### Proposed Decision

As assessment and grading functionality is outside the current scope, performance reporting shall be derived from operational indicators available within the platform.

### Student Performance Metrics

Student performance reporting may include:

- Attendance percentage
- Homework completion rate
- Assignment submission rate
- Participation indicators
- Behavioral records (if enabled)

### Teacher Performance Metrics

Teacher performance reporting may include:

- Attendance compliance
- Timely attendance submissions
- Assignment management activity
- Homework publication activity
- Student engagement indicators
- Completion of administrative responsibilities

### Business Rule

Performance reporting shall be based on measurable data available within system-managed activities and shall not imply academic grading where grading functionality is not implemented.


## 6 Guardian Consent and Data Protection

### Proposed Decision

Consent should be captured during enrollment.

### The platform should support:

- Acceptance of privacy notice.
- Consent for storage and processing of personal information.
- Photo and media usage consent.
- Emergency contact authorization.
- Communication consent (email, SMS, notifications).

### The system should store:

- Date and time of consent.
- Version of the consent document.
- Guardian identity.
- Method of acceptance.
- Consent status history.

### Business Rule 

Enrollment is considered complete only after required guardian consents have been captured and stored as verifiable records.

## Summary of Recommended Decisions

| Area | Recommended Decision |
|--------|--------|
| Academic Structure | The system shall support an Academic Year → Term/Semester hierarchy. |
| Student Promotion | The system shall provide a controlled year-end rollover process while preserving historical records. |
| Historical Data | The system shall retain historical enrollment, roster, and attendance data in read-only mode. |
| Student & Guardian Retention | The system shall retain student and guardian records for 7 years after departure from the institution. |
| Staff Retention | The system shall retain staff records for 7 years after employment termination. |
| Subscription Expiry | The system shall allow data export during a 90-day grace period before archival. |
| Data Disposal | The system shall securely delete or anonymize personal data after the applicable retention period. |
| Attendance Correction Window | The system shall allow teachers to modify attendance records until the end of the attendance day. |
| Leadership Approval | The system shall require leadership approval for attendance corrections made after the attendance date. |
| Attendance Lock Period | The system shall lock attendance records after 30 calendar days. |
| Audit Trail | The system shall maintain a complete audit trail for attendance modifications and data deletion activities. |
| Student Access | Students shall be permitted to access only their own academic, attendance, fee, timetable, and communication records. |
| Parent Access | Parents shall be permitted to access information related only to their linked children. |
| Student Performance Reporting | The system shall calculate student performance using attendance, assignment, homework, participation, and behavior metrics where available. |
| Teacher Performance Reporting | The system shall calculate teacher performance using operational and engagement metrics available within the platform. |
| Academic Grading | The system shall not treat performance metrics as formal academic grading where grading functionality is outside the scope. |
| Guardian Consent | The system shall require mandatory guardian consent during student enrollment. |
| Consent Evidence | The system shall record consent type, status, acceptance method, timestamp, and document version. |
| Enrollment Completion | The system shall not consider enrollment complete until all mandatory consents have been captured. |
| Privacy Compliance | The system shall maintain verifiable records of all consent and privacy-related activities. |