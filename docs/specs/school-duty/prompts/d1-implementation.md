Read `docs/` spec kit + `.spec/constitution.md` first. This is the single source of truth. At the end, update the spec kit (README change-log + relevant docs + deviations log) to reflect what you did.

Implement **D1 only — School Duty rotation, manager workspace, and staff-attendance integration** in `D:\AlFalah-Manage-System`. This prompt authorizes D1 implementation using the confirmed decisions and the documented defaults relevant to D1. Update the planning status accordingly; do not ask me to reconfirm decisions already recorded. Read the entire feature kit before coding:

- `docs/specs/school-duty/README.md`
- `docs/specs/school-duty/research.md`
- `docs/specs/school-duty/clarifications.md`
- `docs/specs/school-duty/spec.md`
- `docs/specs/school-duty/ux.md`
- `docs/specs/school-duty/plan.md`
- `docs/specs/school-duty/tasks.md`
- `docs/phases/PHASE-DUTY-SCHOOL-DUTY.md`

Inspect the actual current timetable, staff attendance, authorization, and frontend code before changing it. Preserve unrelated working-tree changes. Apply the project's layered architecture and the `dotnet-layered-api-architect` skill, retaining the existing `ApiResponse<T>` envelope and server-enforced school scope.

Deliver D1 completely, including backend, additive schema, meaningful verification, and a polished working frontend:

1. Add the duty tab inside the existing `/timetable` workspace, preserving timetable editing, teacher views, cloning, checkpoints, inline swaps, printing, and existing standby/daily-cover behavior.
2. Add explicit duty settings for selecting participating teachers, excluding/restoring teachers, operating dates, closure ranges, and rotation order. Distribute duty equally across the selected eligible teachers, without teaching-load weights. Provide deterministic preview, publication, future regeneration, and visible fairness/compensation information.
3. Reuse existing staff `AttendanceRecord` facts. When A is absent, the next eligible scheduled teacher B covers A's date and A takes B's next duty date. Repeat against the current effective queue if A remains absent. Retain original/effective teachers, both dates, reasons, and history. Commit each exchange atomically and prevent duplicate handling.
4. Integrate durable reconciliation into attendance saving, including corrections and retries. Missing attendance is unknown. Respect exclusions, manual protection, concurrency, and unresolved compensation. Preserve the secretary's existing attendance authorization.
5. Give the actual active school manager duty management initially. Support manual replacement/exchange with mandatory reasons and a review of both dates. Provide teachers with a schedule-only projection; enforce backend ownership and confidentiality. General feature delegation remains deferred.

**Frontend excellence is a core acceptance requirement. The client places exceptionally high importance on appearance: the delivered UI must be striking, premium, beautifully composed, and impressive enough for a client demonstration. Budget substantial implementation time for visual design and refinement.**

Build the full D1 manager experience: an elegant today card, readable week/term schedule, intentional typography and spacing, restrained green/gold branding, refined teacher-selection settings, clear before/after exchange reviews, readable history, and purposeful loading/empty/error/exception states. Use existing Al-Falah design tokens and shared controls. Keep information useful, real, and easy to scan. Implement excellent Arabic RTL, matching English translations, keyboard access, touch-friendly actions, responsive layouts, and reduced-motion support.

Inspect the rendered UI at 390px, 768px, and 1440px. Exercise real interactions, capture the principal states, and refine wrapping, alignment, density, overflow, contrast, and visual hierarchy. A successful build alone does not establish visual acceptance. Report what was actually inspected, and leave final client visual acceptance pending my review.

Run appropriate backend/frontend checks and meaningful rotation, repeated-absence, exclusion, manual-override, idempotency, school-isolation, and atomic-concurrency tests. Validate migrations against isolated SQL first; preserve original application settings, credentials, and existing data. Clearly distinguish mocked verification from live results.

Keep D2 signatures/report execution, D3 PDF/archive delivery, general feature assignment, and extensions to the official waiting-period register outside this implementation. Preserve the existing waiting-period and daily-cover functionality and regression-check affected integrations. Do not deploy to production or reset data.

Continue until D1's authorized work is complete. Update feature docs, task checkboxes, phase tracker, project change log, and decisions/deviations. Report delivered behavior, checks, visual evidence, and genuine remaining limitations; **stop after D1**.

Write documentation in English and communicate with me in Arabic.
