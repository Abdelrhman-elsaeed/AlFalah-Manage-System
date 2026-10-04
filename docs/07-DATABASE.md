# 07 — Database

**Status:** Baseline + verified Development database · **Last updated:** 2026-07-15

## Engine & ORM
- **SQL Server LocalDB** for development.
- **SQL Server** for production later.
- **EF Core Code First**.
- `appsettings.Development.json` for the local connection string.

## Example dev connection string
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=AlFalahDb;Trusted_Connection=True;MultipleActiveResultSets=true"
  }
}
```

## Timestamps
- Use **UTC DateTime** or **DateTimeOffset** for all timestamps.

## Required indexes
Add indexes for common lookups:
- SchoolId
- UserId
- RoleId
- IsActive
- CreatedAt

## Global query filters / soft delete
- If soft delete is added, apply **global query filters** where appropriate.

## Migrations
```bash
# Create a migration
dotnet ef migrations add <Name> --project AlFalah.Infrastructure --startup-project AlFalah.Api

# Apply migrations to the database
dotnet ef database update --project AlFalah.Infrastructure --startup-project AlFalah.Api
```
> Phase 1 migration already created: **InitialCreate**.
>
> Phase 2 migration: **Phase2SchoolUserManagement** — adds soft-delete columns (`IsDeleted`, `DeletedAt`, `DeletedByUserId`) to `Schools`, `Users`, `UserSchoolRoles`; adds `UpdatedAt`/`UpdatedByUserId` to `UserSchoolRoles`; adds global query filters in `OnModelCreating`; adds supporting indexes (`IX_*_IsDeleted`, `IX_Schools_Name_City_LocationDetails`, `IX_Users_IsActive`).

## Development database verification (2026-07-15)

- `dotnet ef database update --project AlFalah.Infrastructure --startup-project AlFalah.Api` completed successfully; no migrations were pending. The database is at `AddTeacherProfileClasses`.
- The current `AlFalahDbContext` intentionally maps ASP.NET Core Identity entities to `Users`, `Roles`, `UserRoles`, `UserClaims`, `UserLogins`, `RoleClaims`, and `UserTokens`. Therefore `AspNetUsers`/`AspNetRoles` are not table names in this project; query `Users`/`Roles` instead. No compatibility views or destructive rename were added.
- The live schema contains Identity, `Schools`, `Visits`, `RubricVersions`, `ImprovementPlans`, `Complaints`, `InstructorProfiles`, and all other current model tables.
- Development startup on `http://localhost:5264` ran migrations and the idempotent seeder. The baseline sample school is `مدرسة الفلاح النموذجية` (Riyadh, ID 1); existing visits and other manually-created rows were preserved.

## Intelligent Timetable Phase 02 schema (2026-09-06)

Applied migration: `20260905163146_AddTimetableTimings`, local development `AlFalahDb`.

- Adds `BellScheduleTemplates`, `BellScheduleRevisions`, `BellScheduleDays`, and `BellPeriods`. School-composite foreign keys constrain template selections and timetable revision links. Restricted deletes and the DbContext append-only guard preserve revision rows.
- Unique indexes: school/year/semester/active name, template/revision, revision/day, day/sequence. Check constraints enforce day ranges, positive sequences and start-before-end. Cross-row overlap and contiguous chronological sequence validation run in the application before the complete revision is persisted transactionally.
- Adds the timing revision reference and `TimingsRequireRevalidation` to `SchoolTimetables`; wires the Phase 01 setup template relationship. Widens period references to `int` and allows Friday. `CK_TeacherOfficeHours_TimeShape` is removed/recreated around column widening in both migration directions.
- Template and dependent setup/timetable revisions use optimistic concurrency; conflicting requests return HTTP 409. Audit rows and dependency invalidation commit in the same transaction as revision/selection changes.
- No guessed historical backfill. Existing unconfigured timetables require selecting a template and explicit publication. Development E2E seed data supplies a realistic six-period template and targeted lessons.

For local EF commands set `ALFALAH_MIGRATIONS_CONNECTION` from the Development connection configuration; the design-time factory otherwise uses its separate migrations database. Do not substitute the base appsettings connection for local verification.

## School File Storage S1 schema (2026-10-03)

Migration `20261003023752_SchoolFileStorageFoundation` creates 11 additive tables, scoped composite foreign keys, search/unique filtered indexes, rowversion, immutable-history/tree/scope triggers, and 8 permission rows with manager/teacher mappings. It drops, renames or alters no legacy table/column. The cyclic current-version FK is explicitly defined in the migration rather than the EF navigation model; preserve this constraint when evolving the schema.

Nullable active-link and school/year template uniqueness use explicit filtered index shapes, documented and exercised on SQL Server in [S1 verification](specs/school-file-storage/verification/README.md). No destructive Down: rollback uses flags and retains history. The migration was tested on a fresh LocalDB database and an isolated COPY_ONLY clone of Development. Original Development and production were not migrated; no Google configuration/credential was modified.

Offline backfill is opt-in and never runs at API startup. [Runbook](specs/school-file-storage/scripts/README.md) requires an explicit local database and report, offers dry-run, and reports unmapped rows or drift without overwriting history. Existing APIs/matrix continue using legacy tables during S1.

## School File Storage S2 persistence (2026-10-03)

Additive migrations `20261003034907_SchoolFileStorageLibrary` and `20261003040217_SchoolFileStorageLibraryIntegrity` add StorageOperations and scoped folder/file search indexes. Operations are unique by school/actor/request key, plus a filtered school-wide CreateFolder key and school/provider item ID; scoped folder/file/version FKs, rowversion and status/completion checks retain the S1 explicit current-version FK and immutable-history triggers. Asset/version/pointer/completion/audit commit atomically; reserved ID survives failed completion for reconciliation. Down refuses physical deletion.

Both S1/S2 were tested together on fresh isolated LocalDB fixtures, including concurrent uploads and SQL-save rollback; original Development was not migrated. [Reviewable S2 SQL](specs/school-file-storage/verification/s2-migration.sql), [verification and cutover gates](specs/school-file-storage/verification/s2-library-and-uploads.md), [5000-asset performance](specs/school-file-storage/verification/s2-performance.json). S1 backfill is not a dual-write mechanism for S2 legacy uploads; final provenance/link/matrix drift handling is a S3/rollout gate.

## School File Storage S3 integrity (2026-10-04)

Four additive migrations: `20261003231834_SchoolFileStorageEvidenceReview`, `20261003232149_SchoolFileStorageReviewIntegrity`, `20261003233803_SchoolFileStoragePreApprovalReplacement`, `20261003233933_SchoolFileStorageReviewerIdentity`. New scoped FileChangeRequests/FileChangeDecisions, one pending request/file, one candidate operation/request, exact task mapping uniqueness, mandatory nonlegacy reject reasons and reviewer identity snapshots. All existing current-version FKs, immutable version identity and independent-link filtered indexes are retained. Append-only review/change decisions and Storage.* audit are enforced in SQL; request provenance/policy/original/candidate history and shared writer provenance cannot be rewritten. Mapped legacy review columns are frozen by trigger.

Serializable workflow transactions touch the asset as well as its links/requests, so concurrent review/replacement and stale rowversions fail with409. Candidate version/upload completion/audit is atomic; accepting its request atomically moves the current pointer, resets active links and appends a decision/audit. SQL/provider uncertainty retains the reserved operation for S2 reconciliation. Constraints use portable trim comparisons, preserving the existing SQLite query test without relaxing SQL rejection rules.

Offline `--repair-shared-writer` verifies pre-S3 completed S2 operation/file/version/source identity, stamps a separate immutable baseline and adds an exact mapped link/legacy decision if absent. It never overwrites LegacyProvenanceJson or pretends to reconstruct a missing original snapshot. Subsequent backfill validates immutable identity/review/byte metadata; normal availability/name/withdrawal/link decisions do not create false source drift. Repair/backfill reruns are idempotent. The adapter requires an isolated AlFalahSFS_* or AlFalahS1Tests_* database and refuses actual configured Development. [SQL script](specs/school-file-storage/verification/s3-migration.sql), [comparison and limits](specs/school-file-storage/verification/s3-evidence-and-review.md). No operational migration/backfill/cutover; Down refuses destructive rollback.
