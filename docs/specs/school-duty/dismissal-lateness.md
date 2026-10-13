# D2 Amendment — Student Dismissal Lateness

**Updated:** 2026-10-13. Owner-confirmed amendment implemented in authorized D2; [verification](verification/d2.md) distinguishes actual synthetic internal delivery from mocked browser presentation. D1 remains delivered; D3 is not started.

## Confirmed outcome

The actual duty teacher selects an enrolled student through a premium mobile student picker and records dismissal lateness. The system then creates **both a notice visible in the guardian dashboard and an automated message in the existing Messages tab** (`/student-affairs/messages`). The visible sender is `النظام` / `System`, never a teacher, manager or other person. This channel/identity decision is confirmed by the owner on 2026-10-13. The same fact appears in the student's record and existing manager statistics, with a count and identifiable student list. Student names are selected, not typed. The report retains the exact source section structure in [references.md](references.md).

Dismissal lateness is distinct from morning arrival delay, lesson delay, absence, gate-pass exit and a disciplinary violation. It does not create those facts, change their counters or imply a conduct penalty. The student may appear independently in the report's violation section when there is a real separate observation.

## Authorized execution defaults for D2

These engineering defaults were authorized by the submitted D2 prompt and implemented against the existing code. They are not additional owner answers. Source-data, transaction, guardian, statistics/history and rendered evidence are recorded in [D2 verification](verification/d2.md).

- An explicit confirmation commits the lateness fact and durable notification intent. Searching, selecting, autosaving a draft or opening a report does not send a notice. Do not postpone the notice until the end-of-day signed report or D3 PDF delivery.
- Record observed local date/time and the configured dismissal-time reference, action/notes and actual author/assignment revision. Actual departure time is optional until known; never infer that the student has left from the lateness notice. Derive delay minutes only when the comparison times are known. No arbitrary grace period is approved; missing dismissal timing needs an explicit setup/exception state.
- Use one active lateness fact per school/student/duty date. Repeated additions return or review that fact; multiple notes/departure updates do not create multiple initial notices. A student may have separate facts on different dates. Use stable fact IDs across drafts and retained signed report revisions.
- Commit fact, audit and durable notice intent atomically. The student record and dashboard query the committed fact, so they do not depend on final report submission or successful external delivery. A lost response, retry, concurrent confirmation or later report submission must not duplicate the incident/count/notice.
- Resolve recipients from current, school-scoped, effective `StudentGuardian` links marked to receive notifications, applying the established primary/eligible-recipient policy. Validate the student and relationship server-side; never accept guardian/user/contact IDs from the browser. Multiple eligible links follow the existing recipient policy rather than silently choosing an unrelated contact.
- Reuse the existing guardian dashboard notification and internal Messages infrastructure for the two confirmed surfaces. WhatsApp/SMS/provider activation is outside this requirement; the channel decision is resolved and requires no reconfirmation.
- Show Pending, Delivered, RetryScheduled, Failed/NeedsAttention and NoEligibleGuardian as appropriate to the real channel. Delivery means the channel's actual contract, not that the guardian read the notice. A missing guardian or messaging outage retains the lateness fact and gives an actionable manager exception; it must not invent success.
- A confirmed fact is not removed by deleting an unsent report row. Corrections/retractions require an authorized reason and retained audit/version. If an initial notice was already delivered, show that history and issue a distinct correction notice when appropriate; never erase the original notification or resend it as a new initial fact. Report corrections retain the signed versions and reference the current fact revision.

Suggested Arabic notice content identifies the school, student, duty date and observed dismissal lateness, with a clear request to arrange pickup. It contains no other student's data or private violation notes. Final wording is reviewed in D2 alongside English translations.

## System-authored dashboard notice and message

- The trusted server creates the notice/message from the confirmed fact. The teacher remains the recorded fact author in authorized audit/history, but is not the message sender. Label and store explicit system provenance; never impersonate a real user or display a teacher avatar/name as sender.
- One stable fact/recipient correlation links dashboard notice, message, student history and correction history. Use separate durable surface results so a dashboard success/message failure retries only the missing message, and vice versa. A normal new-message notification must not produce a second dashboard lateness alert for the same fact/recipient.
- The guardian dashboard must show the actual dismissal-lateness notice/content and student/date context, not merely increment a generic unread count. Provide a deep link to the corresponding system message in the existing Messages tab. Message visibility and dashboard access require current school/student/guardian authorization; stable thread identity must not expose other guardians' conversations or unrelated children.
- Messages use existing unread/list/read-receipt behavior. Sent/delivered/read are distinct: a dashboard read is not evidence that the message was read. Worker retries or opening the report/dashboard cannot send another initial message. Corrections retain originals and link a clear system correction on both surfaces.
- Dispatch after explicit fact confirmation without waiting for report submission, teacher office hours or a person's manual Send action. Preserve existing office-hour/authentication rules for ordinary human conversations; this is a narrow internal system-notice path.
- Add a real system-origin capability to current messaging contracts/queries/UI as necessary. Ordinary browser send endpoints cannot select/forge `System` origin, recipient identities or a system sender. Do not create a login-capable fake staff account or grant teachers extra messaging permissions as a workaround.
- Proposed UI default: system notices are a recognizable system thread/message presentation within existing Messages, with no pretend human reply recipient. If replies are later supported, explicitly route them to an authorized real recipient; no new conversational workflow is approved by this amendment.

Current messaging has required `ConversationMessage.SenderUserId`/`SenderUser` and human-oriented thread types; it does not currently establish a system sender. D2 must implement a narrow additive provenance model and trusted delivery seam, retain old sender/history semantics and enforce server authorization. A display-only rename to `النظام` is insufficient.

## Student picker — required interaction design

Use a full-height dialog on phones and a generous centered dialog on tablet/desktop, using Al-Falah green/gold tokens and shared search/select controls. The current duty teacher can search the whole enrolled school roster for that term, including classes they do not teach; this grants only minimal lookup, not broad student/guardian profile access.

1. **Fixed header:** `اختيار الطالب`, active school/term, clear close action and an accessible search field (`ابحث باسم الطالب أو رقمه المدرسي`). Debounced server search; retain focus and ignore stale responses.
2. **Filters:** grade and classroom/section, clear/reset, and a compact active-filter summary. Classes are real scoped IDs, not arbitrary text. Provide a readable classroom navigator/grouped results with class labels and result counts; keep search available when browsing a class.
3. **Scrollable result region:** class groups containing generous student rows with full name, class and safe school student number. Use stable StudentId/EnrollmentId so duplicate names remain distinguishable. Do not display national ID, guardian contacts or disciplinary data. Fetch results with server pagination; do not download the whole school to the phone.
4. **Fixed selection footer:** chosen student's name/class and `اختيار الطالب`, disabled until a selection exists. Selection automatically fills the report class. Keep search/filter context when returning to select another student; mark students already recorded and provide an action to review the existing fact.
5. **Focused fact review:** student, class, observed time, action/notes and a concise notice-intent message, followed by `تأكيد التأخر وإبلاغ ولي الأمر`. Keep report-row selection separate from this explicit commitment.

Single-student selection per confirmation is the initial default; rapid repeated entry preserves class/search context. On mobile, filters can collapse into a clearly labeled filter sheet. The virtual keyboard must not hide the selected-student summary or confirmation action. Empty class, no results, loading, fetch error, transferred/inactive selection, missing guardian, pending delivery, delivery failure and duplicate confirmation are designed states with preserved input.

Inspect rendered interactions at 390px, 768px and 1440px: long/duplicate Arabic names, large classes, search race, changing filters, scrolling/pagination, keyboard navigation/focus return, touch targets at least 44px, RTL/English LTR, reduced motion and no document-level horizontal overflow. Capture the student picker, confirmation, notice status and corrected fact. A passing build is insufficient; final client visual approval remains pending.

## Manager statistics and student history

Extend the existing manager dashboard operations/student area; do not create a competing dashboard. Add `تأخر الانصراف` with today's distinct-student count by default. Clicking opens the names, class, duty date, observation/departure times when known and notification status, with date-range/term/class filters and bounded pagination. For a multi-day range expose distinct students and incident count separately, so a repeated student is not ambiguously counted. Pending/failed delivery does not remove a real incident; corrected/retracted facts update active aggregates without erasing historical evidence.

Add a labeled dismissal-lateness item to the student's record/timeline and relevant profile statistics, including date/time, class at the event, responsible duty teacher, action and permitted notice status. Query it under existing student-record authorization. Duty lookup alone does not grant full student-record access. Preserve the same stable fact identity across dashboard, student record and duty report; no three independently editable copies.

## Current implementation seams — static findings

- `Student`, `StudentEnrollment`, `Classroom` and `StudentGuardian` already exist in `AcademicEntities.cs`; guardian links have effective dates and `ReceivesNotifications`.
- `StudentAffairsNotificationDispatcher.CreateGuardianNotificationsAsync` already resolves school/student links and creates deduplicated in-app `Notification` rows. D2 must inspect transaction/retry semantics rather than assuming this method alone provides durable new-fact delivery.
- The guardian dashboard already links `UnreadThreads` to `/student-affairs/messages` and `UnreadNotifications` to guardian notifications. The Messages tab uses `ConversationThread`/`ConversationMessage`/`MessageReceipt`; current sender FK and human send-handler assumptions require the system-provenance seam above. These are static findings, not implemented new notices.
- `SchoolManagerOperationsDto`, `DashboardService` and `school-manager-overview.component` are the existing manager statistics seam; there is no dismissal-lateness field in the inspected contract.
- `StudentWorkflowRepository.GetStudentTimelineAsync` currently returns an empty placeholder page. Merely writing a fact will not make it appear there: D2 must wire a real scoped dismissal-lateness projection and the corresponding record/profile UI. This finding does not authorize a general Student Affairs rewrite.

## Phase allocation and verification

**D2:** scoped roster picker, explicit lateness confirmation, guardian-dashboard notice **and internal system-authored message**, independent durable retry/read state, student-record projection, existing manager dashboard count/list, draft/report references and corrected-fact behavior. These are required D2 deliverables, not deferred to PDF work.

**D3:** source-faithful official PDF rendering, multi-page output and archive. Compare actual PDFs with `4.jpeg`/`5.jpeg` using the checklist in [references.md](references.md); the D2 editor follows the same section order while remaining easy on phones. Existing signature ownership and no-fabricated-signature rules remain mandatory.

Meaningful verification covers whole-school scoped search, filters, pagination and duplicate names; forged school/student/class/guardian IDs; inactive/expired links and missing guardians; lost responses and concurrent confirmation; atomic rollback and no repeated notice/message; one-surface failure followed by retry; actual guardian-dashboard content/deep link and Messages list/unread/read receipt; server-only system provenance and rejected browser spoofing; immediate system delivery outside human office hours; correction links on both surfaces; history/statistics before report submission; preserved morning-delay counts and existing human messaging rules. Distinguish real local internal-message persistence/UI verification from mocks; no external delivery claim.

## D2 delivery status

Explicit confirmation now commits fact/revision/audit/durable intents atomically, independently of report submission. The real hosted worker delivers both internal surfaces with server-owned System origin, one correlation per fact/revision/recipient, independent receipts and missing-only retries. Effective/expired/missing guardians and recipient/thread/school isolation are exercised with synthetic accounts. No real-parent test notice or external provider is used.

Manager today/range/class/term statistics and real student record/timeline/profile query the same committed facts and retained revisions. Morning-delay/absence/disciplinary metrics remain separate. Source sections and selected student enrollment identities survive signing/correction. Final client visual acceptance and physical mobile keyboard review remain pending; PDF/archive delivery remains D3.
