<!-- pipeline-provenance
produced_by: ai-spec-agent (source-analyser)
generated: 2026-09-11
-->

# 02 — Requirements Spec — `academic-records`

Traces: BRD §6.3 (Students), §6.4 (Teachers), §6.5 (Class/Section/Subject), §6.13 (Student/Parent
access) · ENT-003/004/005/006/007 · ROLE-002–007. Design trace: Tech Design §3.8, §5, §5.7, §7,
§8.5.

## Students (FR-011–017, FR-074)

| SPEC ID | Source | Statement | Acceptance criteria |
|---|---|---|---|
| SPEC-FR-011 | FR-011 | Enrol a student (SR number, roll number, name, DOB, gender, email, class placement) | `POST /api/v1/students` creates with all required fields; class must exist in caller's institution |
| SPEC-FR-012 | FR-012 | Record guardian details (father's/mother's name, contact email, telephone) | Included in the same create/update payload; `VAL-` rules per field |
| SPEC-FR-013 | FR-013 | Amend student details and change class placement | `PUT /api/v1/students/{id}`; class-placement change validated against BR-017 (exactly one class at any time) |
| SPEC-FR-014 | FR-014 | Withdraw a student — reversible deactivation, history intact | `POST /api/v1/students/{id}/withdraw`; `IsActive=false`; attendance/class-assignment history unaffected and still attributable |
| SPEC-FR-015 | FR-015 | Retrieve a student individually, full list, or class roster | `GET /api/v1/students/{id}`, `GET /api/v1/students`, `GET /api/v1/classes/{id}/roster` |
| SPEC-FR-016 | FR-016 | Optionally link a student record to a platform user account | `PUT /api/v1/students/{id}/link-account` (staff-initiated) |
| SPEC-FR-017 | FR-017 (resolved, T-003) | Parent-to-Student association via single-use, time-limited link code | `POST /api/v1/students/{id}/parent-link-codes` (staff, 8-char code, 7-day expiry, `CFG-005`); `POST /api/v1/parent-links {code}` (self-registering Parent) creates a `ParentStudentLink`; code single-use, unexpired, institution-matched (see PF-005) |
| SPEC-FR-074 | FR-074 | Student can access own attendance, timetable, exam schedule, announcements, own academic reports | Scoped `GET` endpoints filtered to `student_id = caller's linked student` |

## Teachers (FR-018–023)

| SPEC ID | Source | Statement | Acceptance criteria |
|---|---|---|---|
| SPEC-FR-018 | FR-018 | Register a teacher (name, email, phone, gender, address, joining date) | `POST /api/v1/teachers` |
| SPEC-FR-019 | FR-019 | Amend/remove (reversible) a teacher | `PUT /api/v1/teachers/{id}`, `POST /api/v1/teachers/{id}/remove` — soft-delete only |
| SPEC-FR-020 | FR-020 | Retrieve a teacher individually or as full list | `GET /api/v1/teachers/{id}`, `GET /api/v1/teachers` |
| SPEC-FR-021 | FR-021 | Assign a teacher as class teacher | `PUT /api/v1/classes/{id}/class-teacher`; BR-023 one-class-teacher-per-class enforced |
| SPEC-FR-022 | FR-022 | Assign a teacher to teach a subject to a class | `POST /api/v1/subjects/{id}/teacher-assignments` |
| SPEC-FR-023 | FR-023 | Optionally link a teacher record to a platform user account | `PUT /api/v1/teachers/{id}/link-account` |

## Class / Section / Subject (FR-024–029)

| SPEC ID | Source | Statement | Acceptance criteria |
|---|---|---|---|
| SPEC-FR-024 | FR-024 | Create classes (name + section) | `POST /api/v1/classes`; BR-026 uniqueness |
| SPEC-FR-025 | FR-025 | Amend/remove classes | `PUT /api/v1/classes/{id}`, `DELETE /api/v1/classes/{id}` blocked while students remain placed (BR-028) |
| SPEC-FR-026 | FR-026 | Assign a class teacher to each class | See SPEC-FR-021 |
| SPEC-FR-027 | FR-027 | Retrieve classes individually/list, with class teacher shown, **Teacher role included (read)** | `GET /api/v1/classes` accessible to Teacher role — corrects the mockup's nav exclusion, per `ui-design-final.md` §0 correction 1 |
| SPEC-FR-028 | FR-028 | Maintain the subject catalogue | `POST/PUT/DELETE /api/v1/subjects` |
| SPEC-FR-029 | FR-029 | Map subjects to teachers and classes | `POST /api/v1/subjects/{id}/class-mappings` |

## Student/Parent access — cross-cutting (FR-075, BR-065/066)

| SPEC ID | Source | Statement | Acceptance criteria |
|---|---|---|---|
| SPEC-FR-075 | FR-075 | Parent accesses linked children's attendance/timetable/exam-schedule/announcements/reports only | All scoped `GET` endpoints filter by `ParentStudentLink` rows for the caller; a parent with 2 linked children sees exactly those 2, never others |

## Business rules

| BRL ref | Statement |
|---|---|
| BR-017 | Student: exactly one institution, exactly one class at any time |
| BR-018 | SR number + roll number unique within institution |
| BR-019 | Class placement required before attendance can be recorded |
| BR-020 | Student records visible only to authorised staff of same institution |
| BR-021 | Guardian contact details are sensitive — same access rule as student record |
| BR-022 | Teacher: exactly one institution |
| BR-023 | Class: at most one class teacher; Teacher: class-teacher of at most one class |
| BR-024 | Teacher may hold multiple subject+class assignments |
| BR-025 | Removing a teacher must not orphan class/attendance history — reassign/release first |
| BR-026 | Class name + section unique within institution |
| BR-027 | Class: exactly one institution |
| BR-028 | Class not removable while students remain placed |
| BR-029 | Class structure maintained by leadership; teachers read-only |
| BR-065 | Fee/leave-request/homework access excluded from Student/Parent (per `requirements.yaml` excluded set FR-077–079) |
| BR-066 | Students/parents access only their own/linked-children's information |

## NFR references

SPEC-NFR-006 (list/detail perf on rosters), SPEC-NFR-016 (tenant isolation), SPEC-NFR-028
(audit on student/teacher/class mutation).

## Full detail

`07-business-logic/academic-records.md` · `05-api-contracts/academic-records.md` ·
`08-error-catalogue.md` (`ERR-3xx`) · `10-process-flows/academic-records.md` (PF-004 enrolment,
PF-005 parent-link) · `11-ui-spec/academic-records.md` (Classes/Teachers/Students screens).
