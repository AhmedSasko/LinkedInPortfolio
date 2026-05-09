# LinkedIn Portfolio — Design Spec

**Date:** 2026-05-10  
**Status:** Approved

---

## Overview

A full-stack personal portfolio app that scrapes the owner's LinkedIn profile using PuppeteerSharp (headless Chromium), stores the data in a local SQL Server database, and displays it as a clean portfolio website.

---

## Architecture

Two projects in one solution:

- `LinkedInPortfolio.API` — .NET Core 8 Web API (scraping, storage, serving)
- `linkedin-portfolio-ui` — React (Vite + TypeScript) frontend

```
React Frontend  ──HTTP──►  .NET Core API  ──EF Core──►  SQL Server LocalDB
                                │
                         PuppeteerSharp
                         (headless Chromium)
                                │
                           linkedin.com
```

The API handles all business logic. React is purely a display layer. No authentication — the portfolio page is public and the admin/sync page is localhost-only.

---

## Data Model (EF Core Code-First)

### `ProfileSnapshot`
| Column | Type | Notes |
|---|---|---|
| Id | int PK | |
| FetchedAt | datetime | Last sync timestamp |
| Name | string | |
| Headline | string | |
| Location | string | |
| About | string | |
| PhotoUrl | string | URL or base64 |

### `Experience`
| Column | Type |
|---|---|
| Id | int PK |
| Title | string |
| Company | string |
| StartDate | string |
| EndDate | string |
| Description | string |
| IsCurrent | bool |

### `Education`
| Column | Type |
|---|---|
| Id | int PK |
| School | string |
| Degree | string |
| FieldOfStudy | string |
| StartYear | string |
| EndYear | string |

### `Skill`
| Column | Type |
|---|---|
| Id | int PK |
| Name | string |
| EndorsementCount | int |

### `Project`
| Column | Type |
|---|---|
| Id | int PK |
| Title | string |
| Description | string |
| Url | string |
| StartDate | string |
| EndDate | string |

### `Certification`
| Column | Type |
|---|---|
| Id | int PK |
| Name | string |
| IssuingOrganization | string |
| IssueDate | string |
| CredentialUrl | string |

**Sync strategy:** Each manual sync fully replaces all records (delete + re-insert). Sync only writes on full success — on failure, existing DB data is untouched.

---

## Scraping Strategy

`LinkedInScraperService` steps:

1. Launch PuppeteerSharp headless Chromium
2. Navigate to `linkedin.com/login`, fill credentials from config
3. Navigate to `linkedin.com/in/{profileSlug}`
4. Scroll page to trigger lazy-loaded sections
5. Click "Show more" / expand buttons for About, Experience, Skills
6. Extract data via DOM selectors for each section
7. Return structured `ProfileData` DTO
8. Close browser

**Configuration** (`appsettings.json`):
```json
"LinkedIn": {
  "Email": "your@email.com",
  "Password": "yourpassword",
  "ProfileSlug": "your-profile-slug"
}
```

Credentials are never hardcoded. Random delays between actions reduce anti-bot detection risk. Non-headless fallback available if headless gets blocked.

---

## API Endpoints

| Method | Route | Description |
|---|---|---|
| GET | `/api/profile` | Returns full profile data from DB |
| POST | `/api/sync` | Triggers scrape, replaces DB, returns result |
| GET | `/api/profile/status` | Returns last sync timestamp + record counts |

### `GET /api/profile` response
```json
{
  "fetchedAt": "2026-05-10T14:00:00Z",
  "name": "string",
  "headline": "string",
  "location": "string",
  "about": "string",
  "photoUrl": "string",
  "experience": [...],
  "education": [...],
  "skills": [...],
  "projects": [...],
  "certifications": [...]
}
```

### `POST /api/sync` response
```json
{ "success": true, "syncedAt": "2026-05-10T14:00:00Z", "message": "Profile synced successfully" }
```
On failure:
```json
{ "success": false, "message": "Failed to scrape: login blocked" }
```

CORS allows `localhost:5173` (React dev) and production origin.

---

## React Frontend

**Tech:** Vite + TypeScript + React + TailwindCSS + TanStack Query + React Router

### Routes

**`/` — Portfolio Page**
Resume-style public display:
- Header: photo, name, headline, location
- About section
- Experience timeline
- Education
- Skills grid + Certifications
- Projects cards

**`/admin` — Sync Page**
- Last sync timestamp
- "Sync from LinkedIn" button
- Loading spinner during sync (30–60s expected)
- Success / error message after sync

---

## Key Constraints

- Personal portfolio — single user, no multi-tenancy
- Admin page is unprotected but localhost-only
- LinkedIn scraping violates LinkedIn ToS — for personal/educational use only
- SQL Server LocalDB — no external DB server required
