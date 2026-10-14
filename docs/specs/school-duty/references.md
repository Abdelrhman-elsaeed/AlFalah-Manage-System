# Source Forms — Mandatory D2/D3 Reference

**Reviewed again:** 2026-10-13, after the owner explicitly required the exact source report division. All five original images were opened and visually inspected; SHA-256 fingerprints match the original research. Original files remain unchanged and are not copied into the repository.

**Original directory:** `C:\Users\abdelrhman\Desktop\المناوبة\New folder`.

The links below are local source references. D2/D3 implementers must reopen the actual forms, correct viewing orientation when needed, and compare their output visually. If the sources are unavailable on another machine, obtain the originals rather than inventing a replacement layout. Sample names, signatures, school, year and logos are historical example content, not runtime defaults.

| Source | Purpose / phase | SHA-256 |
|---|---|---|
| [1.jpeg](<C:/Users/abdelrhman/Desktop/المناوبة/New folder/1.jpeg>) | Official term duty roster, `QF71-B-2-2`; D3 roster renderer | `242FF09D0CE830E5592A05E3FD785F67B66C77CF3E68B998532B63465B05B997` |
| [2.jpeg](<C:/Users/abdelrhman/Desktop/المناوبة/New folder/2.jpeg>) | Subject/class/teaching-load context; preserves the distinction from equal duty rotation | `756160157D620C9E0E89A2E18EE0FC7C71CC25902B76259F7045985DF1262C5F` |
| [3.jpeg](<C:/Users/abdelrhman/Desktop/المناوبة/New folder/3.jpeg>) | Waiting-period register, `QF71-B-2-1`; outside D1–D3 duty implementation | `073C78E1087C58ECE728909EE92CE954AFBFD9ECDB8A80F57A365891E318398A` |
| [4.jpeg](<C:/Users/abdelrhman/Desktop/المناوبة/New folder/4.jpeg>) | **Primary daily report reference**, `QF71-B-2-3`; D2 field/section contract and D3 exact official layout | `573C985BF08EBB01B429F1CACE5FB4D5CEC6CC8F749B51CBF644E04DDA4F7E88` |
| [5.jpeg](<C:/Users/abdelrhman/Desktop/المناوبة/New folder/5.jpeg>) | Responsibility letter and acknowledgement; D2 snapshot and D3 official letter layout | `EBCFFB19BBC9F72C1094CBADA324ADCB40AE462AA4314C2A3CE40921812D1661` |

## Daily report — exact division and order

The official daily report must preserve the source's section order, table divisions, Arabic labels, column order and original/substitute identity/signature positions. It is not acceptable to replace it with a generic summary PDF. Responsive editing can use stacked cards **inside the same logical sections**.

| Order | Source section | Required mapping |
|---|---|---|
| 1 | Administrative header | Source school/education authority and branding positions; populate approved live-school values and freeze them with the signed snapshot |
| 2 | `تقرير المناوبة اليومي`, `QF71-B-2-3` | Preserve title/form identifier and academic year/term division |
| 3 | Duty identity table | Duty day, date, assigned teacher name, signature and notes; retain original planned teacher identity |
| 4 | `بدلاً عنه الأستاذ` row | Actual substitute identity and their own signature/notes in the source positions; never place an absent teacher's fabricated signature in the original row |
| 5 | `أسماء الطلاب المتأخرين` | Dedicated table, source column order: row number, name, class, time, action taken, notes. In the application these are **dismissal-lateness** facts selected by StudentId |
| 6 | `أسماء الطلاب المخالفين` | Separate table with the same source columns; do not merge with late students or introduce automatic disciplinary penalties |
| 7 | `الملاحظات` | Separate general observations area following both student tables |
| 8 | Handover instruction and administrative footer | Preserve source next-day handover wording and administrative identity/signature positions; no fabricated manager/agent signature or unrequested approval gate |

Last-student departure time is an already confirmed addition from the client conversation. Add a clearly labeled field adjacent to the observations/handover area, keeping all source sections intact. Guardian-delivery status, student stable IDs and dashboard internals belong to application metadata; do not invent new official table columns or displace the source columns.

The seven printed student rows are the source page capacity, not a maximum incident count. Retain the table division and repeat its headers on continuation pages. Handle empty categories explicitly, Arabic shaping, long student/class names, signature sizing and page breaks without changing the logical form.

## Letter and roster

The letter follows `5.jpeg`: administrative header, teacher addressee, day/date, supervision-until-last-student obligation, manager block and teacher acknowledgement/name/signature. Saved signatures belong to the person performing the explicit acknowledgement. Freeze dynamic school/year/term/date/name assets; do not reuse handwritten source examples.

The term roster follows `1.jpeg`: heading/year/term, week groupings, day/date/name/signature/notes, closure/exam rows and administrative footer. Preserve its grouped table structure, using continuation pages where term size requires them. Source signature columns never authorize invented completion/signature values.

## Mandatory review gates

- **Before D2 data/UI implementation:** reopen `4.jpeg` and `5.jpeg`; check every source section has a retained data/snapshot mapping. Review mobile picker/report sections at 390px, 768px and 1440px.
- **During D3 renderer implementation:** reopen `1.jpeg`, `4.jpeg` and `5.jpeg`. Generate actual Arabic PDFs and retain source/output comparison captures or a field-by-field visual checklist. Verify exact report divisions/column order, letter obligation, original/substitute signature ownership, empty sections, more than seven rows, long names, dynamic school headers and last departure.
- **Before D3 acceptance:** inspect actual rendered pages and archived byte hashes. Record any source mismatch and obtain an explicit decision before changing the official structure. A frontend preview, mock PDF or passing build does not prove fidelity; final client visual approval remains pending.

## D2 source review completed

Original report/letter images were opened before implementation. D2 stores all source sections and identity/signature mappings, with the confirmed last-departure addition and no seven-row business limit. Original private source images/handwriting were preserved outside the repository. [D2 verification](verification/d2.md) maps fields to data/snapshots and retains rendered mobile/tablet/desktop evidence. This completes the D2 data/UI reference gate only; actual official PDF/source comparisons and archive byte verification remain mandatory in D3.

## D3 source/output review - 2026-10-13

The original 1.jpeg, 4.jpeg and 5.jpeg were reopened, their recorded fingerprints checked and their content visually inspected before renderer design. Upright temporary report/letter copies were kept outside the repository. No private sample images, historical identities or source signatures became runtime assets.

[D3 verification](verification/d3.md#actual-source-to-output-inspection) maps each source section/column/signature position to actual generated output. Nine synthetic PDFs/fifteen pages retain original/substitute, explicit empties, more than seven entries, long observations, last departure, frozen unavailable branding and retained corrections. All pages of the representative three-page report and five-page roster were visually inspected. Licensed embedded Amiri provides Arabic shaping; canonical school Gregorian dates and frozen school-local times replace sample historical dates.

The long-name eighteen-week roster uses paired complete week groups over five readable pages, preserving source organization with continuation pages rather than compressing long names into the source's dense single page. No roster completion/signatures are invented. Final page PNGs/font/hash inventory and native mocked/live-local browser download evidence are retained. Provider-double hash equality is not real Drive delivery; owner visual acceptance remains pending. See D-110 for this pagination choice and remaining gates.
