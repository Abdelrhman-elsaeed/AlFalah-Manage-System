# 07 — Database

**Status:** Baseline · **Last updated:** 2026-07-10

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
