<!-- pipeline-provenance
produced_by: ai-spec-agent (error-catalogue-writer)
generated: 2026-09-11
revised: 2026-09-15 — spec_suite_version 1.0.4 (narrow patch, authorised by Rashmi Mehta at the
  Gate 4 CHANGES-REQUIRED verdict). Added ERR-206/207/208 (leadership sign-up code lifecycle,
  Q-238 / U38). Corrected ERR-904's dangling cross-reference — it now names `09-security-spec.md`
  §10, which this patch creates, and the three CFG- keys behind it (Q-240 / U39, folds in U16).
  No existing ERR- was renumbered or re-worded.
-->

# 08 — Error Catalogue

**Never shards.** Every error in this suite appears here exactly once. No other file words an
error inline — `05-api-contracts`, `07-business-logic`, `10-process-flows` and `11-ui-spec` all
reference these `ERR-` codes by ID only.

**Response shape (all errors):** `{ "errorCode": "ERR-xxx", "message": "<non-technical, matches
NFR-003>", "traceId": "<correlation id>" }`. HTTP status is chosen per the table below; the
`errorCode` is always present regardless of status so the client never needs to parse the message
string to branch behavior.

## 1xx — Institutions

| ERR | HTTP | Message (user-facing) | Trigger | Trace |
|---|---|---|---|---|
| ERR-101 | 409 | "An institution with this name, email and postal code is already registered." | BRL-003 composite duplicate | BR-003 |
| ERR-102 | 403 | "You don't have permission to do this." | BRL-001/002 role/scope check fail | BR-001, BR-002 |

## 2xx — Identity / Access

| ERR | HTTP | Message | Trigger | Trace |
|---|---|---|---|---|
| ERR-201 | 400 | "Your password doesn't meet the minimum requirements." | BRL-014 password policy fail | BR-014 |
| ERR-202 | 401 | "Your username or password is incorrect." | Login fail (never reveal which field is wrong) | FR-007 |
| ERR-203 | 409 | "That username or email is already registered." | VAL-006 uniqueness fail at registration | FR-006 |
| ERR-204 | 401 | "Your session has expired. Please sign in again." | Access+refresh both expired/denylisted | BR-015, BR-016 |
| ERR-205 | 403 | "You don't have permission to do this." | Generic RBAC denial (FR-010's clear-denial requirement) | NFR-015 |
| ERR-206 | 410 | "This sign-up code isn't valid." | Leadership/staff sign-up code unknown, revoked, or not in `Issued` status (BRL-077) | Q-238 |
| ERR-207 | 410 | "This sign-up code has expired. Ask your school for a new one." | Leadership/staff sign-up code past `expires_at_utc` (`CFG-010`) | Q-238 |
| ERR-208 | 409 | "This sign-up code has already been used and can't be changed." | Revoke or regenerate attempted on a consumed leadership/staff code (BRL-076) | Q-238 |

## 3xx — Academic Records (Students/Teachers/Classes/Subjects/Parent-link)

| ERR | HTTP | Message | Trigger | Trace |
|---|---|---|---|---|
| ERR-301 | 409 | "That register number or roll number is already in use." | BRL-018 duplicate | BR-018 |
| ERR-302 | 422 | "This student has no class placement yet — assign one before recording attendance." | BRL-019 | BR-019 |
| ERR-303 | 409 | "This class already has a class teacher, or this teacher is already a class teacher elsewhere." | BRL-023 | BR-023 |
| ERR-304 | 409 | "Reassign or release this teacher's class/subject assignments before removing them." | BRL-025 | BR-025 |
| ERR-305 | 409 | "A class with this name and section already exists." | BRL-026 | BR-026 |
| ERR-306 | 409 | "This class still has students placed in it." | BRL-028 | BR-028 |
| ERR-307 | 409 | "This code has already been used and can't be changed." | Revoke/regenerate on a consumed code | T-003 |
| ERR-308 | 410 | "This code has expired. Ask a staff member for a new one." | Parent-link code expired | T-003 |
| ERR-309 | 403 | "This code isn't valid for your school." | Institution mismatch on consume | T-003 |
| ERR-310 | 410 | "This code isn't valid." | Code not in `Issued` status | T-003 |

## 4xx — Attendance

| ERR | HTTP | Message | Trigger | Trace |
|---|---|---|---|---|
| ERR-401 | 423 | "This attendance record is locked and can no longer be changed." | 30-day lock (FR-035) | FR-035 |
| ERR-402 | 422 | "This correction needs approval from school leadership before it takes effect." | Day 1–29 correction by non-leadership creates a pending request, not an error — this code covers an attempt to *directly* write past-day data bypassing the pending flow | FR-034 |
| ERR-403 | 400 | "Attendance can't be recorded for a future date." | VAL-014 | — |

## 5xx — Communication (Announcements/Rooms/Messages/Meetings)

| ERR | HTTP | Message | Trigger | Trace |
|---|---|---|---|---|
| ERR-501 | 403 | "Only school leadership can withdraw an announcement." | BRL-038 | BR-038 |
| ERR-502 | 403 | "That passcode isn't correct." | BRL-041 | BR-041 |
| ERR-503 | 403 | "Only the room's creator or school leadership can remove this room." | BRL-042 | BR-042 |
| ERR-504 | 400 | "Messages can't be longer than 4,000 characters." | BRL-045 | BR-045 |
| ERR-505 | 403 | "You need to join this room before joining its meeting." | BRL-046 | BR-046 |
| ERR-506 | 409 | "Camera or microphone access was blocked. Check your browser permissions." | ACS Calling permission denial (Tech Design §13.1 risk 4) | NFR-026 |

## 7xx — Operations (Reporting/Scheduling)

| ERR | HTTP | Message | Trigger | Trace |
|---|---|---|---|---|
| ERR-701 | 409 | "This teacher is already scheduled for another class at this time." | BRL-053 | BR-053 |
| ERR-702 | 409 | "This class already has a period scheduled at this time." | BRL-054 | BR-054 |
| ERR-703 | 409 | "This room is already booked at this time." | BRL-055 | BR-055 |

## 8xx — Configuration / Commercial

| ERR | HTTP | Message | Trigger | Trace |
|---|---|---|---|---|
| ERR-801 | 402 | "This action would exceed your plan's limit. Upgrade your plan to continue." | BRL-073 tier-capacity gate | FR-073 |
| ERR-802 | 402 | "This feature isn't available on your current plan." | BRL-063 tier-capability gate | BR-063 |

## 9xx — Generic / Cross-cutting

| ERR | HTTP | Message | Trigger | Trace |
|---|---|---|---|---|
| ERR-901 | 400 | "Something about this request wasn't right — please check and try again." | Generic validation failure (falls back here only when no feature-specific `ERR-` applies) | — |
| ERR-902 | 404 | "We couldn't find what you're looking for." | Resource not found, or existence deliberately not disclosed to an unauthorized caller (see `07-business-logic/institutions.md` E2) | — |
| ERR-903 | 500 | "Something went wrong on our end. Please try again, and contact support if it continues." | Unhandled server error | company.md §5 "no swallowed exceptions" |
| ERR-904 | 429 | "You're doing that a bit too fast — please wait a moment and try again." | Rate limiting — **the three limited surfaces and their thresholds are specified in `09-security-spec.md` §10** (login `CFG-011`, account creation `CFG-012`, room-passcode attempts `CFG-013`). Every 429 in this product carries this code. | NFR-017, BRL-078, Q-240 |

**Response header on `ERR-904`:** a `Retry-After` header (seconds) accompanies every 429 so the
client can back off deterministically rather than guessing. This is the one error row that adds a
header to the standard response shape; it is stated here, not re-worded in `09-security-spec.md`.

## Traceability rule

Every `ERR-` row cites the `BR-`/`FR-`/`NFR-`/`T-` it enforces. No error is invented without a
requirement or design decision behind it — a code that exists only to make a controller compile
(e.g. a framework-level 400 for malformed JSON) maps to `ERR-901`/`ERR-903` rather than minting a
new, untraceable code.
