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
