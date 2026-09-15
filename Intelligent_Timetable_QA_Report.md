# Intelligent Timetable — End-to-End Browser QA Report

**Test date:** 14 September 2026, 06:22–06:28 Africa/Cairo  
**Application:** Angular 17 SPA (`http://localhost:4200`) + .NET 8 API (`http://localhost:5264`)  
**Browser:** Microsoft Edge (Chromium), automated with Playwright 1.55  
**Viewport used for the functional and visual audit:** 1440 × 1000  
**Identity:** `admin.test` / School Manager / `Al-Falah E2E Test School`  
**Method:** Browser UI only. No unit tests, direct API setup calls, database inspection, or application-code changes were used.

## Executive verdict

**Release sign-off: CONDITIONAL FAIL / ACTION REQUIRED.**

Navigation and the requested Phase 1–7 user actions worked through the live UI, including persistence and client-side safety guards. The clean run produced no page exceptions and no HTTP 4xx/5xx responses. Sign-off is withheld for three reasons:

1. A timetable marked **published and live** currently reports **29 hard errors**.
2. Phase 8 did not demonstrate green and yellow candidates. Across the available school data, substitution searches returned no candidates and swap searches returned only disabled red candidates.
3. The browser repeatedly emits i18n and font-loading errors, and the Settings header has a measurable desktop overlap.

## Acceptance scorecard

| Area | Result | Browser evidence |
|---|---|---|
| Login and navigation | **PASS** | Login reached the School Manager shell. The `الجدول الذكي` category existed, expanded, and exposed 11 links. Every link rendered non-empty content. `تخصيص المواد` intentionally redirected to `/intelligent-timetable/subjects`. |
| Phase 1–2: Settings and timings | **PASS** | Created setup `QA Browser Setup 56154655` (HTTP 201), created timing `QA Browser Timing 56154655` (HTTP 201), added three periods, and attached the timing to the setup (HTTP 200). |
| Period overlap guard | **PASS** | Setting Period 2 to `07:15` while Period 1 ended at `07:45` displayed `الحصة 2 تبدأ قبل نهاية الحصة 1.` and disabled `حفظ جميع التغييرات`. Restoring valid times enabled save. |
| Phase 3: Breaks | **PASS** | An overlapping break displayed one 46-character message: `يوجد تداخل زمني مع الحصص. يرجى مراجعة الأوقات.` Save was disabled. Moving the break to `07:40–08:00` cleared the error and saved successfully (HTTP 200). No wall of text appeared. |
| Phase 4: Teacher availability | **PASS** | For `E2E Teacher`, the first available checkbox changed from checked to unchecked. Save returned HTTP 200; after a full page reload it remained unchecked. Maximum load persisted as 24. |
| Phase 5: Subject-to-class setup | **PASS** | Created `QA Browser Subject 56154655` (HTTP 200), selected both visible classes, and created two class requirements with `تم إنشاء 2، تحديث 0، تخطي 0.` (HTTP 200). |
| Phase 6: Teacher assignment/workload | **PASS** | Assignment dialog displayed `0 / 24` before selection and `1 / 24` including the draft after selecting `E2E Teacher`. The cell confirmed and the batch saved (HTTP 200) with `تم حفظ إسنادات المواد وتحديث أنصبة المعلمين.` |
| Phase 7: Review cards and publish gate | **PASS WITH CAVEAT** | Cards loaded for `E2E Published Timetable`: 29 hard errors, 0 warnings. The publication control was disabled and an attempted click was blocked. However, its label was already `الجدول منشور`, so the hard-error condition could not be isolated from the already-published condition. |
| Phase 8: Absent teacher and candidate colors | **PARTIAL / NOT ACCEPTED** | The legend correctly displayed green/yellow/red. The actual candidate list did not: substitute mode returned no candidates; swap mode returned only disabled red candidates. Green and yellow candidate cards were not observed. |

## Detailed execution evidence

### Navigation

The following routes were opened in Chromium and produced non-blank content:

- `/timetable`
- `/intelligent-timetable/timings`
- `/intelligent-timetable/breaks`
- `/intelligent-timetable/teachers`
- `/intelligent-timetable/subjects`
- `/intelligent-timetable/assignments`
- `/intelligent-timetable/subject-rules` → redirected to `/intelligent-timetable/subjects`
- `/intelligent-timetable/review`
- `/intelligent-timetable/substitutions`
- `/intelligent-timetable/print`
- `/intelligent-timetable/settings`

No route produced a blank shell, page crash, or 404.

### Created schedule and timing

The browser created a new setup, opened a new timing template, and increased the period count from one to three. The final valid timing was:

| Period | Start | End |
|---|---:|---:|
| 1 | 07:00 | 07:40 |
| 2 | 08:00 | 08:40 |
| 3 | 09:00 | 09:40 |

The overlap scenario was performed before saving. No invalid POST/PUT was sent while the UI was in the overlapping state.

### Availability persistence

The teacher grid visibly changed to `تغييرات غير محفوظة`, then to `جميع التغييرات محفوظة`. A browser reload and fresh selection of `إعداد المدرسة التجريبية` returned the same unchecked state. This verifies server-backed persistence rather than component-local state.

### Workload display

The assignment dialog showed the requested Assigned/Max information and recalculated immediately with the unsaved draft:

```text
Before selection: 0 / 24 — يشمل المسودة — remaining 24
After selection:  1 / 24 — يشمل المسودة — remaining 23
```

The specialization warning remained visible and did not prevent the permitted assignment.

### Review and publication

Only two reviewable timetables were available through the UI:

| Timetable | State | Hard errors | Warnings | Publication button |
|---|---|---:|---:|---|
| `E2E Published Timetable` revision 9 | Published | 29 | 0 | `الجدول منشور`, disabled |
| `Phase 7 Review QA 1788967877565` revision 4 | Published | 0 | 1 | `الجدول منشور`, disabled |

The requested hard-error safeguard is visibly active, but the environment had no unpublished draft with a hard error. Therefore, the test cannot prove that the hard error alone caused the disabled state.

### Substitution matrix

The browser selected the absent teacher and checked five working dates (`2026-09-13` through `2026-09-17`), two lessons per date, and both operation modes:

- **Substitute for this day:** 10 candidate evaluations; all returned an empty candidate list.
- **Swap timetable lessons:** 10 candidate evaluations; Sunday returned no candidates and the remaining eight returned one red candidate each.
- Observed candidate-card colors: **Red only**.
- Every red action button was disabled.
- The red candidate explained multiple hard constraints, including unavailable/stale teacher availability, invalid/missing class-subject-room linkage, missing scheduled quota, and missing teacher assignment.

The legend itself correctly rendered:

- Green: `متاح تماماً`
- Yellow: `متاح مع تجاوز وسبب مسجل`
- Red: `غير متاح`

## Findings

### IT-QA-001 — Published timetable remains live with 29 hard errors

**Severity: High / release blocker**  
**Status: Reproduced through UI**

`E2E Published Timetable` revision 9 simultaneously displayed:

- `منشور · مباشر` / published and live;
- 29 hard errors that “prevent publishing and cannot be overridden”;
- a disabled publication control;
- Phase 8 daily operations against that live timetable.

This is an unsafe state even though the publish button is disabled: the invalid version is already live. Confirm whether edits to timing, availability, subjects, or assignments must invalidate/unpublish an existing timetable, or whether published versions should be immutable snapshots. Release should not be approved until this behavior is explicitly resolved or accepted.

### IT-QA-002 — Phase 8 green/yellow candidate acceptance is not demonstrated

**Severity: High acceptance gap; product defect not proven**  
**Status: Data-blocked / partially reproduced**

The school contains only one active teacher in the tested setup. The UI correctly displayed an empty state for substitute candidates and red disabled swap candidates, but it could not produce eligible green or overrideable yellow candidates. The acceptance criterion “candidate list shows Green/Yellow/Red” therefore remains unverified.

Before sign-off, seed or create at least:

- one fully eligible substitute to produce Green;
- one overrideable soft-conflict substitute to produce Yellow;
- one hard-conflict substitute to retain Red.

Then repeat the absent-teacher flow and verify the card styling, reason requirement, and enabled/disabled actions for each color.

### IT-QA-003 — Settings title overlaps the context form at desktop width

**Severity: Medium UI defect**  
**Status: Reproduced at 1440 × 1000**

The `إعدادات الجدول` heading and its school subtitle render underneath the academic-context form. Browser geometry measured:

```text
Title box: x=928.23, y=129.25, w=76.02, h=58.00
Form box:  x=209.09, y=136.31, w=780.66, h=58.36
Intersection area: approximately 3,133 px²
```

The overlap is clearly visible before any scrolling and reduces heading readability.

### IT-QA-004 — Duplicate translation keys reported as console errors

**Severity: Medium technical-quality defect**  
**Status: Reproduced 21 times per language during the clean run**

```text
[i18n] DUPLICATE top-level keys detected in ar.json: ROLES. JSON.parse keeps only the last occurrence — fix the merge.
[i18n] DUPLICATE top-level keys detected in en.json: ROLES. JSON.parse keeps only the last occurrence — fix the merge.
```

The visible Arabic screens remained usable, but duplicate keys can silently discard translations and make localization behavior order-dependent.

### IT-QA-005 — Inter font cannot be decoded

**Severity: Low**  
**Status: Reproduced repeatedly**

```text
Failed to decode downloaded font: http://localhost:4200/media/Inter-roman.var.woff2?v=3.19
OTS parsing error: Failed to convert WOFF 2.0 font to SFNT
```

The warning appeared 24 times per message during the clean navigation run. No immediately broken Arabic glyphs were observed, but the browser falls back instead of using the requested font.

## Browser/network audit

The final clean pass recorded:

- **0** uncaught page exceptions;
- **0** HTTP 4xx responses;
- **0** HTTP 5xx responses;
- successful state-changing responses: profile creation 201, timing creation 201, timing selection 200, break save 200, teacher save 200, subject save 200, class allocation 200, and teacher assignment save 200.

The intentional overlap cases were rejected locally by disabled Save controls and did not generate invalid network requests.

## Test-data footprint

The following persistent records/state were created or changed through the UI:

- Complete run: `QA Browser Setup 56154655`, `QA Browser Timing 56154655`, and `QA Browser Subject 56154655`.
- `QA Browser Subject 56154655` was linked to two classes; `E2E Teacher` was assigned to one one-period requirement.
- `E2E Teacher` maximum load is 24 and the first Sunday slot finishes the run as unavailable.
- Harness-stabilization remnants from the earlier interrupted browser pass: `QA Browser Setup 56029783` and unallocated catalog subject `QA Browser Subject 56029783`.

No cleanup was performed because this engagement was explicitly read-and-report with no corrective changes.

## Final recommendation

Approve Phases 1–6 functionally. Treat the Phase 7 publish guard as working but investigate the already-live invalid timetable. Do not grant full module sign-off until IT-QA-001 is resolved or formally accepted, and Phase 8 is rerun with test data capable of producing actual Green, Yellow, and Red candidate cards. Console and Settings-layout defects should be included in the next stabilization pass.
