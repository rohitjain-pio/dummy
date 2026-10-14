<!-- pipeline-provenance
produced_by: ai-spec-agent (ui-spec-writer)
generated: 2026-09-11
-->

# 11 — UI Spec — `configuration-commercial`

## UI-018 — Settings (`/dashboard/settings`)

**Roles — role-gated, a real gap the mockup didn't model, resolved and approved 2026-09-11
(`ui-design-final.md` §12, final and binding):**

**Leadership view:** School Profile card (name, registration number, email, phone, address, plan
— edit action, per FR-002) + 4 config-section cards (General/User Management/Notifications/
Academic, per FR-063/064/065/066) each with per-item "Configure" action → System Information
(version, last-updated, status, storage — informational chrome only, no BRD backing, kept as
non-interactive display).

**Non-leadership (Student/Parent/Teacher) view:** **own-account fields only** — name/contact,
password change, notification preferences for their own account. **A materially smaller screen,
not the leadership view with sections hidden** (approved, final) — the leadership config
sections do not exist in the DOM for these roles, they are not merely hidden by CSS.

**Configure modal (leadership):** generic setting/value/applies-to pattern. **Every save writes an
audit-log entry unconditionally — this is not a user-toggleable checkbox**, even though the
mockup's `modalConfigure` shows one (BR-059 is unconditional; the checkbox is dropped from the
built UI, not merely defaulted-on).

**State matrix:** loading (settings fetch) · success · save-confirmation (toast).
**API:** API-056–060, API-002 (institution profile edit). **Trace:** FR-002, FR-063–066, BR-057–059.

## Cross-reference — Pricing screen

Pricing (`#/pricing`) is specified in `11-ui-spec/public-auth.md` §UI-004 since it is a public,
unauthenticated screen, not part of the authenticated Settings/Subscription surface — but its
feature-list/comparison-table content is governed by the same BR-061/062/063 entitlement model
this file's Settings screen (institution plan display) also reads from `SubscriptionPlan`.
