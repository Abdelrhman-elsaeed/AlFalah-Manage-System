# Classroom catalogue and teaching assignments

**Current status: C36 / D-128 delivers C5 saved-revision assignment PDF after the C33-C35 foundation/consumers/workspace.** [Current statement behavior](statement.md), [verification, actual PDFs and screenshots](verification/statement/README.md), [assignment workspace](assignments.md). C6 pilot/owner/physical/printer acceptance remains pending. Production build retains existing CSS budget blockers. Earlier sections below record their original delivery scope.

**C32 / D-124 · 2026-10-15 · Discovery, design and interactive prototype delivered. Production implementation NOT started.**

The owner explicitly selected **discovery/design/prototype first**. This package proposes how to unify classroom identity across existing features and modernize teaching assignments. No existing application route, entity, permission, migration, business record, archive or provider setting was changed.

## Confirmed owner decisions

1. The manager defines the school's classroom sections; other workflows select existing sections instead of inventing names.
2. A teacher selects multiple sections and **confirms their own associations directly**. There is no manager-approval queue for this action. This choice is explicit despite the existing relationship's use in classroom access.
3. Grade numbers start within their stage, such as first intermediate and first secondary; new sections are numbered1,2,3. Existing letter-based sections require an explicit migration mapping rather than silent relabeling.
4. Teaching assignments retain single teaching, co-teaching and split quota, including whole paired periods and existing capacity rules.
5. The assignment statement follows the supplied `2.jpeg` structure. The sample uses synthetic names, no copied signatures, and the platform's existing logo. Its grade/section notation is stated explicitly to avoid RTL slash ambiguity.

## Review artifacts

- [Current-system impact map and evidence](impact-map.md).
- [Proposed model, policies, interfaces and migration](design.md).
- [Implementation phases and acceptance checklist](implementation-plan.md).
- [Visual direction and critique](visual-direction.md).
- [Verification and screenshots](verification/README.md).
- [Sample assignment PDF](verification/assignment-statement-sample.pdf), [rendered page](verification/assignment-statement-page.png).
- [Interactive prototype source](prototype/index.html).

The prototype is currently available at **http://127.0.0.1:4780/index.html**. It is a standalone in-memory design, separate from the authenticated application. Its save, bulk-create and teacher-confirmation actions affect synthetic browser state only; reload resets it. Browser **Print / Save PDF** uses the currently saved prototype assignments, independent of filters. The existing production Angular screen remains unchanged.

Start it again from the repository root when needed:

```powershell
python -m http.server 4780 --bind 127.0.0.1 --directory docs/specs/classroom-catalog/prototype
```

The requested `anthropics/skills` `frontend-design` skill was installed into the local Codex skill directory and read for this work. The prototype follows existing platform colors/Tajawal and isolates its styles; the installation is available to future turns automatically.

## Key finding

The system already has a structured `Classroom` and structured subject/teaching assignments. The incompatible older teacher relation is `InstructorClass.ClassLabel`, and visits still carry text labels. Creating another classroom system would deepen the inconsistency. Extend and reuse the existing catalogue, replace identity comparisons at their consumers, and retain historical text as snapshots.

## Decisions still requiring implementation review

These do not block this design deliverable: exact stage metadata for every supported school; manager confirmation of legacy letter/numeric mappings; approved export signatory/header assets; whether the production statement should expose an explicit per-member specialization category immediately or introduce structured specialization-subject links first. The proposed policies and default recommendation are recorded in [design](design.md). None is silently implemented here.
