# 01 — Architecture

**Status:** Baseline · **Last updated:** 2026-07-10

## Style
**N-Tier / Layered Architecture** implemented as a **modular monolith**.
No microservices.

## Backend projects & responsibilities
| Project | Responsibilities |
|---------|------------------|
| **AlFalah.Api** | Controllers, Middlewares, Filters, `Program.cs`, Authentication/Authorization setup, Swagger setup |
| **AlFalah.Application** | DTOs, Services, Interfaces, Validators, UseCases, Mapping, business application logic |
| **AlFalah.Domain** | Entities, Enums, Constants, Domain rules |
| **AlFalah.Infrastructure** | DbContext, EF Core configurations, Identity implementation, Repositories (if needed), Seeders, File storage abstraction placeholder, PDF generation placeholder, Email placeholder |
| **AlFalah.Shared** | `ApiResponse`, common models, helpers, constants, localization resources (if needed) |

## Controller & layering rules
- Controllers must be **thin**.
- **No** business logic in controllers.
- **No** direct EF Core queries in controllers.
- Use **services**.
- Use **DTOs**.
- Use **validations**.
- Use **ApiResponse**.
- Use **global exception middleware**.
- Use **CurrentUserService**.
- **Enforce school context in backend**.
- **Do not** rely on frontend filtering for security.

## Standard API response
```json
{
  "isSuccess": true,
  "message": "",
  "data": null,
  "errors": []
}
```

## Backend folder tree
```
D:\AlFalah-Manage-System
├── backend
│   ├── AlFalah.sln
│   ├── AlFalah.Api
│   ├── AlFalah.Application
│   ├── AlFalah.Domain
│   ├── AlFalah.Infrastructure
│   └── AlFalah.Shared
└── frontend
    └── al-falah-app
```
> Naming correction from the original proposal: use **"AlFalah"** (not "Afalah"),
> and **avoid spaces** in folder names.

## Frontend folder tree
```
src/app/core
├── auth
├── guards
├── interceptors
├── services
├── layout
└── localization

src/app/shared
├── components
├── pipes
├── validators
└── directives

src/app/features
├── auth
├── main-manager
├── school-manager
├── moderator
├── instructor
├── schools
├── users
├── roles
├── settings
└── dashboard
```

See [06-FRONTEND-ARCHITECTURE.md](06-FRONTEND-ARCHITECTURE.md) for details.
