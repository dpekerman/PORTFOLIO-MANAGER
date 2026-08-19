# Azure Setup Checklist — Follow This Step by Step

Check off each item as you complete it.  
**Everything in this file is a manual step you do in the Azure portal or your terminal.**

---

## Before You Start

- [ ] You have a Microsoft account (Outlook, Hotmail, or work account)
- [ ] You have a credit card ready (required for Azure signup — SQL DB will be $0, App Service ~$18 CAD/month)
- [ ] You have your Gmail App Password ready (Google Account → Security → 2-Step Verification → App passwords)
  - If you don't have one: go to [myaccount.google.com/apppasswords](https://myaccount.google.com/apppasswords), create one named "Portfolio Manager"
- [ ] Open a text file (Notepad) to save values as you go — you will need them later

---

## Step 1 — Create Azure Account / Sign In

- [ ] Go to [portal.azure.com](https://portal.azure.com)
- [ ] Sign in with your Microsoft account
- [ ] If you don't have an Azure subscription:
  - Go to [azure.microsoft.com/en-ca/pricing/purchase-options/azure-account](https://azure.microsoft.com/en-ca/pricing/purchase-options/azure-account)
  - Click **Pay as you go** (not "Start free" — the free 12-month trial limits some services)
  - Complete signup with credit card

---

## Step 2 — Generate Your JWT Secret Now (do this before Azure)

Open PowerShell on your local machine and run:

```powershell
[Convert]::ToBase64String((1..48 | ForEach-Object { [byte](Get-Random -Max 256) }))
```

- [ ] Copy the output (looks like `abc123...` ~64 characters)
- [ ] **Save it in your Notepad** — label it `JWT_SECRET`

---

## Step 3 — Create Resource Group

1. In the Azure portal search bar at the top, type **Resource groups** and click it
2. Click **+ Create**
3. Fill in:
   - **Subscription** — leave as-is (your subscription)
   - **Resource group name** — `rg-portfolio-manager`
   - **Region** — `Canada Central`
4. Click **Review + create** → **Create**
5. Wait for "Your deployment is complete" (~10 seconds)

- [ ] ✅ `rg-portfolio-manager` created

---

## Step 4 — Create Azure SQL Database (Free — $0/month)

> ⚠️ Must use the special "Start free" link — the normal SQL create flow does NOT offer the free tier

1. Go to **[aka.ms/azuresqlhub](https://aka.ms/azuresqlhub)**
2. In the right panel "Create a database", click **Start free**
3. ✅ Confirm you see a green **"Free offer applied!"** banner at the top of the create page
   - If you don't see it — stop, close the page, and try the link again
4. Fill in:
   - **Resource group** → `rg-portfolio-manager`
   - **Database name** → `PortfolioManagerDb`
   - **Server** → click **Create new**:
     - **Server name** → `portfolio-sql-dpekerman` *(add your name to make it globally unique)*
     - **Location** → `Canada Central`
     - **Authentication** → SQL authentication
     - **Admin login** → `portfolioadmin`
     - **Password** → create a strong password *(upper + lower + number + symbol, 12+ chars)*
     - [ ] **Save this password in Notepad** — label it `SQL_PASSWORD`
     - Click **OK**
5. **Behavior when free limit reached** → select **Auto-pause the database until next month**
6. Click **Review + create** → **Create**
7. Wait ~2–3 minutes for deployment

**After deployment completes:**

8. Click **Go to resource** (the database)
9. In the left menu → **Settings → Connection strings**
10. Click the **ADO.NET** tab
11. Copy the full connection string — it looks like:
    ```
    Server=tcp:portfolio-sql-dpekerman.database.windows.net,1433;Initial Catalog=PortfolioManagerDb;Persist Security Info=False;User ID=portfolioadmin;Password={your_password};MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
    ```
12. Replace `{your_password}` with your actual SQL password from above
13. [ ] **Save the full connection string in Notepad** — label it `SQL_CONNECTION_STRING`

**Allow connections:**

14. In the left menu, click on the **server name** link near the top (under "Server name" field) — this opens the SQL Server (not the database)
15. Left menu → **Security → Networking**
16. Toggle **"Allow Azure services and resources to access this server"** → **ON**
17. Click **"+ Add your client IPv4 address"** *(allows your local machine to run EF migrations)*
18. Click **Save**

- [ ] ✅ SQL Database created
- [ ] ✅ Connection string saved
- [ ] ✅ Firewall rules set

---

## Step 5 — Create App Service Plan

1. In the search bar type **App Service plans** → click it
2. Click **+ Create**
3. Fill in:
   - **Resource group** → `rg-portfolio-manager`
   - **Name** → `portfolio-asp`
   - **Operating System** → **Linux**
   - **Region** → `Canada Central`
   - **Pricing plan** → click **Explore pricing plans** → select **B1** under Basic → click **Select**
4. Click **Review + create** → **Create**

- [ ] ✅ App Service Plan B1 Linux created

---

## Step 6 — Create Web App (the .NET API backend)

1. In the search bar type **App Services** → click it
2. Click **+ Create** → **Web App**
3. Fill in:
   - **Resource group** → `rg-portfolio-manager`
   - **Name** → `portfolio-manager` *(this becomes `portfolio-manager.azurewebsites.net`)*
     - If `portfolio-manager` is already taken, try `portfolio-manager-dp` and tell me — I'll update the code
   - **Publish** → **Code**
   - **Runtime stack** → **.NET 8 (LTS)**
   - **Operating System** → **Linux**
   - **Region** → `Canada Central`
   - **Linux Plan** → select `portfolio-asp (B1)` from the dropdown
4. Click **Review + create** → **Create**
5. Wait ~1 minute

- [ ] ✅ App Service `portfolio-manager` created

---

## Step 7 — Create Static Web App (the Angular frontend)

1. In the search bar type **Static Web Apps** → click it
2. Click **+ Create**
3. Fill in:
   - **Resource group** → `rg-portfolio-manager`
   - **Name** → `portfolio-ui`
   - **Plan type** → **Free**
   - **Region** → `East US 2`
   - **Deployment details → Source** → **GitHub**
   - Click **Sign in with GitHub** → authorize Azure to access your repos
   - **Organization** → `dpekerman`
   - **Repository** → `PORTFOLIO-MANAGER`
   - **Branch** → `main`
   - **Build presets** → **Angular**
   - **App location** → `frontend/portfolio-manager-ui`
   - **Api location** → *(leave blank)*
   - **Output location** → `dist/portfolio-manager-ui/browser`
4. Click **Review + create** → **Create**
5. Wait ~1 minute
6. Click **Go to resource**
7. On the **Overview** page, copy the **URL** (e.g. `https://purple-wave-abc123.azurestaticapps.net`)
8. [ ] **Save this URL in Notepad** — label it `STATIC_WEB_APP_URL`
9. Also on the Overview page, click **Manage deployment token**
10. Copy the token
11. [ ] **Save it in Notepad** — label it `AZURE_STATIC_WEB_APPS_API_TOKEN`

> ⚠️ **Important:** Azure auto-added a workflow `.yml` file to your GitHub repo.
> Tell me what it is named (check [github.com/dpekerman/PORTFOLIO-MANAGER/tree/main/.github/workflows](https://github.com/dpekerman/PORTFOLIO-MANAGER/tree/main/.github/workflows))
> and I will delete it — your `cd.yml` already handles deployment.

- [ ] ✅ Static Web App created
- [ ] ✅ URL saved
- [ ] ✅ Deployment token saved

---

## Step 8 — Configure App Service Settings (the 13 environment variables)

1. Go to your App Service → left menu → **Settings → Configuration**
2. Click the **Application settings** tab
3. For each row below, click **+ New application setting**, enter the Name and Value, click **OK**

| # | Name | Value |
|---|---|---|
| 1 | `ASPNETCORE_ENVIRONMENT` | `Production` |
| 2 | `ConnectionStrings__DefaultConnection` | *(SQL_CONNECTION_STRING from Step 4)* |
| 3 | `Jwt__Secret` | *(JWT_SECRET from Step 2)* |
| 4 | `Jwt__Issuer` | `PortfolioManager` |
| 5 | `Jwt__Audience` | `PortfolioManagerClient` |
| 6 | `CorsOrigin` | *(STATIC_WEB_APP_URL from Step 7 — no trailing slash)* |
| 7 | `EmailNotification__Enabled` | `true` |
| 8 | `EmailNotification__SmtpHost` | `smtp.gmail.com` |
| 9 | `EmailNotification__SmtpPort` | `587` |
| 10 | `EmailNotification__UseStartTls` | `true` |
| 11 | `EmailNotification__Username` | `dima.pekerman@gmail.com` |
| 12 | `EmailNotification__Password` | *(your Gmail App Password — 16 characters)* |
| 13 | `EmailNotification__FromAddress` | `dima.pekerman@gmail.com` |

4. After all 13 are added → click **Save** at the top → click **Continue**

- [ ] ✅ All 13 App Service settings saved

---

## Step 9 — Add GitHub Secrets (enables the CD deployment)

Go to [github.com/dpekerman/PORTFOLIO-MANAGER/settings/secrets/actions](https://github.com/dpekerman/PORTFOLIO-MANAGER/settings/secrets/actions)

### Secret 1: App Service publish profile

1. In Azure portal → go to your App Service `portfolio-manager`
2. Click **Get publish profile** button at the top of the Overview page
3. This downloads a `.PublishSettings` XML file — open it in Notepad, select all, copy
4. Back in GitHub → click **New repository secret**
   - **Name** → `AZURE_WEBAPP_PUBLISH_PROFILE`
   - **Secret** → paste the full XML
   - Click **Add secret**

- [ ] ✅ `AZURE_WEBAPP_PUBLISH_PROFILE` added

### Secret 2: Static Web App token

1. Back in GitHub → click **New repository secret**
   - **Name** → `AZURE_STATIC_WEB_APPS_API_TOKEN`
   - **Secret** → paste the token saved in Step 7
   - Click **Add secret**

- [ ] ✅ `AZURE_STATIC_WEB_APPS_API_TOKEN` added

---

## Step 10 — Run EF Migrations Against Azure SQL (one-time, from your local machine)

Open PowerShell in VS Code terminal:

```powershell
cd D:\PORTFOLIO-MANAGER\backend\PortfolioManager.Api

dotnet ef database update --connection "YOUR_SQL_CONNECTION_STRING_HERE"
```

Replace `YOUR_SQL_CONNECTION_STRING_HERE` with your full connection string from Step 4.

Expected output: a list of migration names being applied, ending with `Done.`

This creates all tables in Azure SQL:
- ASP.NET Identity tables (users, roles, tokens)
- All 20+ business tables (portfolio, watchlist, signals, etc.)

- [ ] ✅ EF migrations applied to Azure SQL

---

## Step 11 — Delete the Auto-Generated Azure Workflow File

After Step 7, Azure added an extra `.yml` file to your repo in `.github/workflows/`.  
Tell me its filename and I'll delete it for you.

Or delete it yourself:
1. Go to [github.com/dpekerman/PORTFOLIO-MANAGER/tree/main/.github/workflows](https://github.com/dpekerman/PORTFOLIO-MANAGER/tree/main/.github/workflows)
2. Find any file that is NOT `ci.yml` or `cd.yml`
3. Open it → click the trash icon → commit the deletion

- [ ] ✅ Extra workflow file deleted

---

## Step 12 — First Deployment

Push to `main` to trigger the CD workflow:

```powershell
cd D:\PORTFOLIO-MANAGER
git checkout main
git push origin main
```

Or — since `main` is already up to date — make any trivial commit:

```powershell
cd D:\PORTFOLIO-MANAGER
git checkout develop
git checkout main
git commit --allow-empty -m "chore: trigger first Azure deployment"
git push origin main
```

- [ ] Go to [github.com/dpekerman/PORTFOLIO-MANAGER/actions](https://github.com/dpekerman/PORTFOLIO-MANAGER/actions)
- [ ] Watch the **CD – Deploy to Azure** workflow run — both jobs should go green (~3–5 min)

- [ ] ✅ First deployment succeeded

---

## Step 13 — First Login Setup (Admin Account Creation)

1. Open your Static Web App URL in the browser (e.g. `https://purple-wave-abc123.azurestaticapps.net`)
2. You should be redirected to `/setup`
3. Fill in:
   - **Display Name** → `Dmitry`
   - **Email** → `dima.pekerman@gmail.com`
   - **Password** → your chosen admin password (8+ chars, upper + lower + digit)
4. Click **Setup** — you are now logged in as Admin
5. Go to **Settings → Users** and create the 2 additional users

- [ ] ✅ Admin account created
- [ ] ✅ Additional users created

---

## Step 14 — Verification

| Check | How | Expected result |
|---|---|---|
| App loads | Open Static Web App URL | Redirected to `/login` |
| Auth works | Log in with admin credentials | Dashboard loads |
| API protected | Open `https://portfolio-manager.azurewebsites.net/api/portfolio` in browser | `401 Unauthorized` |
| Swagger disabled | Open `https://portfolio-manager.azurewebsites.net/swagger` in browser | `404 Not Found` |
| Background services | Azure portal → App Service → **Log stream** | RSI scan log lines appear every 60s |
| Budget alert | Azure portal → Cost Management → Budgets → create alert at $25 CAD | Alert created |

- [ ] ✅ All verification checks passed

---

## 🎉 Done — You're Live on Azure

**Monthly cost:** ~$18–22 CAD (App Service B1 only — SQL and Static Web App are free)

**For future deployments:**
```powershell
git checkout main
git merge develop --no-edit
git push origin main   # triggers CD automatically
```

See [development-workflow.md](development-workflow.md) for the full ongoing workflow.
