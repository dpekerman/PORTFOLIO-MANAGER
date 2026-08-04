# Portfolio Manager — Auth, Roles & UI Enhancement Report

**Branch:** `feature/tailwind`  
**Date:** 2026-08-03  
**Status:** ✅ All Changes Implemented & Compiled

---

## 1. JWT Authentication & Login System

### What was built

Full token-based auth from scratch on an application that previously had no authentication.

| Layer           | Technology                                   | Details                              |
| --------------- | -------------------------------------------- | ------------------------------------ |
| Backend auth    | ASP.NET Core Identity + JWT Bearer           | PBKDF2 password hashing, signed JWTs |
| Token lifecycle | 15-min access token + 7-day refresh token    | Refresh token hashed (SHA-256) in DB |
| Cookie security | httpOnly, Secure (prod), SameSite=Strict     | Scoped to `/api/auth` path only      |
| Secret storage  | `dotnet user-secrets` (dev) / env var (prod) | Never in source code                 |

### Auth flow

```
App start → APP_INITIALIZER
  ├─ GET /api/auth/setup-required
  │   ├─ true  → navigate /setup  (first-run admin creation screen)
  │   └─ false → POST /api/auth/refresh (silent refresh via httpOnly cookie)
  │               ├─ success → user stays logged in
  │               └─ fail    → navigate /login
  │
  401 from any API call → silent refresh → retry → if fails → /login
```

### Files added

| File                                            | Purpose                                               |
| ----------------------------------------------- | ----------------------------------------------------- |
| `Models/ApplicationUser.cs`                     | IdentityUser + DisplayName, CreatedAt                 |
| `Models/RefreshToken.cs`                        | Hashed token + UserId FK + expiry                     |
| `Models/JwtSettings.cs`                         | Typed config class                                    |
| `Models/AuthModels.cs`                          | Request/response records                              |
| `Services/ITokenService.cs` + `TokenService.cs` | JWT + SHA-256 refresh token generation                |
| `Controllers/AuthController.cs`                 | setup-required, setup, login, refresh, logout, me     |
| `Controllers/UsersController.cs`                | Admin-only user CRUD                                  |
| `core/services/auth-api.service.ts`             | HTTP wrappers                                         |
| `core/services/auth-state.service.ts`           | Signal state: currentUser, accessToken, setupRequired |
| `core/services/users-api.service.ts`            | Users API wrapper                                     |
| `core/interceptors/auth.interceptor.ts`         | Bearer token attach + 401 silent refresh              |
| `features/auth/login/`                          | Login screen (email + password)                       |
| `features/auth/setup/`                          | First-run admin creation screen                       |

---

## 2. User Roles (Admin, Trader, Viewer)

### Role matrix

| Operation                                           | Admin | Trader | Viewer |
| --------------------------------------------------- | ----- | ------ | ------ |
| View all data                                       | ✅    | ✅     | ✅     |
| Add/Edit/Delete portfolio data                      | ✅    | ✅     | ❌     |
| Configuration (notifications, schedule, allocation) | ✅    | ❌     | ❌     |
| User management                                     | ✅    | ❌     | ❌     |
| RSI Scanner / EOD Signals (read)                    | ✅    | ✅     | ✅     |

### Backend enforcement

All 13 controllers have class-level `[Authorize]` (any authenticated user).  
Write methods additionally get:

- Data controllers (Portfolio, Watchlist, Cash, Options): `[Authorize(Roles = "Admin,Trader")]`
- Config controllers (Notification, Allocation, SectorIndustry, ValueScreener, Scanner, EodSignals): `[Authorize(Roles = "Admin")]`

### Frontend enforcement

Write buttons are **hidden** (not disabled) for Viewer role:

- `[style.display]="authState.canWrite() ? null : 'none'"` on all write buttons
- Portfolio: 17 buttons hidden
- Watchlist: 5 buttons hidden
- Transactions: 4 buttons hidden
- Config → Users tab: `@if (authState.isAdmin())` guard

---

## 3. Per-User Data Isolation

### Database changes

Added nullable `UserId` FK column to four user-owned tables:

| Table            | Column added                | Notes                                                 |
| ---------------- | --------------------------- | ----------------------------------------------------- |
| `PortfolioItems` | `UserId NVARCHAR(450) NULL` |                                                       |
| `WatchlistItems` | `UserId NVARCHAR(450) NULL` | Unique index changed: `(Symbol)` → `(Symbol, UserId)` |
| `CashItems`      | `UserId NVARCHAR(450) NULL` |                                                       |
| `OptionItems`    | `UserId NVARCHAR(450) NULL` |                                                       |

Migration: `AddUserIdToPrivateData` (applied ✅)

### Service filtering logic

All four data services (Portfolio, Watchlist, Cash, Option) inject `IHttpContextAccessor` and apply:

```csharp
private IQueryable<T> OwnedItems() {
  var q = db.Items.AsQueryable();
  if (IsAdmin()) return q;          // Admin sees everything
  var uid = CurrentUserId();
  return q.Where(x => x.UserId == uid || x.UserId == null);  // own data + legacy unowned
}
```

**Null-owned legacy data** (records that existed before auth was added) is visible to all users until re-attributed.

### Shared data (not user-owned)

| Table                                                      | Reason                       |
| ---------------------------------------------------------- | ---------------------------- |
| `DailySignals`                                             | Public RSI market scan data  |
| `ValueScreenerSnapshots`                                   | Shared screener results      |
| `ValueScreenerScheduleConfigs`                             | System schedule config       |
| `AllocationRiskTargets/SectorTargets/SinglePositionLimits` | Portfolio-wide configuration |

---

## 4. User Management UI

- Located at **Config → Users** tab (Admin-only, hidden from Trader/Viewer)
- Features: Create user (DisplayName, email, password, role), change role inline, delete user
- Safety guards: Cannot delete self, cannot remove last Admin
- Backend endpoint: `UsersController` with `[Authorize(Roles = "Admin")]`

---

## 5. Demo Mode Enhancement

### Two masking styles (mutually exclusive toggle)

| Style            | How it works                                                           | When to use                                     |
| ---------------- | ---------------------------------------------------------------------- | ----------------------------------------------- |
| **Blur & Scale** | CSS `filter: blur(6px)` on real values                                 | Screen sharing where layout should be preserved |
| **Fake Numbers** | Knuth hash generates completely unrelated plausible amounts ($1K–$78K) | Business demos where real scale must be hidden  |

### UI changes

- Replaced two separate stroked buttons with `mat-button-toggle-group` for clear exclusive selection
- Description text updates based on selected style
- Both modes persist in `localStorage`

### Template integration

- `[class.demo-redact]` (CSS blur) only applied when blur mode active
- Key financial values piped through `dv(value)` helper in portfolio page:
  - Total portfolio value
  - Day gain amounts
  - Total cash
  - Total options market value / cost
  - Individual cash item amounts
- `dv()` / `dvp()` helpers added to `PortfolioPageComponent` and `TransactionsPageComponent`

---

## 6. Responsive Toolbar Fix

### Problem

At screen widths > 1200px, all 8 nav labels showed simultaneously alongside the user chip and action buttons, making the toolbar overcrowded.

### Fix

- Nav text label breakpoint increased: `1200px` → `1400px`
- User chip name breakpoint: `1100px` → `1400px`
- At ≤ 1400px: icons only with tooltips (clean, compact)
- At > 1400px: icons + labels (large monitors only)

---

## 7. Database Scripts Updated

| Script                           | Change                                            |
| -------------------------------- | ------------------------------------------------- |
| `11_AddIdentityAndAuth.sql`      | **New** — creates Identity tables + RefreshTokens |
| `02_CreateTables.sql`            | Appended Identity + RefreshTokens DDL             |
| `05_DeleteAllData.sql`           | Deletes auth tables in FK-safe order              |
| `00_MASTER_DeployProduction.sql` | Step 8: Identity tables + updated NEXT STEPS      |

---

## 8. EF Migrations Applied

| Migration                     | Changes                                                                                                                          |
| ----------------------------- | -------------------------------------------------------------------------------------------------------------------------------- |
| `AddIdentityAndRefreshTokens` | AspNetUsers, AspNetRoles, AspNetUserRoles, AspNetUserClaims, AspNetUserLogins, AspNetUserTokens, AspNetRoleClaims, RefreshTokens |
| `AddUserIdToPrivateData`      | UserId column on 4 tables; WatchlistItem unique index update                                                                     |

---

## 9. Pre-Production Checklist

### ✅ Done

- JWT secret in user-secrets (never in source)
- `Secure` cookie flag: tied to `Request.IsHttps` (false in dev, true in prod)
- `app.UseAuthentication()` before `app.UseAuthorization()`
- `WeatherForecast` template stub removed from Program.cs
- Role seeding on every startup
- CORS: `AllowCredentials()` for httpOnly cookie

### ⚠️ To do before production

1. **Rate limiting** on `/api/auth/login` — add ASP.NET Core `RateLimiter` middleware
2. **CORS origins** — change `http://localhost:4200` to production domain
3. **Environment variable** — set `ASPNETCORE_ENVIRONMENT=Production`
4. **Jwt:Secret** — set as environment variable (not user-secrets) in production
5. **HTTPS** — enforce in production (Kestrel / reverse proxy)
6. **Expired refresh token cleanup** — add background job to prune `RefreshTokens` table
7. **Audit logging** — consider logging login/logout/failed attempts
8. **Email verification** (optional, post-cloud migration)
9. **Password reset via email** (optional, post-cloud migration)

---

## 10. How to Start

```powershell
# Backend (port 5000)
cd backend\PortfolioManager.Api
dotnet run --launch-profile http

# Frontend (port 4200)
cd frontend\portfolio-manager-ui
npx ng serve
```

**First run:** Browser navigates to `/setup` → create Admin account → use Config → Users to add more users.

**Returning users:** Silent JWT refresh via httpOnly cookie keeps session alive across page reloads.
