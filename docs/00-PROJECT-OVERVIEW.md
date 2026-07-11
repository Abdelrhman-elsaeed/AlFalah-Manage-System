# 00 — Project Overview

**Status:** Baseline · **Last updated:** 2026-07-10

## Vision
A new web application for **Al-Falah Schools** that manages classroom visits,
instructor evaluations, reports, improvement plans, follow-ups, complaints, and
dashboards across **multiple schools**. The old desktop system is only a
**reference**; we are **not** modifying or directly migrating it. We are building
a clean web application from scratch.

## The old desktop system (reference only)
A single-school, single-user Electron / React / SQLite app used by a supervisor to:
- Manage teachers.
- Create classroom visits.
- Score teachers against **25 standards** grouped under **5 domains**.
- Generate automatic analysis.
- Print Arabic reports.
- Create improvement plans for weak domains.
- Add follow-ups and progress percentages.

## What the new system must support
- Multiple schools.
- Independent school data.
- Real authentication.
- Real authorization.
- Roles and permissions.
- Arabic and English.
- ASP.NET Core Web API backend.
- Angular frontend.
- SQL Server database.
- EF Core Code First.
- N-Tier / Layered Architecture.
- Future scalability and feature expansion.

## Target stack
| Concern | Choice |
|--------|--------|
| Backend | ASP.NET Core Web API (.NET 8) |
| Frontend | Angular (17+) |
| UI Library | PrimeNG |
| Database | SQL Server |
| ORM | EF Core Code First |
| Auth | ASP.NET Core Identity + JWT + Refresh Tokens |
| Architecture | N-Tier / Layered Architecture (modular monolith) |
| Deployment | One server initially |
| Language | **Arabic primary**, English secondary |
| RTL | Required from day one |

## Business domain summary
- Multiple schools under the same organization; each school is **independent** in data.
- A school identity is **not only its name** — the same name may exist in
  different cities (e.g. Al-Falah Primary School in Jeddah vs. in Makkah).
  Identity uses at least: **Name + Stage + City + LocationDetails**.
- Each school has exactly **one School Manager** and exactly **one stage**.

See [02-DOMAIN-MODEL.md](02-DOMAIN-MODEL.md) for the full `School` entity.
