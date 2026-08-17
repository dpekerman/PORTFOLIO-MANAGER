# Portfolio Manager – TSX Momentum Scanner

A full-stack executive-grade stock portfolio tracker and RSI momentum scanner built with **Angular 22**, **.NET 8**, and **SQL Server**, using [Yahoo Finance](https://finance.yahoo.com) as the market data provider.

> **No API key required.** All live data is sourced from Yahoo Finance — just run the backend and start scanning.

---

## Features

- **Executive RSI Momentum Scanner** — TSX watchlist (50 symbols), scanned for RSI(14) extremes
- **5 Technical Indicators per signal**: RSI/Stochastics · MACD crossover · Bollinger Bands · Volume (OBV) · 50/200 DMA deviation
- **Reversal Probability Rating** — Low / Medium / High (aggregate of 5 indicator scores)
- **CONFIRMED vs EARLY WARNING** signal classification with 3 trigger rules each
- **Portfolio CRUD** — add/remove TSX/US stocks, live price quotes when API key is set
- **Dark Bloomberg-style UI** — Angular Material 22, OnPush, signals, fully responsive
- **Mobile responsive** — card layout on ≤768px, horizontal scroll with touch support

---

## Architecture

```
PORTFOLIO-MANAGER/
├── backend/
│   └── PortfolioManager.Api/      # .NET 8 Web API (port 5000)
│       ├── Controllers/           # portfolio, stocks, scanner endpoints
│       ├── Data/                  # EF Core 8 + SQL Server migrations
│       ├── Models/                # ScannerModels, PortfolioItem, Dtos
│       └── Services/              # YahooFinanceService, PortfolioService, RsiScannerService
├── frontend/
│   └── portfolio-manager-ui/      # Angular 22 SPA (port 4200)
│       └── src/app/
│           ├── core/              # models, API service, state services
│           └── features/          # dashboard, market-header, rsi-scanner, stock-card
├── database/
│   ├── 01_CreateDatabase.sql
│   ├── 02_CreateTables.sql
│   ├── 03_SeedData.sql
│   └── 04_DropAll.sql
└── .github/workflows/ci.yml       # CI: build + lint + security audit
```

---

## Quick Start (local)

### Prerequisites

| Tool       | Version            |
| ---------- | ------------------ |
| .NET SDK   | 8.0+               |
| Node.js    | 22.x LTS           |
| SQL Server | 2019+ (Express OK) |

### 1 — Database setup

```sql
-- Run in order against your SQL Server instance:
database/01_CreateDatabase.sql
database/02_CreateTables.sql
database/03_SeedData.sql      -- optional: seeds 5 demo positions
```

### 2 — Backend

```powershell
cd backend\PortfolioManager.Api
dotnet run --launch-profile http
# API available at http://localhost:5000/swagger
```

### 3 — Frontend

```powershell
cd frontend\portfolio-manager-ui
npm install
npx ng serve
# UI available at http://localhost:4200
```

### One-click launch

```cmd
start-all.bat     # kills existing processes, starts both in separate windows
```

---

## API Endpoints

| Method | Path                    | Description                              |
| ------ | ----------------------- | ---------------------------------------- |
| GET    | `/api/scanner/rsi`      | RSI scan with 5 indicators + probability |
| GET    | `/api/portfolio`        | All portfolio positions                  |
| POST   | `/api/portfolio`        | Add position                             |
| PUT    | `/api/portfolio/{id}`   | Update position                          |
| DELETE | `/api/portfolio/{id}`   | Remove position                          |
| GET    | `/api/stocks/quotes`    | Live quotes for all positions            |
| GET    | `/api/stocks/search?q=` | Yahoo Finance symbol search              |

---

## Environment Configuration

**Never commit connection strings.** Use:

| Secret         | Where to set                                                            |
| -------------- | ----------------------------------------------------------------------- |
| SQL connection | `appsettings.json` locally; GitHub Secret `SQL_CONNECTION_STRING` in CI |

---

## GitHub Branch Strategy

| Branch      | Purpose                                     |
| ----------- | ------------------------------------------- |
| `main`      | Production-ready, protected. PR required.   |
| `develop`   | Integration branch for features.            |
| `feature/*` | Short-lived feature branches off `develop`. |

---

## CI/CD

GitHub Actions (`.github/workflows/ci.yml`) runs on every push/PR to `main` and `develop`:

1. **.NET build** — `dotnet build --configuration Release`
2. **Angular production build** — `ng build --configuration production`
3. **Angular lint** — `ng lint`
4. **npm audit** — high-severity CVE check
5. **NuGet vulnerability check** — `dotnet list package --vulnerable`
6. **Artifacts uploaded** — `api-drop` and `angular-drop` (7-day retention)

---

## QA Checklist

See [QA Steps](#steps-before-qa) section below for the full pre-QA handoff checklist.

            └── features/          # Dashboard, StockCard, AddStockDialog

````

---

## Prerequisites

| Tool        | Version                        |
| ----------- | ------------------------------ |
| .NET SDK    | 8.x                            |
| Node.js     | 22.x LTS                       |
| SQL Server  | local (Express or full)        |
| Angular CLI | 22 (installed locally via npx) |

---

## ⚙️ Backend Setup

### 1. Apply database migrations (auto-runs on first start in Development)

```powershell
dotnet ef database update
```

Or just run the app — it calls `MigrateAsync()` on startup in Development mode, creating `PortfolioManagerDb` automatically.

### 3. Run the backend

```powershell
cd backend/PortfolioManager.Api
dotnet run
```

API runs at **http://localhost:5000**
Swagger UI: **http://localhost:5000/swagger**

---

## 🖥️ Frontend Setup

```powershell
cd frontend/portfolio-manager-ui
npm install
npx ng serve
```

Angular dev server runs at **http://localhost:4200**
All `/api` requests are proxied to the backend at `http://localhost:5000`.

---

## Running Both Together

Open two terminals:

**Terminal 1 – Backend:**

```powershell
cd backend/PortfolioManager.Api
dotnet run
```

**Terminal 2 – Frontend:**

```powershell
cd frontend/portfolio-manager-ui
npx ng serve
```

Then open **http://localhost:4200**.

---

## Features

- **Dashboard** with a live portfolio summary bar (total value, cost, gain/loss %)
- **Stock cards** showing live price, change, day range, shares, market value, P&L
- **Add Stock dialog** with Yahoo Finance symbol search autocomplete (Material Design)
- **30-second polling** for near-real-time price refresh
- **Delete** a position directly from the card
- All data persisted in SQL Server via EF Core

---

## API Endpoints

| Method | Path                         | Description                    |
| ------ | ---------------------------- | ------------------------------ |
| GET    | `/api/portfolio`             | List all portfolio items       |
| POST   | `/api/portfolio`             | Add a stock                    |
| PUT    | `/api/portfolio/{id}`        | Update shares/cost             |
| DELETE | `/api/portfolio/{id}`        | Remove a stock                 |
| GET    | `/api/stocks/quotes`         | All positions with live quotes |
| GET    | `/api/stocks/quote/{symbol}` | Single live quote              |
| GET    | `/api/stocks/search?q=`      | Symbol search via Yahoo Finance |

---

## Security Notes

- API key stored in `.NET User Secrets` (development) — never in `appsettings.json` (not currently required — Yahoo Finance needs no key)
- For **production**: use Azure Key Vault, AWS Secrets Manager, or environment variables
- CORS is locked to `localhost:4200` in development
- EF Core parameterized queries prevent SQL injection
- No sensitive data stored in browser localStorage
````
