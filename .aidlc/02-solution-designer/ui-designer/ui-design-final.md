<!-- pipeline-provenance
produced_by: ui-enrichment (Beat 2, solution-designer)
plugin_version: sdlc-greenfield-v3
generated: 2026-09-11
sources:
  - .aidlc/00-input/mockups/school-management-ui(RJ) 2.html (working, data-driven mockup — read directly, not screenshotted)
  - .aidlc/01-brd-analysis/brd-analyst/final-brd.md
  - .aidlc/02-solution-designer/technical-design/final-tech-design.md
  - .aidlc/02-solution-designer/ui-designer/design-tokens.md (§9 corrected in this same pass)
-->

# UI Design — Final — School Management System ("SchoolSync")

Screen-by-screen design consumed by `ai-spec/ui-spec-writer` to produce `11-ui-spec.md`. Every
visual value below is a token name from `design-tokens.md` — never a literal hex/px. Component
names are from `design-tokens.md` §8 (Component inventory).

**Roles** (from BRD §4.1/§4.2 and the mockup's `DB.roles`): `SuperAdmin` (Platform Operator),
`Admin`, `Principal`, `SchoolIncharge` — collectively "institution leadership" / "institution
staff" in the BRD — `Teacher`, `Student`, `Parent`.

---

## 0. Navigation model

**Two shells:** a **public** marketing shell (unauthenticated) and an **app** shell (authenticated,
role-filtered side nav). A user moves from public → app only through the Auth modal; there is no
separate full-page login route (`#/login`) — login/register are always a modal over whatever
public page was showing.

### App shell nav (role-filtered)

| Nav item | Route | Roles | Badge |
|---|---|---|---|
| Dashboard | `/dashboard` | all 7 roles | — |
| Classes | `/dashboard/classes` | SuperAdmin, Admin, Principal, SchoolIncharge, **Teacher** *(corrected — see design-tokens.md §9, FR-027)* | — |
| Teachers | `/dashboard/teachers` | SuperAdmin, Admin, Principal, SchoolIncharge | — |
| Students | `/dashboard/students` | SuperAdmin, Admin, Principal, SchoolIncharge, Teacher | — |
| Attendance | `/dashboard/attendance` | SuperAdmin, Admin, Principal, SchoolIncharge, Teacher | — |
| Announcements | `/dashboard/announcements` | all 7 roles | — |
| Meetings | `/dashboard/meeting` | all 7 roles | — |
| Schedule | `/dashboard/schedule` | SuperAdmin, Admin, Principal, SchoolIncharge, Teacher | none *(corrected — see §9, FR-059–062 are in scope)* |
| Reports | `/dashboard/reports` | SuperAdmin, Admin, Principal, SchoolIncharge | none *(corrected — see §9, FR-054–058 are in scope)* |
| Settings | `/dashboard/settings` | all 7 roles (content scoped per role — see §Settings below) | — |

**Student/Parent visibility** per BRD §6.13 (FR-074/075) is narrower than the nav list above
implies for the general case: a Student or Parent's actual accessible set is Dashboard,
Attendance (own/linked-children only), Announcements, Meetings (if the BRD's collaboration scope
extends to them — confirmed by FR-045's "participants" being any authenticated user), Settings
(own account only). Classes/Teachers/Students/Schedule-as-editor/Reports do not apply to
Student/Parent — the nav config for these two roles is a strict subset, not the general list with
read-only variants; **screens must return `screenUnauthorized()` for any deep link outside a
role's actual set**, not merely hide the nav entry (BRD BR-020, and mirrors
`tech-design/security-planner`'s permission matrix).

**Deep links:** every `/dashboard/*` route is directly addressable. A deep link to a route outside
the signed-in role's set renders `screenUnauthorized()` (real mockup screen — "Your current role
does not have permission to view this area," Go Back / Go to Dashboard). An unknown route renders
`screenNotFound()` (real mockup screen — standard 404, Back to Home).

**Back/cancel semantics:** every modal's Cancel/Close discards in-progress form input with no
confirmation (these are short, low-cost forms per BRD scope — add/edit class/teacher/student,
configure a setting). The one exception requiring confirmation is **destructive delete** (class,
teacher, student, announcement, room) — a dedicated confirmation modal names what will be deleted
and states "This action cannot be undone" (real mockup pattern, `confirmDelete()`) before the
delete proceeds. No wizard/multi-step flows exist in this scope; nothing here needs breadcrumb or
step-skip rules.

**Role switching:** the mockup's `roleSwitcher` (profile menu) is confirmed **mockup-only, not a
real product feature** (design-tokens.md §8) — a real account is bound to one role at
registration (BRD, no re-scoping mechanism defined). Do not build a role switcher into the
product; `ai-spec` must not treat it as a requirement.

---

## 1. Public marketing screens

*(Lower design priority than the authenticated app — these are pre-registration surfaces, no
BRD business rules attach to their content — but real mockup-derived and included for
completeness since `ai-spec` needs a source record for every route.)*

### 1.1 Home — `#/`
**Roles:** unauthenticated (public). **Source:** mockup-derived, full fidelity.
**Structure:** sticky header (logo, public nav, Login/Register CTA) → hero (headline, sub-copy,
"Start Free Trial"/"Watch Demo" CTAs, trial disclaimer, 4 stat tiles, live platform-stats card
with a mini chart) → feature grid (4 cards) → testimonials (3 cards) → closing CTA band (dark,
`color-primary-700` per Tech Design's palette binding) → footer.
**States:** success only — no data-dependent empty/error/loading state; the "Live" platform-stats
badge is decorative, not a real-time connection (no BRD requirement backs a live socket on the
public site).
**Copy:** as mockup verbatim (headline, CTAs, stat labels) — no glossary conflicts (none of these
terms appear in BRD §16).

### 1.2 About — `#/about`
**Roles:** unauthenticated. **Source:** mockup-derived. Mission/Vision cards, Core Values grid,
Leadership Team grid. **States:** success only, static content.

### 1.3 Pricing — `#/pricing`
**Roles:** unauthenticated. **Source:** mockup-derived, full fidelity.
**Structure:** monthly/yearly billing toggle → 3 plan cards (Basic/Professional/Enterprise, one
marked "Most Popular") → feature-comparison table → FAQ accordion.
**Business-rule tie-in:** the 3 tiers and their capability gating are **BR-061/062** — the plan
card feature lists and the comparison table must be kept consistent with whatever `tech-design`'s
entitlement model actually enforces server-side; a marketing page overselling a tier's features
is a real defect class, not merely a copy nit. **Operator decision 2026-09-11:** the mockup's
plan-card `features` arrays and comparison-table rows are a **validation-required mockup
artifact, not a requirement or implementation commitment.** `ai-spec`/`ai-spec-source-analyser`
must not carry any individual feature claim into `11-ui-spec.md` (or any other spec file) as
evidence of a real product capability unless it independently traces to an approved FR/BR — the
mockup's own copy is not sufficient grounds on its own.
**States:** billing-toggle switch (monthly/yearly) is a client-only success-state re-render — no
loading/error possible.

### 1.4 Contact — `#/contact`
**Roles:** unauthenticated. **Source:** mockup-derived.
**Structure:** 4 contact-method cards → 2-column layout: contact form (Full Name*, Email*,
School/Institution, Number of Students dropdown, Message*) + "Let's Start a Conversation" panel.
**States:** **empty** (blank form, default) · **validation** (required-field, on submit per the
mockup's native `required` attributes — no live validation shown, so **improved-beyond-mockup:**
add on-blur validation for Email format, matching the platform's general form pattern) ·
**success** (toast: "Message sent — our team will respond within 24 hours," form resets) — no
server round-trip is implied by BRD (this is a lead-capture form, not a modeled entity); `ai-spec`
should treat the submit as a notification-dispatch (BR/FR under §6.12 commercial) rather than
inventing a CRM entity.

### 1.5 Auth modal (login / register) — over any public route
**Roles:** unauthenticated → authenticated on success. **Source:** mockup-derived, full fidelity
(`openAuth()`).
**Login fields:** Username*, Password*, Remember me, Forgot password? link. **Register fields:**
Username*, Email*, Password*, Role* (dropdown of all 7 roles), School* (typeahead against known
institutions).
**States:** empty (blank) · validation (native `required`; **improved-beyond-mockup:** real build
must validate email format and password policy live, since the mockup accepts any string) ·
loading (**improved-beyond-mockup:** the mockup's submit is instant/client-only — a real
credential/registration check has real latency; show a disabled-button + spinner state on submit,
threshold: any submit, since auth calls are never sub-100ms) · error
(**improved-beyond-mockup:** invalid credentials / duplicate username/email / unknown school —
none of these are in the mockup, which always "succeeds"; inline error banner above the form,
field stays populated except password) · success (mockup: toast + redirect to `#/dashboard` on
login, or "Registration successful" toast + switch to login form on register).
**Copy:** "Login"/"Register" verbs kept from mockup — matches BRD's own "Login/Register" wording,
no glossary conflict.
**Navigation:** modal close (X / backdrop click) discards without confirmation — no destructive
risk in an auth form. Switching Login↔Register inside the modal preserves nothing (fresh form),
consistent with mockup behaviour.

---

## 2. App shell

**Source:** mockup-derived, full fidelity (`appShell`, `sideNav`, `appMain`, header, profile menu).
**Structure:** fixed 64px-wide (token: not yet named — flag to `theme-designer`/Tech Design: the
sidebar width `w-64`/`16rem` should get a named token, e.g. `layout-sidenav-width`, since it's a
structural layout value, not a spacing increment) dark (`color-text-primary`-toned, i.e. `ink-900`
background) side nav with logo, role-filtered nav items (§0 above), Log out button pinned to
bottom. Header: hamburger (mobile), page title + breadcrumb path, search box (desktop only,
**decorative in this release — no FR backs a global search**, flag to `ai-spec` as
`improved-beyond-mockup` scope decision: build the input but it is out of scope to wire to a real
search endpoint unless `ai-spec` finds an FR I've missed), notification bell (mockup: static toast
on click — **flag: no BRD requirement for a notification center/inbox; keep as a decorative
affordance or drop it, ai-spec's call**), profile menu (name/role/school, role-switcher — **do
not build**, see §0 — Account settings link, Log out).
**States:** sidebar collapse/expand on mobile (`<lg` breakpoint) — off-canvas, backdrop-dismiss.
Profile menu: closed/open, dismiss on outside click or Escape (**improved-beyond-mockup**: mockup
doesn't specify Escape; add it — standard a11y expectation for any dismissible menu, ties to
`ui-enrichment-accessibility-auditor`'s remit).
**Navigation:** breadcrumb (`crumbPath`) is always exactly "Home" in the mockup regardless of
depth — **improved-beyond-mockup:** the built product should reflect actual nesting (e.g.
Dashboard / Attendance) since a fixed breadcrumb is a mockup shortcut, not a real design decision.

---

## 3. Dashboard — `/dashboard`
**Roles:** all 7 (content should vary by role — see below). **Source:** mockup-derived, full
fidelity for the Admin/leadership view (`pageHome()`).
**Structure:** welcome header + date + "Take Attendance"/"New Announcement" quick actions → 4 stat
tiles (Total/Present Teachers, Total/Present Students) with sparklines → Attendance Trend
(area chart, Students vs Teachers) + Today's Attendance donut → Students-per-Class bar chart +
Gender Split donut → Recent Activities feed + Upcoming Events list + Upcoming Features (roadmap
teaser — **flag: "Upcoming Features" panel is a marketing/demo affordance, not a BRD
requirement; ai-spec's call whether to build it as inert roadmap copy or drop it**).
**Role variance — a real gap the mockup does not model:** the mockup's dashboard is
leadership-shaped only (institution-wide stats). **Student and Parent dashboards must be
materially different** per BRD §6.9/§6.13 — a Student sees their own attendance percentage and
academic reports only (FR-057, FR-074); a Parent sees the same for their linked children
(FR-075); neither sees institution-wide teacher/student totals (BR-048 scopes leadership
dashboards to leadership). **Approved 2026-09-11 (operator):** Student/Parent dashboard = a
narrower variant (own/linked-children attendance trend + today's status + recent announcements),
reusing the same card/chart components at reduced scope, not a different visual language. Final —
`ai-spec` should build `11-ui-spec.md`'s Dashboard entry with this role variance as a firm
requirement, not an open question.
**States:** empty (a brand-new institution with 0 students/teachers — every stat tile shows 0,
charts show a flat/empty state rather than a broken axis — **improved-beyond-mockup**, mockup
always has seed data) · loading (skeleton stat tiles + chart placeholders, threshold: any
fetch — dashboards aggregate multiple entities so a spinner-only state would look broken for
several hundred ms) · success (as designed above).

---

## 4. Classes — `/dashboard/classes`
**Roles:** SuperAdmin, Admin, Principal, SchoolIncharge, Teacher (read; corrected, FR-027).
**Source:** mockup-derived, full fidelity (`pageClasses()`).
**Structure:** card grid, one card per class — class-teacher avatar+name, room, student count,
attendance-rate, sparkline, View/Edit/Delete icon actions (**leadership only** — BR-029 says class
structure is leadership-maintained, teachers have read access only; **improved-beyond-mockup: for
`Teacher` role, hide Edit/Delete icon actions and the "+ Add New Class" button entirely** — the
mockup doesn't gate these by role at all, this is a real authorization gap to close, not a
cosmetic one), "Take Attendance" / "View Roster" quick actions per card.
**Add/Edit/Delete modal (`modalClass`):** fields per FR-024 (class name, section) + class-teacher
assignment (FR-026). Delete uses the shared confirmation pattern (§0).
**States:** empty ("No classes yet" — **improved-beyond-mockup**, mockup always has 5 seed
classes) · loading (skeleton cards) · success.

## 5. Teachers — `/dashboard/teachers`
**Roles:** SuperAdmin, Admin, Principal, SchoolIncharge (leadership only, per mockup's own gating
— no BRD requirement grants Teachers a peer directory, so the mockup's role list is followed
as-is here). **Source:** mockup-derived, full fidelity (`pageTeachers()`).
**Structure:** Staff-by-subject bar chart + presence donut → searchable/filterable
(name/email search, subject filter) directory table (avatar, name+email, subject, phone, gender,
address, status pill, view/edit/delete) with pagination and CSV export.
**Add/Edit/Delete modal (`modalTeacher`):** fields per FR-018 (name, email, phone, gender, address,
joining date) + class-teacher/subject-teaching assignment (FR-021/022).
**States:** empty ("No teachers found — adjust your search or filters," real mockup empty-state
copy) · loading (skeleton table rows) · success.

## 6. Students — `/dashboard/students`
**Roles:** SuperAdmin, Admin, Principal, SchoolIncharge (full CRUD), Teacher (**view only** —
mockup's own `canEdit` check already correctly restricts Add/Edit/Delete to leadership roles,
matching BR-029-equivalent staff-ownership pattern for student records). **Source:**
mockup-derived, full fidelity (`pageStudents()`).
**Structure:** enrolment-by-class bar chart + status-breakdown donut → searchable
(name/email/SR-number) + class-filterable table (SR number, roll number, name+email, class, DOB,
gender, status pill, view/edit-if-leadership/delete-if-leadership) with pagination and export.
**Add/Edit modal (`modalStudent`):** fields per FR-011/012 — SR number, roll number, name, DOB,
gender, email, class placement, plus guardian block (father's name, mother's name, guardian phone,
guardian email — BR-021 treats these as sensitive, same access rule as the student record itself).
**States:** empty ("No students found...") · loading · success.

## 7. Attendance — `/dashboard/attendance`
**Roles:** SuperAdmin, Admin, Principal, SchoolIncharge, Teacher (mark); Student/Parent see only
their own/linked-children's records via the Dashboard, not this full-roster screen (BR-032/033).
**Source:** mockup-derived, full fidelity (`pageAttendance()` + `modalFillAttendance()`).
**Structure:** class selector + date picker + "Fill Attendance" action → 4 stat tiles (Roster,
Present, Absent, Late/Leave) → attendance-over-time area chart + today's-breakdown donut →
full attendance table (roll no., name, date, status pill) with export.
**Fill-attendance modal:** bulk "Mark all Present/Absent" + per-student
Present/Absent/Late/Leave toggle row (real mockup pattern, `attRows()`/`setAtt()`) → Save.
**Business-rule-driven states the mockup does not show (must be designed, not inherited):**
- **Correction cut-off (FR-033/034/035):** same-day self-correction is open (as mockup shows);
  from the next day, a correction attempt must route through an **approval state**, not a silent
  edit — **improved-beyond-mockup, this is a real gap.** Recommend: editing a >1-day-old record
  shows a distinct "Request correction" action instead of the direct toggle, submitting a
  leadership-approval request rather than writing immediately; the record shows a
  pending-approval badge until resolved. After 30 days (FR-035) the toggle/request action is
  removed entirely — the row renders read-only with a "Locked" indicator.
- **Default not-present (BR-034):** on opening a fresh roster, every row must default to a
  *not-yet-marked* visual state, distinct from "Absent" — the mockup's `modalFillAttendance()`
  actually defaults every student to `'Absent'` (`STATE.attendance[s.id]='Absent'`), which
  **contradicts BR-034** ("every learner defaults to a not-present state until explicitly
  marked" — not-present ≠ Absent as a recorded outcome). **Flag: this is a mockup defect, not a
  design choice** — the real build must show an unmarked/neutral state (e.g. all four toggle
  buttons unselected) until a teacher actively picks one, so nothing is silently recorded as
  "Absent" by default.
**States:** empty (class with 0 students — "This class has no students enrolled yet," real mockup
copy) · loading (skeleton table + stat tiles) · success · the correction/lock states above.

## 8. Announcements — `/dashboard/announcements`
**Roles:** view — all 7; post/edit — leadership + Teacher (BR-037); withdraw — leadership only
(BR-038, `canPost` in mockup already reflects create/edit correctly but its delete icon is shown
to the same `canPost` set — **corrected 2026-09-11 (operator-approved):** BR-038 restricts
withdrawal to leadership specifically, narrower than "can post"; the delete icon is gated to
leadership only, not the full canPost set which includes Teacher. Final.
**Source:** mockup-derived, full fidelity (`pageAnnouncements()`).
**Structure:** search bar + "New Announcement" (if permitted) → card feed (scene image, author
avatar+name+date, title, view/edit/delete icons if permitted, excerpt, "Read more").
**View modal:** full title, author+date, full content, "visible to all members of [school]" note.
**Create/Edit modal (`modalAnnouncement`):** title, content, effective date (FR-038).
**States:** empty ("No announcements found — nothing matches your search right now") · loading
(skeleton cards) · success.

## 9. Meetings / Collaboration — `/dashboard/meeting`, `#/chat/:roomId`, `#/video-call/:roomId`
**Roles:** all 7 (BR-040 — any authenticated user). **Source:** mockup-derived, full fidelity
across all three screens (`pageMeetings()`, `screenChat()`, `screenVideo()`).

**9.1 Room list (`/dashboard/meeting`):** "+ Create Room" → card grid (cover image, participant
avatars stack, name, creator, description, passcode-protected badge, participant count, Join/Chat/
Video actions, leadership-or-creator-only delete — BR-042).
**Create-room modal:** name*, description (optional), passcode* (FR-043 — shared out-of-band by
creator, never displayed in the room list — confirmed, mockup's card correctly shows only a
"passcode protected" badge, never the value itself).
**Join-room modal:** read-only room name + passcode entry (FR-044).

**9.2 Chat (`#/chat/:roomId`):** header (back, room name, live participant count, Start Video Call,
Leave Room) → message log (history-loaded marker, message bubbles, typing indicator) → composer
(text input + Send) → participants sidebar (desktop only, presence dot per FR-047).
**States:** loading (history fetch — skeleton bubbles) · empty (a brand-new room, "No messages
yet — say hello" — **improved-beyond-mockup**, mockup always has seed messages) · success ·
**real-time presence** (join/leave/typing events per FR-047 — **improved-beyond-mockup: the
mockup's typing indicator is a static hard-coded line, not wired to any event; the real build
must show/hide it live** and reflect actual join/leave, which the mockup does not model at all).

**9.3 Video call (`#/video-call/:roomId`):** dark full-screen layout, header (back, call title,
participant count, LIVE badge), participant tile grid (portrait, name label, muted indicator),
control bar (mic toggle, camera toggle, screen-share, jump-to-chat, Leave Call).
**States:** connecting (**improved-beyond-mockup: the mockup enters "Connected" instantly; a real
media session has a real connect/negotiate phase** — show a connecting spinner per tile until
each participant's stream resolves) · connected (as designed) · reconnecting
(**improved-beyond-mockup, ties to BRD NFR-012** — a dropped participant's tile shows a
reconnecting overlay rather than vanishing) · error (media permission denied — camera/mic
blocked by the browser — **improved-beyond-mockup, not in the mockup at all**: show an
inline permission-needed message on that participant's own tile with a "how to enable" hint,
never silently fail).
**Navigation:** Leave Call returns to the room list (mockup behaviour) — no "are you sure" needed,
this is a live session with no unsaved-form state to lose.

## 10. Schedule — `/dashboard/schedule`
**Roles:** SuperAdmin, Admin, Principal, SchoolIncharge (maintain, BR-056); Teacher/learner (view
only). **Source:** mockup-derived (`pageSchedule()` — teaching-load-by-staff chart confirmed;
full grid/timetable rendering not re-read line-by-line in this pass but the function exists and
renders real content per the router map).
**Structure (per FR-059–062):** class selector, "+ Add Schedule" action, teaching-load chart, and
a weekly period grid (day × time-slot) showing class/subject/room/teacher per period.
**Business-rule-driven states:** a period-slot conflict (BR-053/054/055 — teacher, class, or room
double-booked) must be a **blocking validation error at save time**, not a silent overwrite —
**improved-beyond-mockup**, not modeled in the mockup's "Schedule entry form opened" toast stub.
**States:** empty (no periods scheduled yet) · loading · success · conflict-error (above).

## 11. Reports — `/dashboard/reports`
**Roles:** leadership full access; Teacher — own classes only (BR-051). **Source:** mockup-derived,
full fidelity (`pageReports()`).
**Structure:** attendance-vs-performance area chart + grade-distribution donut → attendance-by-
class + subject-engagement bar charts → 4 report-type cards (Student Performance, Class
Attendance, Course Analytics, Teacher Performance) each with Generate/View History → quick-stat
gauges (attendance, completion, satisfaction, efficiency).
**Operator decision 2026-09-11 on the "Student Satisfaction" and "Course Completion" gauges:**
these are **validation-required mockup artifacts, not requirements.** BRD §6.9 grounds reporting
solely in attendance and attendance-compliance per Q-H3's resolution (homework/engagement metrics
were explicitly dropped from scope). **`ai-spec`/`ai-spec-source-analyser` must NOT spec these two
gauges into `11-ui-spec.md` unless it independently traces them to an approved FR/BR** — carrying
them forward from this mockup observation alone is not sufficient grounds. Grade-distribution
(pass-rate donut) and the two attendance/subject charts are traceable to FR-054/056/057 and stay.
**Generate-report modal:** report type (read-only), date range, scope (whole school or one
class), format (PDF/Excel/CSV) — matches FR-054/055's "generate" + "retained indefinitely,
exportable" requirements.
**States:** loading (report-generation is not instant — show a progress/spinner state on
Generate, not the mockup's instant toast) · success (report ready, added to history) · empty
(no reports generated yet for a brand-new institution).

## 12. Settings — `/dashboard/settings`
**Roles:** leadership (School Profile + the 4 config sections, per BR-058 "only institution
leadership may change configuration"); all other roles see **their own account settings only**
(**a real gap: the mockup shows the full leadership settings screen unconditionally — must be
role-gated**, improved-beyond-mockup). **Source:** mockup-derived, full fidelity for the
leadership view (`pageSettings()`).
**Structure (leadership):** School Profile card (name, registration number, email, phone, address,
plan) + Edit action → 4 config-section cards (General/User Management/Notifications/Academic,
per FR-063/064/065/066) each with per-item "Configure" action → System Information (version,
last-updated, status, storage — **flag: platform-version/storage-quota display has no clear BRD
backing; likely fine as informational chrome, ai-spec's call**).
**Configure modal:** generic setting/value/applies-to pattern (mockup's `modalConfigure`) — every
save must write an audit-log entry (BR-059, mockup's own checkbox for this should become
non-optional/always-on, not a user-toggleable checkbox, since BR-059 is unconditional).
**Non-leadership Settings (Student/Parent/Teacher):** own-account fields only (name/contact,
password change, notification preferences for their own account) — a materially smaller screen,
not the leadership view with sections hidden. **Approved 2026-09-11 (operator) — final, build as
specified.**
**States:** loading (settings fetch) · success · save-confirmation (toast, per mockup pattern).

---

## Hand-off summary

```
ui-design-final.md ready: .aidlc/02-solution-designer/ui-designer/ui-design-final.md

Screens designed: 20 (4 public + auth modal + app shell + 12 dashboard pages + chat + video-call
                      + 2 utility screens [Unauthorized/NotFound])
Real mockup fidelity: all 20 (design-tokens.md §9 corrected accordingly)
Out of scope, correctly excluded: Payment, Leave (mockup renders them; BRD excludes them — do not build)

Operator-approved 2026-09-11 — final dispositions:

Items 1-7 (corrections required to align the UI with the approved BRD/Tech Design — APPLY, final):
  1. Nav role-gating: Classes includes Teacher (FR-027) — mockup's exclusion overridden.
  2. Schedule/Reports "soon" badges removed — both are real, in-scope BRD requirements.
  3. Attendance default-state bug fixed: roster opens with every student unmarked/neutral, never
     defaulting to a recorded 'Absent' (BR-034).
  4. Attendance correction workflow (FR-033/034/035 — same-day self-correct, next-day-onward
     needs approval, 30-day lock) designed from BRD text since the mockup doesn't model it.
  5. Dashboard Student/Parent variant (own/linked-children scope, per FR-057/074/075, BR-048)
     approved as a firm requirement, not an open question.
  6. Settings role-gating (leadership view vs. own-account view for everyone else) approved.
  7. Announcements delete-icon narrowed to leadership-only (BR-038), not the broader canPost set.

Items 8-9 (NOT requirements — validation-required mockup artifacts only):
  8. Reports' "Student Satisfaction" and "Course Completion" gauges — mockup-derived visual
     artifacts. `ai-spec` must NOT spec these into 11-ui-spec.md (or any spec file) as evidence of
     a real capability unless independently traced to an approved FR/BR.
  9. Pricing page's plan-feature-list claims (plan cards + comparison table) — same rule: mockup
     copy is not requirements evidence; `ai-spec` must not cite any individual feature claim from
     it without independent FR/BR traceability.

Next: ai-spec reads this file + design-tokens.md to write 11-ui-spec.md, assigning UI-/VAL-/ERR-
IDs per screen, applying corrections 1-7 as firm requirements and treating items 8-9 per the
validation-required constraint above.
```
