<!-- pipeline-provenance
produced_by: ai-spec-agent (ui-spec-writer)
generated: 2026-09-11
inputs:
  - .aidlc/02-solution-designer/ui-designer/ui-design-final.md §§0, 1, 2
  - .aidlc/02-solution-designer/ui-designer/design-tokens.md
revised: 2026-09-15 — spec_suite_version 1.0.4 (narrow patch, authorised by Rashmi Mehta at the
  Gate 4 CHANGES-REQUIRED verdict). **UI-005's Register panel restructured** (Q-238/Q-239, U38):
  the Role dropdown and the institution picker are removed from the privileged path and a
  sign-up-code field replaces them; Q-235 is superseded for privileged roles and still applies to
  the Student/Parent path. Every value cited is a named token from `design-tokens.md` — no literal
  hex or px was introduced. UI-001–004 and UI-006 are unchanged. CT-601 is STALE against UI-005.
-->

**Token-naming note (1.0.4):** `design-tokens.md` §2 declares the monospace family for "code/IDs
only" but gives it **no token name**, unlike every other type entry. UI-005's sign-up-code field
cites it by that declaration. Not treated as a Blocking gap — the value is declared and named in
prose in the token file, which is the authority — but the design system should give it a token
name so class-13 review has something mechanical to check against. Raised as **U42**.

# 11 — UI Spec — `public-auth` (public marketing + Auth modal + App shell)

Every visual value below cites a token name from `design-tokens.md` — never a literal hex/px.

## UI-001 — Home (`#/`)

**Roles:** unauthenticated. **Layout:** sticky header (logo, public nav, Login/Register CTA —
`color-surface` bg, `elevation-card` on scroll) → hero (`text-display` headline, `text-body-lg`
sub-copy, primary CTA using `color-primary`/`elevation-glow-primary`, secondary "Watch Demo" CTA
outline style, trial disclaimer `text-caption`, 4 stat tiles in `color-surface` cards
`radius-lg`/`elevation-card`, live platform-stats mini-chart card) → feature grid (4 cards,
`radius-xl`, `space-6` gutter) → testimonials (3 cards) → closing CTA band (dark surface, per
Tech Design §4.2 palette binding) → footer.
**Component tree:** `TopNav` → `Hero` → `StatTile`×4 → `FeatureCard`×4 → `TestimonialCard`×3 →
`CtaBand` → `Footer`.
**State matrix:** success only (static/decorative content) — the "Live" stats badge is
**decorative, not backed by a socket** (`06-event-contracts.md` §3); no empty/error/loading state
applies.
**Responsive:** feature grid 1-col below `bp-md`, 2-col at `bp-md`, 4-col at `bp-lg`; hero stacks
vertically below `bp-sm`.
**Accessibility:** hero CTA buttons have visible focus rings (`color-primary` at reduced
opacity); all stat tiles are text, not canvas-only, so they're screen-reader legible.
**Trace:** none (no FR/BR governs public marketing content) — included per charter "a source
record for every route."

## UI-002 — About (`#/about`)

**Roles:** unauthenticated. **Layout:** Mission/Vision cards (`text-h2` titles, `text-body`),
Core Values grid, Leadership Team grid (avatar + name + role, `radius-full` avatars).
**State matrix:** success only, static.
**Trace:** none.

## UI-003 — Contact (`#/contact`)

**Roles:** unauthenticated. **Layout:** 4 contact-method cards → 2-col (form + "Let's Start a
Conversation" panel). Form fields: Full Name*, Email*, School/Institution, Number of Students
(dropdown), Message*.
**State matrix:** empty (blank, default) → validation (required-field on submit; **on-blur email
format check**, improved-beyond-mockup per `ui-design-final.md` §1.4) → success (toast: "Message
sent — our team will respond within 24 hours," form resets).
**API:** `POST /api/v1/public/enquiries` (API-062) — a notification-dispatch, not a modeled CRM
entity (per the UI design's own framing).
**Accessibility:** every required field has an associated `<label>` and `aria-required`; the
on-blur email error is announced via `aria-live="polite"`.
**Trace:** FR-069.

## UI-004 — Pricing (`#/pricing`)

**Roles:** unauthenticated. **Layout:** monthly/yearly billing toggle → 3 plan cards
(Entry/Mid/Bespoke — BR-061, "Most Popular" badge on the mid tier using
`color-primary-subtle-bg`/`color-primary-subtle-text`) → feature-comparison table
(`DataTable` component) → FAQ accordion.
**Structural fields shown (traced to BR-061/062 only):** tier name, learner capacity, storage
allowance, support level, price. **The comparison table's per-row feature-availability cells are
populated from `SubscriptionPlan.capabilities` (§04-data-model §2.10) at render time — never
hardcoded copy** — this is the load-bearing constraint from the operator's binding instruction:
the mockup's own `features` array text is a **validation-required mockup artifact**, not built
verbatim; whatever the table shows must match what the entitlement model (BR-063,
`07-business-logic/configuration-commercial.md`) actually enforces server-side, or it is a real
product defect (overselling a tier), not a copy nit.
**State matrix:** billing-toggle is a client-only re-render (monthly/yearly price display) — no
loading/error state possible.
**API:** `GET /api/v1/public/plans` (API-061).
**Trace:** BR-061, BR-062, BR-063 (structure and gating only — not the mockup's specific feature
copy, per `01-source-catalogue.md` §5).

## UI-005 — Auth modal (login / register)

**Roles:** unauthenticated → authenticated on success. **Mounted over:** any public route (no
separate `#/login` page — `ui-design-final.md` §0).
**Login fields:** Username*, Password*, Remember me, "Forgot password?" link.

**Register fields — REVISED at 1.0.4 (Q-238/Q-239, U38). The Role dropdown and the School
typeahead are removed from the privileged path; a sign-up-code field takes their place.** The
form has **two mutually exclusive modes**, selected by a segmented control at the top of the
Register panel:

| Mode | Segmented-control label | Fields | Submits |
|---|---|---|---|
| **A — Student or Parent** (default) | "I'm a student or parent" | Username*, Email*, Password*, **I am a\*** (radio group, exactly two options: Student / Parent), School* (typeahead against `GET /public/institutions`, API-005 — Q-235 still applies **here only**) | `{username, email, password, role, institutionId}` |
| **B — School staff** | "I have a school sign-up code" | Username*, Email*, Password*, **Sign-up code\*** (single text input) | `{username, email, password, staffCode}` |

**What was removed and why.** The prior Role dropdown listed all seven roles minus
`PlatformOperator` (the Q-202 provisional choice) and the School typeahead supplied an
`institutionId` alongside it. That pairing is the client half of REV-001: an anonymous user chose
`Administrator`, chose any school from the public directory, and got a leadership session.
**The dropdown was never the boundary — the server was, and the server agreed with it.** In mode B
the client sends **no role and no institution at all**; both come from the redeemed code
(BRL-075/077). A hand-crafted request adding them is ignored server-side (E4).

**Q-235 is SUPERSEDED for privileged roles.** "The register form submits the id of the school
chosen from the public directory" remains correct and unchanged for **mode A** (Student/Parent).
It does not apply to mode B, where the school is a property of the code. Nothing about API-005's
public directory changes, and it still publishes institution ids — that is now safe, because the
id alone no longer buys a privileged account.

**Sign-up code field spec:**
- Label "Sign-up code", helper text below at `text-caption` in `color-text-secondary`: *"Your
  school gives you this code. It's valid for 7 days."* (`CFG-010` — the copy states the duration
  in words rather than reading the constant, so it must be revisited if `CFG-010` changes.)
- Rendered in the **monospace family** from `design-tokens.md` §2 (the "code/IDs only" family) —
  a sign-up code is an identifier, not prose, and a monospaced field makes `0`/`O` and `1`/`l`
  distinguishable when a parent reads it off a printed slip.
- `autocapitalize="characters"`, `autocomplete="off"`, `spellcheck="false"`. Whitespace is
  trimmed and case is normalised **client-side before submit** (VAL-027), so a pasted code with a
  trailing space is not a failed attempt against `CFG-012`'s rate limit.
- Never pre-filled from a URL query parameter. A code in a link is a code in a browser history and
  a referrer header.

**State matrix:**
- empty (blank form, mode A selected)
- validation (native required + **live email-format and password-policy validation**,
  improved-beyond-mockup — the mockup accepts any string)
- loading (disabled button + spinner on submit — auth calls are never sub-100ms, threshold: any
  submit)
- error — inline error banner above the form at `color-error`, `text-body-sm`; password field
  cleared, all others retained **including the sign-up code** (a valid code mistyped alongside a
  weak password should not have to be re-entered):
  - invalid credentials `ERR-202` (login)
  - duplicate username/email `ERR-203`
  - password policy `ERR-201`
  - **`ERR-206`** — code unknown, revoked or already used. **One message for all three**, matching
    the catalogue wording verbatim; the UI must not distinguish them, or it becomes an oracle for
    which codes exist
  - **`ERR-207`** — code expired
  - **`ERR-904`** — rate limited (§10, `CFG-012`); the banner shows the catalogue wording and the
    submit button stays disabled for the `Retry-After` interval rather than inviting a retry that
    will also fail
  - unknown school (mode A only)
- success (login: toast + close modal + redirect to `/dashboard`; register: "Registration
  successful" toast + immediate session per FR-006, no separate login step needed — **identical
  for both modes**; the redeemed role is simply whatever the session says, and the shell's
  role-filtered nav (UI-006) picks it up with no special casing)

**Navigation:** modal close (X/backdrop) discards without confirmation (no destructive risk).
Login↔Register toggle inside the modal resets the form. **Switching between mode A and mode B
clears the mode-specific fields** (role/school, or code) but retains username/email, so a user who
picked the wrong mode does not retype everything.
**Responsive:** the segmented control stacks to two full-width buttons below `bp-sm`; the modal is
full-height below `bp-sm` per the `_shared.md` modal pattern.
**Accessibility:** modal traps focus; `Escape` closes it; all fields labeled; error banner has
`role="alert"`. The mode segmented control is a **radio group** (`role="radiogroup"`, arrow-key
navigable), not two buttons — changing it changes which fields exist, and that must be announced.
The "I am a" Student/Parent choice is likewise a radio group with a visible legend, not a
`<select>`: with exactly two options a dropdown hides one of them behind a click.
**API:** API-006 (register, both modes), API-007 (login).
**Trace:** FR-006, FR-007, SPEC-FR-006 (as corrected), BRL-075/077, VAL-007/027,
`09-security-spec.md` §1a, Q-238, Q-239. Errors: `ERR-201/202/203/206/207/904`.

## UI-006 — App shell (side nav + header + profile menu)

**Roles:** all 7, content filtered per role. **Layout:** fixed-width dark side nav (`[NEEDS
ANSWER — Q-213, non-blocking]`: the sidebar width has no named token yet — `design-tokens.md`
flagged this to Tech Design/theme-designer and it was not resolved before hand-off; provisional
choice: treat it as a new structural token `layout-sidenav-width = 16rem` to be added to
`design-tokens.md` at build time, not a literal `256px` in code) with logo, role-filtered nav
items (§Nav model below), Log out pinned to bottom. Header: hamburger (mobile, `<bp-lg`), page
title + **real breadcrumb reflecting actual route nesting** (improved-beyond-mockup — the mockup's
fixed "Home" breadcrumb is not carried forward), decorative search box (not wired to a real
endpoint this release — no FR backs it), decorative notification bell (no FR backs a real
notification center/inbox — `06-event-contracts.md` §3), profile menu (name/role/school, **no
role switcher** — `ui-design-final.md` §0 confirms this is mockup-only, never built — Account
settings link, Log out).

**Nav model (role-filtered, per `09-security-spec.md` §2 and the 7 corrections in
`ui-design-final.md`):**

| Nav item | Route | Roles |
|---|---|---|
| Dashboard | `/dashboard` | all 7 |
| Classes | `/dashboard/classes` | leadership + **Teacher** (FR-027 correction) |
| Teachers | `/dashboard/teachers` | leadership only |
| Students | `/dashboard/students` | leadership + Teacher |
| Attendance | `/dashboard/attendance` | leadership + Teacher |
| Announcements | `/dashboard/announcements` | all 7 |
| Meetings | `/dashboard/meeting` | all 7 |
| Schedule | `/dashboard/schedule` | leadership + Teacher (view for Teacher); **no "soon" badge** |
| Reports | `/dashboard/reports` | leadership only; **no "soon" badge** |
| Settings | `/dashboard/settings` | all 7 (content scoped per role) |

**Student/Parent accessible set is a strict subset** (Dashboard, Attendance own/linked-children,
Announcements, Meetings, Settings own-account) — not the general list with read-only variants.
**A deep link outside a role's actual set renders `screenUnauthorized()` (UI-019), not merely a
hidden nav entry** — server-side authorization is the real boundary (FR-009); the nav config is a
UX mirror only.

**State matrix:** sidebar collapse/expand (`<bp-lg`, off-canvas + backdrop-dismiss); profile menu
open/closed (dismiss on outside click **or Escape**, improved-beyond-mockup).
**Accessibility:** nav items are a `<nav>` landmark with `aria-current="page"` on the active
route; profile menu is a proper disclosure widget (`aria-expanded`).
**Trace:** NFR-001, FR-009, FR-010, the 7 UI corrections (`ui-design-final.md` hand-off summary).
