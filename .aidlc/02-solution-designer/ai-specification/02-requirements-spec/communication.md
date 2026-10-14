<!-- pipeline-provenance
produced_by: ai-spec-agent (source-analyser)
generated: 2026-09-11
-->

# 02 — Requirements Spec — `communication`

Traces: BRD §6.7 (Announcements), §6.8 (Collaboration/Messaging/Meetings) · ENT-009/010/011/012 ·
ROLE-001–007. Design trace: Tech Design §3.4, §3.9, §6, §8.4.

## Announcements (FR-038–042)

| SPEC ID | Source | Statement | Acceptance criteria |
|---|---|---|---|
| SPEC-FR-038 | FR-038 | Leadership + Teachers create an announcement (title, content, effective date) | `POST /api/v1/announcements` |
| SPEC-FR-039 | FR-039 | Author recorded on every announcement | `AuthorId` non-nullable, immutable |
| SPEC-FR-040 | FR-040 | Leadership + Teachers amend published content | `PUT /api/v1/announcements/{id}` |
| SPEC-FR-041 | FR-041 | Leadership withdraws an announcement | `POST /api/v1/announcements/{id}/withdraw` — leadership roles only, **not** Teacher (BR-038) |
| SPEC-FR-042 | FR-042 | All authenticated users view current + individual announcements | `GET /api/v1/announcements`, `GET /api/v1/announcements/{id}` — any authenticated role of the institution |

## Collaboration / Messaging / Meetings (FR-043–051)

| SPEC ID | Source | Statement | Acceptance criteria |
|---|---|---|---|
| SPEC-FR-043 | FR-043 | Create a named room (description, passcode); passcode never shown in room list | `POST /api/v1/rooms`; passcode stored hashed; `GET /api/v1/rooms` list response never includes the passcode field |
| SPEC-FR-044 | FR-044 | Browse rooms; join with correct passcode | `POST /api/v1/rooms/{id}/join {passcode}`; wrong passcode → `ERR-5xx` |
| SPEC-FR-045 | FR-045 | Real-time text messages delivered to all present participants | SignalR `EVT-001 MessageSent` fan-out to the room group |
| SPEC-FR-046 | FR-046 | Message history retained, presented on join | `GET /api/v1/rooms/{id}/messages` returns history; client loads it on join/reconnect |
| SPEC-FR-047 | FR-047 | Presence, typing, join/leave visible live | `EVT-002 PresenceChanged`, `EVT-003 TypingChanged`, `EVT-004 ParticipantJoined/Left` |
| SPEC-FR-048 | FR-048 | Start/join a live audio/video meeting tied to a room, multi-participant | `POST /api/v1/rooms/{id}/meetings/token` issues an ACS token; browser SDK establishes the media session |
| SPEC-FR-049 | FR-049 | Control own mic/camera, state visible to others | Client-side ACS SDK call + `EVT-005 MediaStateChanged` broadcast |
| SPEC-FR-050 | FR-050 | Leave room/meeting, others notified | `EVT-004 ParticipantLeft` on room leave; ACS SDK leave-call event on meeting leave |
| SPEC-FR-051 | FR-051 | Room creator or leadership removes a room | `DELETE /api/v1/rooms/{id}` — creator or leadership only (BR-042) |

## Business rules

| BRL ref | Statement |
|---|---|
| BR-036 | Announcements visible to every authenticated user of the publishing institution only |
| BR-037 | Announcements created/amended by leadership and Teachers |
| BR-038 | Announcements withdrawn by leadership **only** — narrower than the create/amend set, per `ui-design-final.md` §8 correction |
| BR-039 | Author + date retained for accountability |
| BR-040 | Only authenticated users create/join/participate in rooms |
| BR-041 | Room joinable only with correct passcode |
| BR-042 | Room removable only by creator or leadership |
| BR-043 | Message history retained for room's life, attributable |
| BR-044 | Message revisable/withdrawable by author; withdrawn messages remain accounted for, not erased |
| BR-045 | Message length bounded (`CFG-003` = 4,000 characters, D3) |
| BR-046 | Meeting participation limited to users present in the associated room |
| BR-047 | Rooms/meetings governed solely by auth + passcode + RBAC — no additional safeguarding rule this release |

## NFR references

SPEC-NFR-008 (chat <1s), SPEC-NFR-009 (meeting quality), SPEC-NFR-012 (reconnect + history
restore), SPEC-NFR-019 (passcode gating), SPEC-NFR-030 (message attribution).

## Full detail

`07-business-logic/communication.md` · `05-api-contracts/communication.md` ·
`06-event-contracts.md` (all `EVT-` definitions) · `08-error-catalogue.md` (`ERR-5xx`/`ERR-6xx`) ·
`10-process-flows/communication.md` (PF-008 room join, PF-009 meeting join) ·
`11-ui-spec/communication.md` (Announcements, Meetings/Chat/Video-call screens).
