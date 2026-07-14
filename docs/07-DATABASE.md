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
