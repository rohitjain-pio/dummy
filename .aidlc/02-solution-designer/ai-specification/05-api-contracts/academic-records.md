<!-- pipeline-provenance
produced_by: ai-spec-agent (contract-writer)
generated: 2026-09-11
-->

# 05 — API Contracts — `academic-records`

## Students

**API-011** `POST /api/v1/students` — Auth: leadership. Request: `{srNumber, rollNumber, name,
dateOfBirth, gender, email, classId, guardianFatherName?, guardianMotherName?,
guardianContactEmail?, guardianPhone?}`. Validation: VAL-008–011. Success `201`. Errors: `ERR-301`,
`ERR-901`.

**API-012** `PUT /api/v1/students/{id}` — Auth: leadership. Amend + class-placement change
(BRL-017). Success `200`. Errors: `ERR-301`, `ERR-902` (unknown student id, or a student of
another institution — the tenant filter makes it invisible, not forbidden). A role denial (a
Teacher attempting this write) is a framework-emitted bodyless `403`, consistent with the other
leadership-only write endpoints; no catalogue body is returned for it. *(1.0.3, U3.)*

**API-013** `POST /api/v1/students/{id}/withdraw` — Auth: leadership. Success `200`, reversible
(`/reactivate` mirror endpoint exists per the soft-delete pattern).

**API-014** `GET /api/v1/students/{id}` · `GET /api/v1/students` · `GET /api/v1/classes/{id}/roster`
— Auth: leadership + Teacher (read). Institution-scoped.

**API-015** `PUT /api/v1/students/{id}/link-account` — Auth: leadership. `{userAccountId}`.

**API-016** `POST /api/v1/students/{id}/parent-link-codes` — Auth: leadership. Success `201` →
`{code, expiresAtUtc}` (`CFG-005`: 8 chars, 7-day expiry). Errors: `ERR-901`.

**API-017** `POST /api/v1/students/{id}/parent-link-codes/{codeId}/revoke` — Auth: leadership.
Errors: `ERR-307` (already consumed).

**API-018** `POST /api/v1/parent-links` — Auth: authenticated Parent. Request: `{code}`. Success
`200` → `{studentId, linkedAtUtc}`. Errors: `ERR-308` (expired), `ERR-309` (institution mismatch),
`ERR-310` (invalid). See PF-005 (`10-process-flows/academic-records.md`) for the full sequence.

**API-019** `GET /api/v1/students/me` (Student) / `GET /api/v1/students/linked` (Parent) —
scoped per FR-074/075.

## Teachers

**API-020** `POST /api/v1/teachers` — Auth: leadership. `{name, contactEmail, telephone, gender,
address, joiningDate}`. Success `201`.

**API-021** `PUT /api/v1/teachers/{id}` · **API-022** `POST /api/v1/teachers/{id}/remove` — Auth:
leadership. `ERR-304` on active-assignment guard (BRL-025).

**API-023** `GET /api/v1/teachers/{id}` · `GET /api/v1/teachers` — Auth: leadership only (matches
the mockup's own gating, no BRD grant to Teacher for a peer directory — `ui-design-final.md` §5).

**API-024** `PUT /api/v1/classes/{id}/class-teacher` — Auth: leadership. `{teacherId}`. Errors:
`ERR-303` (BRL-023).

**API-025** `POST /api/v1/subjects/{id}/teacher-assignments` — Auth: leadership.
`{teacherId, classId}`. API-030 is an intended alias of this endpoint (same request, same
behaviour, same `SubjectTeacherClassMapping` table). *(1.0.3, U1.)*

## Classes / Subjects

**API-026** `POST /api/v1/classes` — `{name, section}`. Errors: `ERR-305`.
**API-027** `PUT /api/v1/classes/{id}` · `DELETE /api/v1/classes/{id}` — Errors: `ERR-306`
(BRL-028).
**API-028** `GET /api/v1/classes/{id}` · `GET /api/v1/classes` — Auth: leadership + **Teacher**
(FR-027 correction).
**API-029** `POST/PUT/DELETE /api/v1/subjects` — Auth: leadership. `DELETE /subjects/{id}`
removes the subject's `SubjectTeacherClassMapping` rows with it; no error is raised for existing
mappings (subject-deletion rule, `07-business-logic/academic-records.md`). *(1.0.3, U5.)*
**API-030** `POST /api/v1/subjects/{id}/class-mappings` — Auth: leadership. **Intended alias of
API-025:** same request body `{teacherId, classId}`, same behaviour, same single table
`SubjectTeacherClassMapping` (composite PK `subject_id, teacher_id, class_id`). Both routes are
kept; neither is removed. *(1.0.3, U1.)*

## Idempotency (applies to every `POST` above)

All create endpoints accept `Idempotency-Key`; a repeated key returns the original response
rather than creating a duplicate resource — this is a transport-layer safeguard distinct from the
BR-018/BR-026 business-uniqueness checks, which fire regardless of key reuse.
