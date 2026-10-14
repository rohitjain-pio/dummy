<!-- pipeline-provenance
produced_by: ai-spec-agent (test-spec-writer)
generated: 2026-09-11
revised: 2026-09-15 — spec_suite_version 1.0.4 (narrow patch). Added TC-511 (room-passcode rate
  limit, Q-240 / U39 — closes U16). No existing TC- changed.
-->

# 14 — Test Spec — `communication`

| TC ref | Layer | Given | When | Then | Trace | Adversarial? |
|---|---|---|---|---|---|---|
| TC-501 | Integration | Room created with a passcode | `GET /rooms` list | Response contains no passcode field at all | SPEC-FR-043 | **Yes** — fails if the DTO leaks the hash or plaintext |
| TC-502 | Integration | Room exists | Join attempt with wrong passcode | `ERR-502`, no group join, no history returned | BRL-041 | **Yes** |
| TC-503 | E2E | Two clients in the same room | Client A sends a message | Client B receives it via `EVT-001` within 1s (NFR-008) | FR-045 | — |
| TC-504 | Unit | Message content of exactly 4,001 characters | Send attempted | `ERR-504` | BRL-045 | **Yes** — boundary test |
| TC-505 | Integration | Teacher authored an announcement | Teacher attempts `POST /announcements/{id}/withdraw` | `ERR-501` 403 | BRL-038 | **Yes** — fails against an implementation that allows any `canPost` role to withdraw |
| TC-506 | E2E | Client disconnects mid-session, reconnects | Reconnect completes | Client re-fetches history via `GET /rooms/{id}/messages`, sees no gap | NFR-012 | — |
| TC-507 | Contract | Caller is not a member of the room | `POST /rooms/{id}/meetings/token` | `ERR-505` 403, no ACS token issued | BRL-046 | **Yes** |
| TC-508 | E2E | Meeting in progress, one participant's connection drops | Reconnect window elapses | Tile shows "reconnecting," not vanished, per NFR-012 | Tech Design §13.1 risk 5 | — |
| TC-509 | E2E | Browser denies camera/mic permission | Participant attempts to join | Inline "how to enable" message on that tile, join not silently failed | Tech Design §13.1 risk 4 | — |
| TC-510 | Unit | Message withdrawn by its author | Withdrawn message rendered | Content blanked, author still shown, row not deleted | BRL-044 | **Yes** — fails against an implementation that deletes the row |
| **TC-511** | **Integration** | An authenticated member-eligible user and a room with a known-wrong passcode | `POST /rooms/{id}/join` (API-043) repeated past `CFG-013` | `429 ERR-904` with `Retry-After`, **and no `RoomMembership` row exists** for that user and room. The limiter is partitioned by `(user id, room id)`: the same user joining a *different* room with the correct passcode still succeeds while throttled on the first. Threshold read from configuration — the test lowers `CFG-013` and asserts the trip point moves | NFR-017, BRL-078, `CFG-013`, N17, **closes U16** | **Yes** — added 1.0.4. A `VAL-018`-length passcode is brute-forceable at wire speed with no limit; this fails against the pre-fix build, which had none |
