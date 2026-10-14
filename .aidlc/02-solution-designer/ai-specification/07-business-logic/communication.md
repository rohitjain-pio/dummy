<!-- pipeline-provenance
produced_by: ai-spec-agent (business-logic-writer)
generated: 2026-09-11
-->

# 07 — Business Logic — `communication`

## BRL — Announcements

| BRL ref | IF | THEN | ELSE |
|---|---|---|---|
| BRL-036 (BR-036) | Announcement viewed | caller is an authenticated user of the publishing institution | else not visible (not merely hidden — not returned by the list/detail endpoint at all) |
| BRL-037 (BR-037) | Announcement created/amended | caller role ∈ leadership ∪ {Teacher} | else denied |
| BRL-038 (BR-038) | Announcement withdrawn | caller role ∈ leadership **only** (narrower than BRL-037 — Teacher may create/amend but not withdraw) | else denied `ERR-501` |
| BRL-039 (BR-039) | Announcement created | author + date recorded, never overwritten by later amendment | amendment updates content/effective-date only, `AuthorId`/`CreatedAtUtc` immutable |

## BRL — Rooms / Messaging / Meetings

| BRL ref | IF | THEN | ELSE |
|---|---|---|---|
| BRL-040 (BR-040) | Room create/join/participate requested | caller is authenticated | 401 |
| BRL-041 (BR-041) | Room join requested | supplied passcode matches the room's (hashed) passcode | reject `ERR-502` "incorrect passcode" — no passcode value ever echoed back |
| BRL-042 (BR-042) | Room removal requested | caller is the room's creator OR institution leadership | else denied `ERR-503` |
| BRL-043 (BR-043) | Message sent | retained for the room's life, attributable to author | — |
| BRL-044 (BR-044) | Message revise/withdraw requested | caller is the message's author | else denied; withdrawal sets a `WithdrawnAtUtc` flag and blanks displayed content, but the row + author link is never deleted |
| BRL-045 (BR-045) | Message submitted | length ≤ `CFG-003` (4,000 characters) | reject `ERR-504` "message too long" |
| BRL-046 (BR-046) | Meeting join requested | caller is currently a participant of the associated room | else denied `ERR-505` |
| BRL-047 (BR-047) | Any room/meeting access decision | governed solely by auth + passcode + RBAC | no additional moderation/supervision layer exists — a future maintainer must not silently add one (constitution.md §2.9) |

## State machine — Room membership (BR-040/041/046)

```
NotAMember --[POST /rooms/{id}/join {passcode}: correct]--> Member (group-joined for SignalR)
Member --[leave / disconnect]--> NotAMember
Member --[meeting starts in this room]--> Member+MeetingEligible
```

**Guard:** the passcode is verified **once**, at join — never re-checked per message (Tech Design
§3.4). A `Member`'s SignalR group membership is the authorization boundary for subsequent
messages/events, not a per-message passcode re-check.

## VAL — field validation

| VAL ref | Field | Rule |
|---|---|---|
| VAL-016 | `announcement.title` | Required, non-empty |
| VAL-017 | `announcement.effective_date` | Required, valid date (may be future-dated — BRD does not restrict to today-or-past) |
| VAL-018 | `room.name` | Required, non-empty |
| VAL-019 | `room.passcode` | Required at creation; stored hashed, never returned in any response after creation |
| VAL-020 | `message.content` | 1–4,000 characters (BRL-045) |

## Edge-case register

| # | Case | Resolution |
|---|---|---|
| E1 | A Teacher who created an announcement tries to withdraw it | Denied per BRL-038 — withdrawal is leadership-only regardless of authorship, a deliberate narrowing corrected during UI design (`ui-design-final.md` §8) |
| E2 | A room's creator account is later deactivated (e.g. teacher removed) | Room persists; leadership retains the ability to remove it (BRL-042's "OR institution leadership" clause exists precisely for this case) |
| E3 | A withdrawn message is later viewed by a new participant joining the room | History includes the withdrawn message in its withdrawn state (content blanked, author still shown) — "accounted for... rather than erased" (BR-044) means the *position in the log* is preserved, not the readable content |
