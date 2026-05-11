# Multi-User LinkedIn Import Design

**Date:** 2026-05-11

## Goal

Allow multiple users to register, log in, and import their own LinkedIn profile into the system by pasting their public LinkedIn URL — no manual data entry, no cookie export, no technical steps.

## Architecture

JWT-based auth (email + password) layered onto the existing .NET 10 + React stack. Each user owns their profile snapshots. LinkedIn data is scraped from the user's public profile URL using the existing PuppeteerSharp headless browser — no login, no cookies required.

**Tech Stack:** ASP.NET Core 10, EF Core 9, Pomelo MySQL, PuppeteerSharp, React + TanStack Query, JWT (System.IdentityModel.Tokens.Jwt)

---

## Data Model

### New: `User` entity

| Column        | Type         | Notes                          |
|---------------|--------------|--------------------------------|
| Id            | int (PK)     | Auto-increment                 |
| Email         | string (255) | Unique, required               |
| PasswordHash  | string       | ASP.NET `PasswordHasher<User>` |
| IsAdmin       | bool         | Default false                  |
| CreatedAt     | DateTime     | UTC, set on insert             |

### Modified: `ProfileSnapshot`

- Add `UserId` (int, FK → `User.Id`, cascade delete)
- Existing snapshots will be deleted by migration (no real users yet)

### Constraints

- `User.Email` has a unique index
- `ProfileSnapshot.UserId` is required (not nullable)

---

## Auth

### Endpoints (no auth required)

**`POST /api/auth/register`**
- Body: `{ "email": "...", "password": "..." }`
- Validates: email format, password min 8 chars
- First registered user gets `IsAdmin = true`
- Returns: `{ "token": "<JWT>" }`
- Errors: 400 if email already exists or validation fails

**`POST /api/auth/login`**
- Body: `{ "email": "...", "password": "..." }`
- Returns: `{ "token": "<JWT>" }`
- Errors: 401 if credentials invalid

### JWT

- Claims: `sub` (userId), `email`, `isAdmin`
- Expiry: 7 days
- Signing key: from `appsettings.json` → `Jwt:Key` (min 32 chars)
- Algorithm: HS256

### Password hashing

`Microsoft.AspNetCore.Identity.PasswordHasher<User>` — built into ASP.NET, no extra packages. Bcrypt-compatible work factor.

### Protected endpoints

All `/api/profile/*` endpoints require `Authorization: Bearer <token>`. Unauthenticated requests get 401. Non-admin requests to admin-only endpoints get 403.

---

## LinkedIn Import Flow

### Endpoint

**`POST /api/profile/import`** (authenticated)
- Body: `{ "linkedInUrl": "https://www.linkedin.com/in/username" }`

### Server flow

1. Validate URL matches `linkedin.com/in/` — return 400 if not
2. Launch headless Chromium with `--no-sandbox`, `--disable-dev-shm-usage`, `--disable-gpu` (Docker-compatible)
3. Navigate to the URL (no cookies, no login)
4. Check landed URL — if it contains `/login`, `/authwall`, or `/checkpoint`, return 400:
   > "Your LinkedIn profile is set to private. Go to LinkedIn → Settings → Visibility → Profile viewing options → set to Public, then try again."
5. Extract profile data using existing DOM extraction (name, headline, location, about, photo, experience, education, skills, projects, certifications)
6. Save `ProfileSnapshot` with `UserId` from JWT claim
7. Return saved profile DTO

### `LinkedInScraperService` changes

- Signature: `ScrapeProfileAsync(string url)` (was no parameters, hardcoded `/in/me/`)
- Remove cookie injection logic
- Keep all existing DOM extraction JS
- Keep headless Chromium launch args

### `SyncController`

Removed — replaced by `POST /api/profile/import` on `ProfileController`.

---

## API Endpoints Summary

| Method | Path                    | Auth     | Description                        |
|--------|-------------------------|----------|------------------------------------|
| POST   | /api/auth/register      | None     | Register, returns JWT              |
| POST   | /api/auth/login         | None     | Login, returns JWT                 |
| POST   | /api/profile/import     | User     | Import LinkedIn profile by URL     |
| GET    | /api/profile            | Admin    | All users' profile summaries       |
| GET    | /api/profile/latest     | User     | Current user's latest snapshot     |
| GET    | /api/profile/:id        | User     | Specific snapshot (must own it)    |
| GET    | /api/profile/status     | User     | Current user's sync status         |

---

## Frontend

### New pages

| Route      | Description                                                  |
|------------|--------------------------------------------------------------|
| /register  | Email + password form → calls register → redirects to /import |
| /login     | Email + password form → calls login → redirects to /         |
| /import    | LinkedIn URL input → calls import → redirects to /           |

### Changed pages

| Route       | Change                                                        |
|-------------|---------------------------------------------------------------|
| /           | Shows current user's latest profile (was: global latest)     |
| /admin      | Admin only — lists all users + latest profile summary each   |
| /profile/:id | Unchanged (historical snapshot view)                       |

### Removed

- Sync card on Admin page (replaced by `/import` flow)
- Cookie card (no longer needed)

### Auth state

`useAuth` hook reads JWT from `localStorage`, decodes claims (`userId`, `email`, `isAdmin`). Protected routes redirect to `/login` if token is absent or expired.

### Navigation

```
Unauthenticated:   /login   /register
Authenticated:     /  |  /import  |  /admin (admin only)  |  [Logout]
```

---

## Dockerfile

The API Docker image runtime stage must install Chromium system libraries for headless browser support:

```
libnss3, libatk1.0-0, libatk-bridge2.0-0, libcups2, libdrm2, libxkbcommon0,
libxcomposite1, libxdamage1, libxfixes3, libxrandr2, libgbm1, libasound2,
libpangocairo-1.0-0, libpango-1.0-0, libcairo2, libgdk-pixbuf-2.0-0,
libgtk-3-0, libx11-xcb1, libxcb-dri3-0, fonts-liberation, xdg-utils
```

(Same requirement as the old headless plan — still needed even without cookie injection.)

---

## Cleanup (in scope for implementation)

The following were committed earlier and must be removed as part of this feature:

- `LinkedInPortfolio.API/Models/AppSetting.cs` — delete
- `LinkedInPortfolio.API/Services/ISettingsService.cs` — delete
- `LinkedInPortfolio.API/Services/SettingsService.cs` — delete
- `LinkedInPortfolio.API/Controllers/SettingsController.cs` — delete
- `AppDbContext.cs` — remove `DbSet<AppSetting>` and its `OnModelCreating` config
- `Program.cs` — remove `AddScoped<ISettingsService, SettingsService>()`
- EF migrations for `AddAppSettings` and `AddAppSettingKeyUniqueIndex` — delete migration files, add a new migration that drops the AppSettings table
- `SyncController.cs` — delete

---

## What Is Not In Scope

- Email verification on registration
- Password reset / forgot password
- Rate limiting on import (one user importing repeatedly)
- Proxy rotation for LinkedIn scraping
- Admin UI to promote users to admin (done via DB directly)
- Profile editing after import
