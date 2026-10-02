# Messaging office-hours policy — 2026-10-03

This policy supersedes the teacher-reply restriction described in the W4 office-hours report.

- Instructors, Student Affairs Officers, and Social Workers with existing messaging access can send at any time. Their messages are delivered immediately, including when office hours are not configured.
- Guardian messages to instructors are accepted at any time. Within an eligible office-hour occurrence they are delivered immediately; outside it they are persisted with pending receipts and scheduled for the earliest eligible occurrence.
- Guardian messages to Student Affairs Officers and Social Workers remain immediate; those recipients do not have teacher office-hour configurations.
- The existing durable queue is implemented through persisted conversation messages, receipts, and release events in the database outbox. The worker revalidates the occurrence before release. A message without an eligible occurrence remains pending until configuration or timetable reconciliation supplies one.
- The guardian sees an Arabic notice beside their pending message, stating that it was sent outside office hours and showing the nearest office-hour date/time as the expected reply time. The time uses the school's offset from the server response, independently of the browser's timezone. This is an expectation, not a guaranteed response deadline.
- If no eligible occurrence is configured, the notice explicitly says that an expected reply time cannot currently be determined.
- The notice survives reloads, follows server changes to the scheduled time, and disappears once the message is delivered. Staff do not see the guardian notice.
- Eligible selections are evaluated by start time, so selection/insertion order cannot cause a later office hour to be reported as the nearest one.

Existing participant, school, role, permission, closed-conversation, and idempotency checks remain in effect. No database migration is required.

## Verification

- Backend: 24 tests passed with `dotnet test backend/AlFalah.Tests/AlFalah.Tests.csproj --configuration Release --filter "FullyQualifiedName~MessagingDeliveryTests|FullyQualifiedName~GatePassAndMessagingMediatRTests" --no-restore --verbosity minimal`.
- Frontend: 12 tests passed with `npm test -- --watch=false --browsers=ChromeHeadless --include=src/app/features/student-affairs/messaging-chat/messaging-chat.component.spec.ts` from `frontend`.
- Repository tests exercise actual message persistence, immediate staff delivery, guardian queue acceptance, idempotent retry, earliest recurring occurrence, office-hour boundaries, and release timing with a fixed clock and EF Core InMemory.
- Frontend tests cover Arabic notice rendering after send, draft clearing, school-local time, persisted messages, updated schedules, missing office hours, delivery completion, and role/ownership visibility.
