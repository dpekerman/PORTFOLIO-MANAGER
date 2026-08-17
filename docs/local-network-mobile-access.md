# Local Network Mobile Access Guide

## How to View Portfolio Manager on Mobile / Tablet Without Publishing to App Store

**Summary:** Run the app on your Windows PC and access it from any phone or tablet on the same Wi-Fi network using a browser. No deployment, no app store needed.

---

## Overview

Your Portfolio Manager is a web application (Angular frontend + .NET backend). Any device with a browser on the same local Wi-Fi can access it — phone, tablet, laptop. You never need to publish to an app store.

```
Your PC (Windows)
  └── Backend:  http://YOUR_PC_IP:5000  (ASP.NET Core API)
  └── Frontend: http://YOUR_PC_IP:4200  (Angular dev server)
        ↕  same Wi-Fi
Phone / Tablet (Safari, Chrome, Edge)
  └── Opens:    http://YOUR_PC_IP:4200
```

---

## Step 1 — Find Your PC's Local IP Address

Open PowerShell or Command Prompt:

```powershell
ipconfig
```

Look for **IPv4 Address** under your Wi-Fi adapter, e.g.:

```
Wireless LAN adapter Wi-Fi:
   IPv4 Address. . . . . . . . . . . : 192.168.1.45
```

Write this down. Yours will be in the `192.168.x.x` or `10.0.x.x` range.

---

## Step 2 — Configure the Backend to Accept Connections from Other Devices

By default, the backend only listens on `localhost`. You need to tell it to listen on your local IP or all interfaces.

### Option A — Run with `--urls` flag (easiest, no file changes)

```powershell
cd d:\PORTFOLIO-MANAGER\backend\PortfolioManager.Api
dotnet run --urls "http://0.0.0.0:5000"
```

The `0.0.0.0` means "accept connections from all network interfaces". Your phone will reach the API at `http://192.168.1.45:5000`.

### Option B — Edit `launchSettings.json` permanently

In `backend\PortfolioManager.Api\Properties\launchSettings.json`, change:

```json
"applicationUrl": "http://localhost:5000"
```

To:

```json
"applicationUrl": "http://0.0.0.0:5000"
```

Then run normally: `dotnet run`

> **Security note:** `0.0.0.0` makes the API accessible to anyone on your local network. This is fine on a home network. Do NOT use this on a public/corporate Wi-Fi without a firewall.

---

## Step 3 — Configure the Angular Frontend to Accept Remote Connections

The Angular dev server also defaults to `localhost`. You need to allow external connections.

### Edit `angular.json` to set the host:

In `frontend\portfolio-manager-ui\angular.json`, find the `"serve"` section:

```json
"serve": {
  "builder": "@angular-devkit/build-angular:dev-server",
  "options": {
    ...
  }
}
```

Add the `host` option:

```json
"serve": {
  "builder": "@angular-devkit/build-angular:dev-server",
  "options": {
    "host": "0.0.0.0",
    "proxyConfig": "proxy.conf.json"
  }
}
```

### Update `proxy.conf.json` to point to your PC's IP:

Change the proxy target from `localhost` to your PC's IP so the Angular dev server can reach the backend:

```json
{
  "/api": {
    "target": "http://192.168.1.45:5000",
    "secure": false,
    "changeOrigin": true,
    "logLevel": "info"
  }
}
```

Replace `192.168.1.45` with your actual PC IP address.

### Start the Angular dev server:

```powershell
cd d:\PORTFOLIO-MANAGER\frontend\portfolio-manager-ui
npx ng serve --host 0.0.0.0
```

Or use `start-frontend.bat` after making the changes above.

---

## Step 4 — Allow Through Windows Firewall

Windows Firewall may block incoming connections on ports 4200 and 5000. Run these commands in an **Administrator** PowerShell:

```powershell
# Allow Angular frontend (port 4200)
New-NetFirewallRule -DisplayName "Portfolio Manager - Frontend" `
  -Direction Inbound -Protocol TCP -LocalPort 4200 -Action Allow

# Allow .NET backend (port 5000)
New-NetFirewallRule -DisplayName "Portfolio Manager - Backend" `
  -Direction Inbound -Protocol TCP -LocalPort 5000 -Action Allow
```

To remove these rules later:

```powershell
Remove-NetFirewallRule -DisplayName "Portfolio Manager - Frontend"
Remove-NetFirewallRule -DisplayName "Portfolio Manager - Backend"
```

---

## Step 5 — Open on Your Mobile Device

1. Make sure your phone/tablet is on the **same Wi-Fi network** as your PC
2. Open any browser (Safari, Chrome, Edge, Firefox)
3. Navigate to: `http://192.168.1.45:4200` (replace with your actual IP)
4. The Portfolio Manager UI will load — no installation required

> **Bookmark it** in your mobile browser for quick access. On iOS Safari: tap Share → Add to Home Screen. On Android Chrome: tap menu → Add to Home Screen. This creates an icon that opens the app in full-screen mode — it looks and feels like a native app.

---

## Step 6 — Recommended: Use the Production Build Instead of Dev Server

The Angular dev server is fine for testing but is slightly slower on mobile. For a better experience, serve the **production build** instead:

### Build once:

```powershell
cd d:\PORTFOLIO-MANAGER\frontend\portfolio-manager-ui
npx ng build --configuration production
```

### Serve the built files with a simple HTTP server:

```powershell
# Install serve (one-time)
npm install -g serve

# Serve the build output on all interfaces
serve -s dist/portfolio-manager-ui/browser -l 4200 --no-clipboard
```

Now navigate to `http://192.168.1.45:4200` from your phone. The production build is much faster.

---

## Start Everything — Quick Reference Script

Create a `start-all-local.bat` in the project root:

```batch
@echo off
echo Starting Portfolio Manager for local network access...

echo.
echo [1/2] Starting backend on all interfaces (port 5000)...
start "Backend" cmd /k "cd /d d:\PORTFOLIO-MANAGER\backend\PortfolioManager.Api && dotnet run --urls http://0.0.0.0:5000"

timeout /t 4 /nobreak > nul

echo [2/2] Starting Angular frontend for local network access (port 4200)...
start "Frontend" cmd /k "cd /d d:\PORTFOLIO-MANAGER\frontend\portfolio-manager-ui && npx ng serve --host 0.0.0.0"

echo.
echo =====================================================================
echo  Access from your mobile device at:
echo  http://YOUR_PC_IP:4200
echo  (Run ipconfig in another terminal to find your PC IP)
echo =====================================================================
pause
```

---

## Troubleshooting

| Problem                           | Solution                                                                      |
| --------------------------------- | ----------------------------------------------------------------------------- |
| Phone can't connect               | Verify both devices are on the same Wi-Fi. Run `ipconfig` to confirm PC IP.   |
| "Connection refused" on port 4200 | Angular server not running, or firewall blocking. Check firewall rules above. |
| "Connection refused" on port 5000 | Backend not running with `--urls http://0.0.0.0:5000`.                        |
| App loads but API calls fail      | `proxy.conf.json` still points to `localhost`. Update to your PC IP.          |
| Slow on mobile                    | Use production build + `serve` instead of `ng serve`.                         |
| IP changes between sessions       | Set a static local IP for your PC in your router's DHCP settings.             |

---

## Mobile-Specific Notes

### iOS (iPhone / iPad)

- Safari works best
- Add to Home Screen: opens as full-screen progressive web app
- If tables are hard to read: use landscape orientation, or pinch-zoom

### Android

- Chrome works best
- Add to Home Screen: tap ⋮ → "Add to Home Screen"
- Portrait/landscape both supported

### Tablet (iPad / Android)

- The app renders well at 768px+ width (most tables fully visible)
- Landscape mode is recommended for the RSI Scanner and Portfolio grid views

---

## Optional: Make This Permanent with a Static IP

To avoid updating the IP every time:

1. Open your router admin page (usually `http://192.168.1.1`)
2. Find "DHCP Reservation" or "Static IP"
3. Find your PC's MAC address: `ipconfig /all` → look for "Physical Address"
4. Assign the same IP permanently to your PC's MAC address
5. Update `proxy.conf.json` once with that fixed IP — never needs changing again

---

## Security Reminder

This setup is for **home/personal use on a trusted local network only**.

- Do NOT use `0.0.0.0` binding on public Wi-Fi (coffee shop, office, hotel)
- The app has no authentication — anyone on your network can view it
- For secure remote access (outside home), consider a VPN or Tailscale instead
