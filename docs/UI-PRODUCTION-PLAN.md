# خطة إنتاجية شاملة — واجهة المستخدم والتقارير المطبوعة
# UI + Printed-Report Production Readiness Plan

> **Status:** in progress · **Owner:** frontend + reporting
> **Scope:** every route in the Angular app, every generated PDF/Excel, and the
> shared design-system layer they all sit on. Nothing is "done" until it is
> re-checked against the acceptance list at the end of its section.

This plan is written against **observed defects**, not guesses: every item in
Part 1 and Part 2 was reproduced either in the running app or by rendering the
real PDF and inspecting the printed sheet (see *Verification harness* below).

---

## 0. Verification harness (build this first — everything else depends on it)

Fixing print layout by reading code is guesswork. Two dev aids make it
observable:

| Aid | What it does | How to run |
|---|---|---|
| `AlFalah.Tests/Reports/PdfVisualDumpTests` | renders a fully-populated **visit report** to `PDF_DUMP_DIR` | `PDF_DUMP_DIR=<dir> dotnet test --filter PdfVisualDumpTests` |
| `AlFalah.Tests/Reports/DashboardVisualDumpTests` | seeds a realistic school (5 teachers, 2 moderators, 38 visits, 9 plans, complaints) and renders **all four role dashboards** | `PDF_DUMP_DIR=<dir> dotnet test --filter DashboardVisualDumpTests` |

Both are skipped when `PDF_DUMP_DIR` is unset, so CI never writes files.

> The API locks its `bin` folder while running. Build tests with
> `-p:BaseOutputPath=<scratch>/build/` to avoid `MSB3027` without stopping the
> dev server.

For **control-level CSS** (the dropdown ✕, input groups, tags…) the equivalent
aid is a static harness screenshotted with the Chrome that Karma already
installs — no dev server, no login, and every state side by side:

```bash
# harness.html links the four real stylesheets and pastes the DOM PrimeNG emits
chrome --headless=new --disable-gpu --allow-file-access-from-files \
       --force-device-scale-factor=2 --window-size=620,700 \
       --user-data-dir=<scratch>/chrome-profile \
       --screenshot=<scratch>/after.png file:///<abs>/harness.html
```

`--user-data-dir` is required (Chrome refuses to write the screenshot without a
writable profile) and the output path must be absolute.

**Rule adopted:** no print-layout or control-CSS change is accepted without a
before/after image.

---

## 1. Cross-cutting defect: three different score scales in one product

This is the headline problem and it touches backend, PDFs, Excel and the web UI.

The rubric scores each standard **0–4**. From there the system currently
publishes *three* different scales at once, sometimes inside the same card:

| Surface | Currently shows | Scale |
|---|---|---|
| Visit PDF → تحليل الزيارة → الدرجة الكلية | `78 / 100` | 0–100 |
| Visit PDF → تحليل الزيارة → المتوسط العام | `3.12 / 4` | 0–4 |
| Visit PDF → متوسطات المحاور | `91.7%` **and** `3.67 / 4` side by side | both |
| Visit PDF → نقاط القوة / مجالات التحسين | `3.67`, `1.17` (bare, no scale) | 0–4 |
| Dashboard PDF → متوسط الأداء | `2.50` | 0–4 |
| Dashboard PDF → مقارنة المدارس | caption: "المتوسط محسوب على مقياس من 4 درجات" | 0–4 |
| Web dashboard → performance chart | y-axis hard-capped at `max: 4` | 0–4 |
| Web visit detail → domain cards | mixed | both |

**Decision (D-UI-1): one published scale — 0 to 100.**

- The rubric keeps 0–4 **internally**: per-standard scores, the stored
  `VisitAnalysis.OverallScore`, and every performance-level threshold
  (`متميز ≥ 3.5` …) are unchanged. Historical snapshots are never recomputed —
  that invariant stands (docs/09, D-26).
- Everything **published to a human** is presented on 0–100 via one shared
  conversion (`× 25`), rounded to one decimal.
- The only place a 0–4 number may still appear is the **individual standard
  score chip**, because there it is not a score out of anything — it is the
  rubric *level* (`4 متميز`, `3 متحقق بدرجة جيدة`), and it is always shown with
  its Arabic level word.

**Consequences to implement**

1. Backend: a single conversion helper, used by `PdfReportService`,
   `DashboardService` (PDF + Excel) and every DTO that surfaces an average.
   `VisitService.ScorePercentage` already exists — promote it to shared and
   stop hand-formatting `X / 4` in the composers.
2. `ReportDomainBlockDto.AverageScore` stays (thresholds/colours need it);
   only the *rendered* string changes.
3. Column header fix: the متوسطات المحاور table labels its middle column
   **المتوسط العام** ("overall average") while it actually holds the *domain*
   average → rename to **متوسط المحور**.
4. Frontend: `cartesianOptions.scales.y.max` 4 → 100; domain/percentage
   formatting via one pipe so no component re-derives it.
5. Every caption that says "من 4 درجات" is rewritten to "من 100".

**Acceptance:** grep the repo for `/ 4`, `من 4`, `max: 4` and `* 25` — the only
survivors are the internal threshold constants and the standard-level chip.

---

## 2. Named defects (reproduced)

### 2.1 Dropdown clear "✕" is huge, mispositioned, and shows with no value

Two independent bugs stacked:

- **Phantom icon.** Forms initialise selects with `''`
  (`userId: ['', Validators.required]`). `ClearableSelectComponent.writeValue`
  does `(val === undefined ? null : val) ?? null`, and `''` is *not* nullish, so
  the control holds `''`. PrimeNG's `isVisibleClearIcon` is
  `modelValue() != null && hasSelectedOption()` → `'' != null` is **true**, so
  the ✕ paints while the placeholder ("اختر") is still showing.
- **Oversized / misplaced glyph.** PrimeNG 17 renders the clear icon as
  `<svg class="p-dropdown-clear-icon">`. `styles/primeng.css` sets
  `width: 1.8rem !important; height: 1.8rem !important` — on the SVG *itself*,
  so the ✕ glyph draws at ~29 px. Combined with PrimeNG's own
  `position:absolute; top:50%; margin-top:-.5rem` it also hangs below centre,
  and the absolute `inset-inline-end` fights the RTL label padding, landing the
  ✕ on top of the placeholder text.

**Fix**
- `writeValue` treats `''` as "no selection" (and so does the internal control),
  so a required-but-empty select shows *no* clear affordance.
- Stop absolutely positioning the icon. `.p-dropdown` is already
  `inline-flex; align-items:center` and the DOM order is
  `label → clear icon → trigger`, so making the icon a **static flex item**
  places it correctly between the value and the chevron in *both* directions,
  with zero overlap risk. Size it ~`0.8rem` with padding for the hit area.
- Apply the same treatment to multiselect / calendar / autocomplete clear icons
  so one control family can't drift from another.

### 2.2 Wrong message when exporting a visit PDF

The PDF request is `responseType: 'blob'`. When it fails, Angular hands back
`error.error` as a **`Blob`**, not parsed JSON. Consequences:

- `ErrorInterceptor.extractMessage` sees `typeof body === 'object'`, finds no
  `.message`, and falls through to the generic `ERRORS.SERVER_ERROR` toast.
- The component's own handler runs too, so the user gets **two** toasts, and the
  server's real Arabic reason (e.g. *"لا يمكن للمعلم تحميل تقرير الزيارة قبل
  اعتماد مدير المدرسة."*) is thrown away.

**Fix**
- Read blob error bodies as text and parse them before deciding the message —
  one shared helper, used by the interceptor and by both components that
  download blobs (`visit-detail`, `visits-list`), plus attendance / evidence
  exports which have the same shape.
- Suppress the interceptor's generic toast when the caller handles the error
  itself (an explicit `HttpContextToken`, same pattern as the existing
  `SUPPRESS_FORBIDDEN_REDIRECT`), so a single failure produces a single,
  accurate message.

### 2.3 Dashboard PDF export layout

Observed on the rendered sheets, all four roles:

| # | Defect | Evidence |
|---|---|---|
| D1 | **KPI reading order reversed.** `foreach (metric) row.RelativeItem()` lays items physically L→R, so on an RTL sheet the *first* metric lands **left-most** — a reader starting from the right reads the KPI strip backwards (4,3,2,1 then 8,7,6,5). | Main Manager p.1 |
| D2 | **Partial last row leaves the gap on the RTL start edge.** Padding cells are appended *after* the real ones, so the Moderator's 7-metric grid opens with an empty slot at the top-right. | Moderator p.1 |
| D3 | **Bar length contradicts its own label.** The bar is filled `count / max` while the number beside it prints `count / total`: مسودة shows a bar at ~1/3 next to the text "16.7%". | School Manager p.1 |
| D4 | **Bars grow from the left** on a right-to-left sheet. | all roles |
| D5 | **Near-blank page.** `ShowEntire` on خطط التحسين pushes a 6-tile block onto its own sheet, leaving ~80 % of page 2 white. | Main Manager p.2 |
| D6 | **Label/value rows are centred inside full-width cells**, so آخر تقييم معتمد prints its values floating in the middle of the sheet with a wide void beside the label. | Instructor p.1 |
| D7 | **Over-wide relative columns** on landscape: مستوى الأداء / المدينة stretch, leaving large empty bands inside ruled cells. | Instructor + Main Manager |
| D8 | **`المستوى` column is empty (`—`) for every row** in أعلى المعلمين تقييماً — the DTO's level is never populated. | Moderator p.1 |
| D9 | Averages on the 0–4 scale + the "مقياس من 4 درجات" caption → see §1. | all roles |

### 2.4 Visit PDF layout

| # | Defect | Evidence |
|---|---|---|
| V1 | Mixed scales, and the متوسطات المحاور middle column is mislabelled. | p.1 (§1) |
| V2 | **Stranded section heading:** متابعة خطط التحسين prints its title band at the foot of p.3 with the table on p.4. | p.3/p.4 |
| V3 | **Wasted sheets:** ~40 % of p.1 and ~65 % of p.4 are white; the report runs to 4 pages for ~2.5 pages of content. | p.1, p.4 |
| V4 | نقاط القوة / مجالات التحسين print a bare number with no unit. | p.3 |

---

## 3. Page-by-page pass

Every route below gets the same treatment. **The checklist is identical for all
of them** — that is deliberate: consistency is the deliverable, not novelty.

### 3.0 The per-page checklist

1. **Header** — title + subtitle present, one primary action, secondary actions
   grouped and never more than 3 visible (overflow into a menu).
2. **RTL** — no physical `left`/`right` in component CSS; logical properties
   only. Icons that imply direction are mirrored.
3. **Loading** — skeleton or spinner, never a bare empty page; the primary
   action is disabled while in flight.
4. **Empty** — an explicit Arabic empty state with the next action, never a
   blank table body.
5. **Error** — one accurate toast (§2.2), inline field errors on forms.
6. **Density** — one control height (`--control-height`), one card radius, one
   gap scale. No per-page magic numbers.
7. **Tables** — sticky header, `table-actions-col` for the action column,
   tooltips on icon-only buttons, `aria-label` on every one of them.
8. **Responsive** — usable at 1280, 1024 and 720 px; tables scroll inside their
   card, the page body never scrolls horizontally.
9. **Focus + keyboard** — visible focus ring, dialogs trap focus, Esc closes.
10. **Scale** — any published score is 0–100 (§1).

### 3.1 Inventory (28 screens)

| # | Route | Component | Known issues to confirm |
|---|---|---|---|
| 1 | `/auth/school-login` | `school-login` | brand/state polish, error placement |
| 2 | `/auth/main-manager-login` | `main-manager-login` | must match #1 exactly |
| 3 | `/parent-survey/:token` | `public-parent-survey` | public page — no shell, own header |
| 4 | `/main-manager/dashboard` | `dashboard-live` | duplicate legend, chart y-max 4 |
| 5 | `/school-manager/dashboard` | `dashboard-live` | **donut legend overlaps the status tags** (reported) |
| 6 | `/moderator/dashboard` | `dashboard-live` | as #4 |
| 7 | `/instructor/dashboard` | `dashboard-live` | as #4 |
| 8 | `/*/evidence-matrix` | `evidence-matrix-page` | wide matrix — horizontal scroll containment |
| 9 | `/school-manager/evidence-settings` | `school-google-drive-settings` | credential-type toggle; secrets are write-only |
| 10 | `/instructor/reports` | `instructor-reports` | 44-line template, thin |
| 11 | `/instructor/evidence-files` | `teacher-evidence-files-page` | 32-line template, thin |
| 12 | `/visit-reports/:id/preview` | `report-preview` | print styling |
| 13 | `/attendance` | `attendance` | export error path (§2.2) |
| 14 | `/parent-surveys` | `parent-survey-admin` | 205-line template |
| 15 | `/schools` | `schools-list` | list checklist |
| 16 | `/schools/new`, `/:id/edit` | `school-form` | form checklist |
| 17 | `/teachers` | `teachers-list` | list checklist |
| 18 | `/teachers/:userId` | `teacher-profile` | score scale |
| 19 | `/users` | `users-list` | list checklist |
| 20 | `/users/new`, `/:id/edit` | `user-form` | form checklist |
| 21 | `/user-school-roles` | `user-school-roles-list` | **phantom ✕ in the new-assignment dialog** (reported) |
| 22 | `/rubric` | `rubric-viewer` | scale legend |
| 23 | `/rubric/edit` | `rubric-editor` | destructive-action confirmation |
| 24 | `/visits` | `visits-list` | "تم تقييم 0 من 0" column reads as a bug |
| 25 | `/visits/new`, `/:id/edit` | `visit-form` | 261-line template, longest form |
| 26 | `/visits/:id` | `visit-detail` | **7 buttons crammed into one action bar** (reported) |
| 27 | `/visits/:visitId/improvement-plans`, `/improvement-plans/:id` | `plan-list`, `plan-detail` | progress % vs score scale |
| 28 | `/complaints`, `/unauthorized`, `/account/settings` | — | checklist |

### 3.2 Shared layer first

Page-level work is cheap only after the shared layer is right. In order:

1. `styles/primeng.css` — control-family audit (dropdown, multiselect,
   calendar, inputgroup, dialog, table, paginator, tag, button). One source of
   truth per property; delete the `!important` chains that exist only to fight
   an earlier rule in the same file.
2. `shared/components/clearable-select` — §2.1.
3. `shared/components/list-toolbar` — field widths, wrap behaviour, the
   search/filter/clear action group.
4. `shared/components/dashboard-live` — legend duplication, chart scale,
   metric-card grid at 3 breakpoints.
5. `shared/layout/shell` — sidebar collapse, active state, topbar density.
6. A shared **`page-actions`** pattern for #26-style toolbars: primary action,
   then a max of two secondaries, the rest under a single "المزيد" menu.

---

## 4. Execution order

| Step | Content | Gate |
|---|---|---|
| 0 | Verification harness (§0) | dumps render |
| 1 | Scale unification (§1) | grep clean, tests green |
| 2 | Dropdown ✕ (§2.1) | no ✕ on an empty required select at 3 breakpoints |
| 3 | Blob error messages (§2.2) | one accurate toast per failure |
| 4 | Dashboard PDF (§2.3) | fresh dumps, all four roles |
| 5 | Visit PDF (§2.4) | fresh dump, no stranded heading |
| 6 | Shared layer (§3.2) | frontend build clean |
| 7 | 28 pages (§3.1) against the checklist (§3.0) | per-page sign-off |
| 8 | Full regression | `dotnet test` + `ng build` + re-dump |

## 5. Definition of done

- `dotnet test` green; `ng build --configuration production` clean and inside
  the existing budgets.
- Every published score is 0–100; the only 0–4 figure left is the standard
  level chip, always beside its Arabic word.
- All four dashboard PDFs and the visit PDF re-dumped and inspected: no
  reversed RTL order, no stranded heading, no page under 40 % full, no bar whose
  length disagrees with its label.
- Every one of the 28 screens passes §3.0 at 1280 / 1024 / 720 px.
- One failure → one accurate Arabic message.
