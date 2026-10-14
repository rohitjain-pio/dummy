# AIDLC Paths — Canonical Stage → Directory Map

> **This file is the single source of truth for where each stage reads and writes.**
> Every agent and the coordinator obey this map. The `stages.<s>.dir` values in
> `.aidlc/_state/aidlc-state.json` are a *projection* of this file — if they ever
> disagree, **this file wins**. Never hardcode a stage directory anywhere else.
>
> This is an org-standard file (shipped pre-filled by `setup-constitution`). Only change it
> if your project deliberately relocates the `.aidlc/` tree — and if you do, change it *here*
> and let everything else follow.

## Root

All run artifacts live under **`.aidlc/`** at the repo root, committed to git so the artifact +
state travel between roles/machines.

### Why `.aidlc/` and not `.pipeline/`

"Pipeline" means **CI/CD** to every developer — and this framework integrates with Azure DevOps,
where *Pipelines* is a literal product name. A `.pipeline/` folder next to an
`azure-pipelines.yml` gets read as CI config, which this is not. Worse, "pipeline" connotes an
automatic, unattended flow, and **the defining property of this system is that it stops at human
gates.** The name argued against the design.

### The sibling plugins have not moved yet — the one fallback

`sdlc-brownfield-dev`, `sdlc-brownfield-ba` and `e2e-test-gen` still write `.pipeline/`, and
**`_config/` is shared with them**. So:

> **Prefer `.aidlc/`. If it does not exist and `.pipeline/` does, read `.pipeline/` and say so
> once.** Never write to both.

This is a **migration affordance with an expiry date**, not a permanent dual path — when the
other three plugins move, delete the fallback. *Two config roots that drift apart are strictly
worse than one root in the wrong place.* The resolver lives in `scripts/_common.sh` (all four
hooks) and `scripts/verify-gate.sh`, and the gate report now names the tree it graded.

## One folder per command, one sub-folder per agent

**The stage folder is named after the command that owns it; each agent inside that command owns
its own sub-folder.** That is the whole shape of the tree, and it exists for three reasons:

1. **You can see which agent produced which artifact** without reading the file.
2. **A re-run of one agent archives one sub-folder**, not the whole stage.
3. **The command owns the question round-trip for its own agents** — which is what makes running
   a command directly behave identically to running it through the orchestrator.

## Reserved / infrastructure directories

| Key       | Directory               | Owner        | Purpose                                                        |
|-----------|-------------------------|--------------|----------------------------------------------------------------|
| config    | `.aidlc/_config/`    | shared       | `company.md`, `project.md`, `paths.md` (this file), `constitution.md`. |
| state     | `.aidlc/_state/`     | coordinator  | `aidlc-state.json`, `work-items.jsonl` (tracker ledger).    |
| archive   | `.aidlc/_archive/`   | coordinator  | archived stage or agent dirs when one is restarted.            |

> **`.aidlc/_build/` is gone.** The build stage now owns `.aidlc/03-development/`, which
> is numbered like every other stage. Anything still writing to `_build/` is reading a stale map.

## Stage → directory map

| Stage         | Key           | Directory                        | Command              | Owner       |
|---------------|---------------|----------------------------------|----------------------|-------------|
| 0 Input       | `input`       | `.aidlc/00-input/`            | `setup-constitution` | BA          |
| 1 BRD         | `brd`         | `.aidlc/01-brd-analysis/`     | `brd-analyzer`       | BA          |
| 2 Solution    | `solution`    | `.aidlc/02-solution-designer/`| `solution-designer`  | Architect / Lead |
| 3 Development | `development` | `.aidlc/03-development/` + repo root | `start-development` | Dev  |
| 4 Code Review | `code_review` | `.aidlc/04-code-review/`      | `code-reviewer`      | Reviewer    |

### Agent → sub-directory map

| Stage | Agent | Sub-directory | Optional? |
|---|---|---|---|
| 1 BRD | `brd-scope-planner` | `.aidlc/01-brd-analysis/brd-scope/` | **yes** — only when the BRD is sliced into phases |
| 1 BRD | `brd-analyst` | `.aidlc/01-brd-analysis/brd-analyst/` | no |
| 2 Solution | `tech-design` | `.aidlc/02-solution-designer/technical-design/` | no |
| 2 Solution | `ui-enrichment` | `.aidlc/02-solution-designer/ui-designer/` | **yes** — skipped when the app has no UI |
| 2 Solution | `ai-spec` | `.aidlc/02-solution-designer/ai-specification/` | no |
| 3 Development | `code-generation` | `.aidlc/03-development/` + product code at repo root | no |
| 4 Code Review | `code-review` | `.aidlc/04-code-review/` | no |

## The full tree

```
.aidlc/
  _config/                       company.md · paths.md · project.md · constitution.md
  _state/                        aidlc-state.json · work-items.jsonl
  _archive/                      <stage-or-agent>-<timestamp>/

  00-input/                      created by setup-constitution (and by a fresh orchestrator run)
    <initial-brd>.md|docx|txt|pdf    the user drops this in
    mockups/                         created EMPTY — UI mockups, screenshots, brand guides
      README.md                      placeholder so git keeps the empty directory

  01-brd-analysis/
    brd-scope/                     only when scope planning ran
      brd-scope.md                   FINAL hand-off — becomes brd-analyst's source BRD
      scope-decisions.md             the live session log: what was cut, into which phase, why
    brd-analyst/
      brd-questions.xlsx             the workbook the human fills
      _questions.md                  machine record — the mirror the gate hooks parse
      final-brd.md                   FINAL
      final-brd.docx                 FINAL, client-facing
      requirements.yaml              the confirmed, ID'd requirement spine

  02-solution-designer/
    technical-design/
      tech-design-draft.md
      _decisions.md                        every choice made FOR the architect, with rationale
      architect-decision-questions.xlsx    Decide + Spike + material unknowns
      _questions.md
      final-tech-design.md           FINAL
      final-tech-design.docx         FINAL
    ui-designer/
      ui-design-questions.xlsx       screens · positions · theme · components · states
      _questions.md
      design-tokens.md               the named token system every screen spec must cite
      ui-design-final.md             FINAL
    ai-specification/
      00-spec-index.md               written LAST
      01-source-catalogue.md ... 15-task-breakdown.md   (+ sharded concern folders)
      _questions.md                  (+ _questions.xlsx on request)
      _upstream-feedback.md

  03-development/
    build-log.md                   append-only, per phase P0..Pn
    adr/                           one file per resolved architectural decision
                                   (product code itself lives at the repo root)

  04-code-review/
    Review_P{n}.md
    Spec_Audit.md                  the independent Mode D audit of the spec suite
    _upstream-feedback.md
```

> **Acceptance is not part of this plugin.** `requirements.yaml` is still produced and still
> traced, so whatever performs acceptance inherits an intact checklist — but no stage here
> routes to it. E2E test *generation* is also external (the `e2e-test-gen` plugin).

## Key artifacts per stage (for quick reference)

- **input** — the rough BRD in `00-input/`, and `00-input/mockups/` for any UI source (Figma
  export, screenshots, brand guide, PDF). **The mockups folder is created empty on purpose** —
  its absence would otherwise be indistinguishable from "nobody supplied mockups".
- **brd** — hand-off: `01-brd-analysis/brd-analyst/final-brd.md` (+ `.docx`); **machine-readable
  spine: `01-brd-analysis/brd-analyst/requirements.yaml`** (the confirmed requirement list every
  later stage reads); questions: `brd-analyst/brd-questions.xlsx` + `_questions.md`. Scope, when
  it ran: `01-brd-analysis/brd-scope/brd-scope.md`.
- **solution** — `technical-design/final-tech-design.md` (+ `.docx`) ·
  `ui-designer/ui-design-final.md` + `ui-designer/design-tokens.md` ·
  `ai-specification/00-spec-index.md` (+ the full suite) · upstream feedback:
  `ai-specification/_upstream-feedback.md`.
- **development** — code at repo root + `03-development/build-log.md` per phase (P0–Pn, per
  `15-task-breakdown.md`) + `03-development/adr/`.
- **code_review** — `04-code-review/Review_P{n}.md`; spec-vs-requirements audit:
  `04-code-review/Spec_Audit.md`; upstream feedback: `04-code-review/_upstream-feedback.md`.

## Spec-suite layout — concern folders, feature files

A spec concern is **either one file or a folder of feature files**, decided per concern by size:
over ~800 lines or more than ~5 features, it shards. The folder keeps the concern's number, so
reading order and ID-prefix routing are unchanged.

```
.aidlc/02-solution-designer/ai-specification/
  00-spec-index.md          <- the feature slug list + the shard map (the authority for both)
  01-source-catalogue.md
  02-requirements-spec/     login.md  orders.md  reporting.md
  05-api-contracts/         login.md  orders.md
  07-business-logic/        login.md  orders.md  _shared.md
  10-process-flows/         login.md  orders.md
  11-ui-spec/               login.md  orders.md
  14-test-spec/             login.md  orders.md
  03-system-design.md  04-data-model.md  08-error-catalogue.md
  09-security-spec.md  12-infrastructure-spec.md  15-task-breakdown.md
```

**Six concerns never shard** — `08` (one canonical error registry), `04` (entities are shared),
`09` (one permission matrix), `15` (the cross-feature dependency DAG), `03` and `12` (global by
definition). Splitting those would duplicate them rather than divide them.

**Cross-feature content lives in `_shared.md`** inside its concern's folder — never copied into
two feature files.

Each `CT-` task's `Specs:` field names the **exact file**, so the build loads one feature's spec
instead of every feature's. `scripts/verify-gate.sh` check C9 enforces the layout: it warns on an
oversized flat file, fails on a sharded folder missing from the shard map, and warns on an
inconsistent feature slug.

## Questions files — one per agent, xlsx to fill, Markdown as the record

| Stage | Agent | Workbook the human fills | Machine record |
|---|---|---|---|
| brd | `brd-scope-planner` | *(none — scoping is a live chat negotiation)* | `brd-scope/scope-decisions.md` |
| brd | `brd-analyst` | `brd-analyst/brd-questions.xlsx` | `brd-analyst/_questions.md` |
| solution | `tech-design` | `technical-design/architect-decision-questions.xlsx` | `technical-design/_questions.md` |
| solution | `ui-enrichment` | `ui-designer/ui-design-questions.xlsx` | `ui-designer/_questions.md` |
| solution | `ai-spec` | `ai-specification/_questions.xlsx` *(on request)* | `ai-specification/_questions.md` |

**The workbook is the artifact a human opens; the Markdown is the record the gate hooks parse,
and the direction is always `xlsx → md`.** Never the reverse, and never a silent fallback that
changes which file is authoritative depending on what tooling a machine has — that was a real
v1 defect. Format and rules: `references/clarify-protocol.md` §5 and `shared-workbook-io`.

**`scripts/answers-filled.sh` globs `.aidlc/0*/*/_questions.md`** — one level deeper than the
flat layout, because every questions record now lives in an agent sub-folder.
