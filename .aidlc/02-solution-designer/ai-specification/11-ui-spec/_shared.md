<!-- pipeline-provenance
produced_by: ai-spec-agent (ui-spec-writer)
generated: 2026-09-11
-->

# 11 — UI Spec — `_shared` (utility screens + cross-cutting UI patterns)

## UI-019 — Unauthorized (`screenUnauthorized()`, not nav-reachable)

**Trigger:** a deep link is opened to a route outside the signed-in role's actual accessible set
(`11-ui-spec/public-auth.md` §UI-006's nav model) — this is the real enforcement surface for the
"nav hides it, but a deep link must still be blocked" rule (server-side authorization is the
actual boundary; this screen is what the client shows when the server denies it).
**Copy:** "Your current role does not have permission to view this area." Actions: Go Back, Go to
Dashboard.
**Trace:** FR-009, FR-010, NFR-003.

## UI-020 — Not Found (`screenNotFound()`, not nav-reachable)

**Trigger:** an unknown route. **Copy:** standard 404. Action: Back to Home.
**Trace:** NFR-003 (explicit, non-technical feedback).

## Cross-cutting UI patterns (apply to every screen in this folder)

- **Destructive-action confirmation (NFR-003):** every delete (class, teacher, student,
  announcement, room) shows a dedicated confirmation modal naming what will be deleted and
  stating "This action cannot be undone," before the delete proceeds (real mockup pattern,
  `confirmDelete()`, carried forward unchanged).
- **Non-destructive cancel:** every other modal's Cancel/Close discards in-progress input with no
  confirmation (short, low-cost forms per BRD scope).
- **Toast pattern:** success/failure feedback via toast (`toastHost` pattern) for every mutation,
  matching NFR-003's "explicit, non-technical feedback."
- **Empty/loading states:** every list/detail/dashboard screen across this folder has a designed
  empty state and a designed loading state (skeleton, never a bare spinner-only screen for
  multi-entity aggregations) per NFR-004 — enumerated per-screen above, never left implicit.
- **Responsive breakpoints:** every screen is verified at `bp-sm`/`bp-md`/`bp-lg`/`bp-xl`
  (NFR-025); side nav collapses to off-canvas below `bp-lg` (§UI-006).
- **Accessibility baseline (NFR-005):** WCAG 2.1 AA across every screen in this folder; the one
  known token-level defect (the mockup's "Late" pill contrast) is already corrected at the token
  layer (`design-tokens.md` §1a) — every screen must consume `color-warning-bg`/`color-warning-text`,
  never the original `amber-500`+white pairing.

## Explicit non-requirements carried into this folder (do not build)

- **Role switcher** (mockup's `roleSwitcher`, profile menu) — confirmed mockup-only dev
  convenience; a real account is bound to one role at registration (BR-008). Never built.
- **Payment, Leave nav entries** — out of scope regardless of the mockup rendering them
  (FR-081/082 excluded); not built even as "coming soon" placeholders.
- **"Student Satisfaction"/"Course Completion" report gauges** — see `11-ui-spec/operations.md`
  §UI-017; omitted entirely, not deferred.
