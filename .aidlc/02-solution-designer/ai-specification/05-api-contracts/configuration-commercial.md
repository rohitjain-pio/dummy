<!-- pipeline-provenance
produced_by: ai-spec-agent (contract-writer)
generated: 2026-09-11
revised: 2026-09-15 — spec_suite_version 1.0.4 (narrow patch). **Written back from the P5 build
  (U26):** API-063 is **no longer anonymous** (Q-230 — it previously let anyone burn any school's
  one free trial), API-064's `startDate` column and the role-re-read rule, the rollover supporting
  endpoints API-059a/059b, API-059's once-only semantics, the API-027 class-delete retrofit, and
  API-069 for the plan editor built unnumbered in CT-503. **The code is the source of truth for
  all of these; no code change is implied.** U28, U30, U31 and U32 are referenced and **remain
  open**. `API-` range is now 001–069.
-->

# 05 — API Contracts — `configuration-commercial`

## Institution configuration / academic year

**API-056** `GET/PUT /api/v1/institution-config` — Auth: leadership (write), any authenticated
user of the institution (read, where relevant e.g. time zone for display). `{academicYear,
timeZone, language}`.
**API-057** `PUT /api/v1/institution-config/notifications` — `{email, sms, inPlatform}` prefs.
**API-058** `PUT /api/v1/institution-config/academic` — `{gradingScale, courseCategories,
assessmentTypes, reportCardFormat}`.
**API-059** `POST /api/v1/academic-years/{id}/rollover` — Auth: leadership. Success `202` (async
job, PF-008).
**API-060** `POST /api/v1/academic-years/{id}/rollover/hold-back` — `{studentIds}`. Auth:
leadership. Applied before or after the rollover job runs (FR-067). **A list containing a foreign
or withdrawn student is rejected whole, with nothing recorded** (TC-703).

### Supporting endpoints for rollover (written back at 1.0.4 — U26, built in CT-502)

**No contract created an `AcademicYear` or set a successor class**, yet API-059 needs both. These
were built minimally rather than assumed:

**API-059a** `POST /api/v1/academic-years` · `GET /api/v1/academic-years` — Auth: leadership.
`{label, startDate, endDate}`.
**API-059b** `PUT /api/v1/classes/{id}/next-class` — Auth: leadership. `{nextClassId | null}` —
sets `Class.next_class_id` (§2.5); **null means final year** (Q-229).

> These carry `a`/`b` suffixes deliberately: they are the endpoints API-059 depends on, and
> suffixing keeps them beside it without renumbering API-060–064, which are built and referenced
> across the suite. **No existing `API-` id moved.**

**API-059 rollover semantics, written back (Q-229):** a year rolls over **exactly once** — a
second API-059 returns `409 ERR-901` and a second job run is a no-op. The next `AcademicYear` is
created if none starts the day after this one ends (label derived, `2026-27` → `2027-28`, same
span). An `Enrollment` is **appended** for each active student, `Student.class_id` is updated as
current placement, **withdrawn students are not rolled**, and **final-year students get no new
enrolment and stay active**. Students enrolled before any `Enrollment` rows existed (P1–P4 never
wrote them) get the closing year's placement snapshotted first, so the closed year stays
queryable. **No code path edits or removes an `Enrollment`** — BR-060/D14's append-only rule holds
through rollover, which is the one operation most likely to break it.

**Class delete (API-027) retrofit, recorded at 1.0.4 (U26):** a class with `Enrollment` history is
refused with **`ERR-306`** rather than failing on a raw foreign-key error; deleting a class that is
another class's successor **clears that link**, so the predecessor becomes a final year rather than
the delete failing.

## Commercial / Subscription

**API-061** `GET /api/v1/public/plans` — Auth: anonymous. Returns the 3 tiers' **structural**
fields (name, capacity, storage, support level, capabilities list) — never marketing copy from
the Pricing mockup verbatim beyond what's grounded in BR-061/062.
**API-062** `POST /api/v1/public/enquiries` — Auth: anonymous. `{fullName, email, institution?,
studentCountRange?, message}`. Success `201` → dispatches a notification (T-004), no CRM entity
modeled (per `ui-design-final.md` §1.4).
**API-063** `POST /api/v1/trials` — **Auth: leadership of that institution, or PlatformOperator.
NOT anonymous.** `{institutionId}`. Success `201` → `{trialExpiresAtUtc}` (`CFG-006` = 30 days).
No payment fields accepted at all; a payment field a client sends (e.g. `cardNumber`) is **ignored,
not stored**.

> **Auth corrected at 1.0.4 (Q-230, U26).** This read "anonymous or newly-registering" with an
> arbitrary `{institutionId}` — i.e. anyone could start (and burn) the one free trial of any
> school in the public directory. **A trial may be started once per institution and only from the
> Entry plan**; a repeat attempt, or one from a paid plan, returns `409 ERR-901`. It grants **Mid**
> for 30 days via the trial fields on `Institution` (§2.1). **Entitlements drop at expiry even
> before the nightly `trial-reversion` job runs** — the job makes stored state match. A purchase
> through API-064 ends a running trial. Errors: `ERR-102` (403 — another school's leadership, or a
> Teacher), 401 anonymous.

**API-064** `PUT /api/v1/subscriptions` — Auth: PlatformOperator only (BRL-064). `{institutionId,
subscriptionPlanId, startDate}` — `startDate` lands on `Institution.subscription_start_date`
(§2.1, added 1.0.4).
**Security note written back at 1.0.4 (U26):** the subscription endpoints **re-read the caller's
role from their account row** rather than trusting the JWT claim, per `07-business-logic/_shared.md`
§1's rule for subscription changes. A token claiming `PlatformOperator` for an account that is not
one gets `403`. A mutation trusting the token failed exactly that test.

**API-069** `PUT /api/v1/subscription-plans/{id}` — Auth: PlatformOperator only. *Written back at
1.0.4 (Q-227, U26, built in CT-503.)* Edits a plan's `learnerCapacity` (null = unlimited),
`storageAllowanceGb` and `capabilities`. An unknown capability name is rejected `ERR-901`. Changes
take effect immediately and are audited (§2.10).

> **On the id:** this endpoint was built in CT-503 with **no `API-` id at all** — giving it one
> now numbers an unnumbered endpoint rather than moving an existing id. `API-069` is used because
> `API-065`–`068` are taken by this patch's leadership-code contracts in
> `05-api-contracts/identity-access.md`.
>
> **Why the plan is editable at all:** the seeded tier limits have **no source** in the BRD, the
> Tech Design or this suite (Q-227) — they were CT-001 placeholders. **U28 remains open**: they
> are now *enforced* placeholders, so wrong numbers are wrong behaviour. Live consequence today —
> Entry and Mid do not carry `ParentAccess`, so the four correctly-built Parent gates refuse
> almost every real parent with `402`. **Not fixed by this patch; it needs the real numbers.**

## API-061 / API-062 — task placement (recorded at 1.0.4, U26)

Both were **in no `CT-`'s scope** in `15-task-breakdown.md` §4. They were built in **CT-503**;
the task breakdown now says so. **API-062's enquiry is logged at Warning and NOT delivered** — no
email channel is bound (T-004). **U30 is open and this patch does not resolve it.**

## Idempotency

`POST /trials`, `POST /enquiries`, `POST /leadership-codes` (API-066) and `POST /reports` accept
`Idempotency-Key`.
