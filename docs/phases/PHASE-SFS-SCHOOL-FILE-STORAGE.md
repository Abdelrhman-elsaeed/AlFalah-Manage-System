# Phase SFS — نظام ملفات المدرسة والشواهد

**Status:** S1 IMPLEMENTED — isolated SQL verification; flags OFF; live Drive gate retained; S2–S6 not started · **Date:** 2026-10-03

The shared decisions and stage map are in [the full Spec Kit plan](../specs/school-file-storage/plan.md). Each stage has a standalone implementation and acceptance specification:

| Stage | Detailed file | Status |
|---|---|---|
| S0 | [Baseline and prototype parity](../specs/school-file-storage/phases/00-baseline-and-parity.md) | Audit complete; parity review pending; live Drive unavailable |
| S1 | [Data foundation and permissions](../specs/school-file-storage/phases/01-foundation-and-permissions.md) | Implemented; isolated SQL/backfill verified; activation OFF |
| S2 | [Library and uploads](../specs/school-file-storage/phases/02-library-and-uploads.md) | Specified |
| S3 | [Evidence and review](../specs/school-file-storage/phases/03-evidence-and-review.md) | Specified |
| S4 | [Self-evaluation and reports](../specs/school-file-storage/phases/04-self-evaluation-and-reports.md) | Specified |
| S5 | [Approved-visit PDF archive](../specs/school-file-storage/phases/05-approved-visit-pdf-archive.md) | Specified |
| S6 | [Import and rollout](../specs/school-file-storage/phases/06-import-and-rollout.md) | Specified |

[S0 report and evidence](../specs/school-file-storage/baseline/README.md): 14 source resources, 108 source coverage rows, 36 tracker items and 145 matrix rows; Development SQL aggregates collected read-only. The Drive observer built with zero errors/warnings but could not decrypt the stored Development credential with the current keys, so no Google HTTP requests were sent and live counts remain unknown. The owner's folder, size/type and retention choices are recorded. No runtime code, migrations, SQL data, Google Drive files, or historical prototype files were changed. S0 acceptance awaits owner parity review and usable Drive observation; S1 was not started. At the end of each implementation stage, record its test evidence and status here, update the plan, and stop at the next gate as required by `.spec/constitution.md`.

S1 was subsequently authorized by the owner's current instruction with the S0 Drive limitation retained. [S1 evidence and rollback](../specs/school-file-storage/verification/README.md): 11 additive tables, 8 DB permissions, live school/ownership checks, manager-only temporal delegation, atomic audit and concurrency, append-only history, and offline dry-run/repeatable backfill. The migration and two applies ran on an isolated COPY_ONLY Development clone, preserving the one legacy Approved submission and one teacher grant, with one new file/version/link/decision and zero duplicates on rerun. Original Development and Google credentials were not changed. Existing teacher/matrix readers remain authoritative; both storage flags default OFF. Technical verification does not resolve live Drive or parity acceptance. Work stops after S1; S2 has not started.
