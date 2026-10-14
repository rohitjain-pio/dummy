<!-- pipeline-provenance
produced_by: ai-spec-agent (ui-spec-writer)
generated: 2026-09-11
-->

# 11 — UI Spec — `communication`

## UI-011 — Announcements (`/dashboard/announcements`)

**Roles:** view — all 7; post/edit — leadership + Teacher (BR-037); **withdraw (delete icon) —
leadership only**, narrower than the post/edit set (BR-038 correction, `ui-design-final.md` §8,
final and binding — the mockup's delete icon was shown to the full `canPost` set including
Teacher; corrected).
**Layout:** search bar + "New Announcement" (if permitted) → card feed (scene image, author
avatar+name+date, title, view/edit/delete icons **only as permitted per role above**, excerpt,
"Read more").
**View modal:** full title, author+date, full content, "visible to all members of [school]" note.
**Create/Edit modal:** title, content, effective date (FR-038).
**State matrix:** empty ("No announcements found — nothing matches your search right now") ·
loading (skeleton cards) · success · error (`ERR-501` on a Teacher attempting withdraw — action
not offered in the UI at all for Teacher, so this only triggers via a stale client state, e.g.
after a role/tier change mid-session).
**API:** API-037–040. **Trace:** FR-038–042, BR-036–039.

## UI-012 — Meetings / room list (`/dashboard/meeting`)

**Roles:** all 7 (BR-040). **Layout:** "+ Create Room" → card grid (cover image, participant
avatar stack, name, creator, description, "passcode protected" badge — **never the passcode
value**, participant count, Join/Chat/Video actions, delete icon shown only to creator-or-
leadership per BR-042).
**Create-room modal:** name*, description (optional), passcode* (FR-043).
**Join-room modal:** read-only room name + passcode entry (FR-044).
**State matrix:** empty ("No rooms yet — create one to get started") · loading (skeleton cards) ·
success · error (`ERR-502` wrong passcode, inline on the join modal).
**API:** API-041–043, 045. **Trace:** FR-043/044/051, BR-040–042.

## UI-013 — Chat (`#/chat/:roomId`)

**Roles:** room members (post-join). **Layout:** header (back, room name, live participant count,
"Start Video Call", "Leave Room") → message log (history-loaded marker, message bubbles, typing
indicator) → composer (text input, max 4,000 chars per `CFG-003`/BRL-045 with a live character
counter, + Send) → participants sidebar (desktop only, presence dot per FR-047).
**State matrix:** loading (history fetch — skeleton bubbles) · empty ("No messages yet — say
hello") · success · **real-time presence** — join/leave/typing events genuinely wired to
EVT-002/003/004 (improved-beyond-mockup: the mockup's typing indicator is a static hard-coded
line; the real build shows/hides it live) · error (`ERR-504` message-too-long, inline on
composer before send is even attempted — client-side length guard mirrors the server rule).
**API:** API-044, 046. **Events:** EVT-001–004, 006. **Trace:** FR-045–047, FR-050, BR-043–045.

## UI-014 — Video call (`#/video-call/:roomId`)

**Roles:** room members. **Layout:** dark full-screen, header (back, call title, participant
count, LIVE badge), participant tile grid (portrait, name label, muted indicator), control bar
(mic toggle, camera toggle, screen-share, jump-to-chat, Leave Call).
**State matrix:**
- **connecting** (per-tile spinner until each participant's stream resolves — improved-beyond-
  mockup, the mockup enters "Connected" instantly)
- **connected** (as designed)
- **reconnecting** (a dropped participant's tile shows a reconnecting overlay, never vanishes —
  ties to NFR-012 and Tech Design §13.1's still-open ACS-reconnection risk; build defensively,
  do not assume the media session silently self-heals)
- **error** (camera/mic permission denied — inline "how to enable" message on that participant's
  own tile, never a silent failure)
**Navigation:** Leave Call returns to the room list — no confirmation needed (live session, no
unsaved form state).
**API:** API-047. **Events:** EVT-005. **Trace:** FR-048–050, NFR-009, NFR-026, Tech Design §13.1
(5 carried-forward risks — this screen's connecting/reconnecting/error states are the
constitution's mandated defensive posture for those risks, not optional polish).
