using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Options;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

/// <summary>
/// Sends HTML RSI signal alert emails via SMTP whenever new CONFIRMED signals are detected.
/// Email is marked as high priority and contains a full technical details table.
/// </summary>
public class EmailNotificationService(
    IOptions<EmailSettings> settings,
    NotificationRecipientsService recipients,
    SignalNotificationTracker tracker,
    ILogger<EmailNotificationService> logger)
{
    private readonly EmailSettings _settings = settings.Value;

    /// <summary>
    /// Checks the scan result for new CONFIRMED signals.
    /// If any new signals exist AND email is configured, sends ONE high-priority email.
    /// </summary>
    public async Task NotifyNewConfirmedSignalsAsync(ScannerResponse scanResult)
    {
        // Merge both chains for tracking
        var allResults = (scanResult.OversoldChain ?? [])
            .Concat(scanResult.OverboughtChain ?? [])
            .ToList();

        var newlyConfirmed = tracker.GetNewlyConfirmedAndSync(allResults);

        if (newlyConfirmed.Count == 0) return;

        var recipientList = recipients.GetAll();
        if (recipientList.Count == 0)
        {
            logger.LogDebug("{Count} new confirmed signal(s) detected, but no email recipients configured.", newlyConfirmed.Count);
            return;
        }

        if (!_settings.Enabled)
        {
            logger.LogInformation("{Count} new confirmed signal(s) — email disabled in settings (EmailNotification.Enabled = false).", newlyConfirmed.Count);
            return;
        }

        if (string.IsNullOrWhiteSpace(_settings.Username) || string.IsNullOrWhiteSpace(_settings.Password))
        {
            logger.LogWarning("Email credentials not configured. Skipping notification for {Count} signal(s).", newlyConfirmed.Count);
            return;
        }

        try
        {
            await SendAlertEmailAsync(newlyConfirmed, recipientList, scanResult.ScannedAt);
            logger.LogInformation("Alert email sent for {SignalCount} confirmed signal(s) to {RecipientCount} recipient(s).",
                newlyConfirmed.Count, recipientList.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send RSI alert email.");
        }
    }

    /// <summary>
    /// Sends a plain test email to <paramref name="toEmail"/> to verify SMTP configuration.
    /// Returns null on success, or the error message on failure.
    /// </summary>
    public async Task<string?> SendTestEmailAsync(string toEmail)
    {
        if (!_settings.Enabled)
            return "Email is disabled in settings (EmailNotification.Enabled = false).";

        if (string.IsNullOrWhiteSpace(_settings.Username) || string.IsNullOrWhiteSpace(_settings.Password)
            || _settings.Password == "REPLACE_WITH_GMAIL_APP_PASSWORD")
            return "SMTP credentials are not configured. Set Username and Password in appsettings.json.";

        try
        {
            using var client = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort)
            {
                EnableSsl = _settings.UseStartTls,
                Credentials = new NetworkCredential(_settings.Username, _settings.Password),
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = 20_000,
            };

            var from = !string.IsNullOrWhiteSpace(_settings.FromAddress) ? _settings.FromAddress : _settings.Username;

            using var message = new MailMessage
            {
                From = new MailAddress(from, _settings.FromName),
                Subject = "✅ Portfolio Manager — SMTP Test",
                Body = $@"<!DOCTYPE html>
<html><head><meta charset=""UTF-8""></head>
<body style=""background:#0d0d0d;color:#e0e0e0;font-family:'Segoe UI',Arial,sans-serif;padding:32px"">
  <div style=""max-width:520px;margin:0 auto;background:#1a1a2e;border-radius:12px;padding:32px;border:1px solid #2a2a4a"">
    <h2 style=""color:#4fc3f7;margin:0 0 16px"">✅ SMTP Configuration Verified</h2>
    <p>Your <strong>Portfolio Manager</strong> email notifications are working correctly.</p>
    <p style=""color:#aaa;font-size:0.9rem"">
      Sent via: <code style=""background:#111;padding:2px 6px;border-radius:4px"">{_settings.SmtpHost}:{_settings.SmtpPort}</code><br>
      From: <code style=""background:#111;padding:2px 6px;border-radius:4px"">{from}</code><br>
      Time: <code style=""background:#111;padding:2px 6px;border-radius:4px"">{DateTime.UtcNow:u}</code>
    </p>
    <p style=""color:#81c784"">You will receive alerts like this whenever a new CONFIRMED RSI signal is detected.</p>
  </div>
</body></html>",
                IsBodyHtml = true,
                Priority = MailPriority.Normal,
            };

            message.To.Add(toEmail);
            await client.SendMailAsync(message);
            logger.LogInformation("Test email sent successfully to {Email}.", toEmail);
            return null; // success
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Test email failed.");
            return ex.InnerException?.Message ?? ex.Message;
        }
    }

    private async Task SendAlertEmailAsync(
        List<RsiScanResult> signals,
        List<string> recipientEmails,
        DateTime scannedAt)
    {
        using var client = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort)
        {
            EnableSsl = _settings.UseStartTls,
            Credentials = new NetworkCredential(_settings.Username, _settings.Password),
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 15_000,
        };

        var oversold  = signals.Where(s => s.ScanType == ScanType.Oversold).ToList();
        var overbought = signals.Where(s => s.ScanType == ScanType.Overbought).ToList();

        var subject = $"⚠️ RSI ALERT — {signals.Count} Confirmed Signal{(signals.Count > 1 ? "s" : "")} Detected";
        var body = BuildHtmlBody(oversold, overbought, scannedAt);

        using var message = new MailMessage
        {
            From = new MailAddress(
                !string.IsNullOrWhiteSpace(_settings.FromAddress) ? _settings.FromAddress : _settings.Username,
                _settings.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = true,
            Priority = MailPriority.High,
        };

        // High-priority headers used by Outlook / Gmail
        message.Headers.Add("X-Priority", "1");
        message.Headers.Add("X-MSMail-Priority", "High");
        message.Headers.Add("Importance", "High");

        foreach (var email in recipientEmails)
            message.To.Add(email);

        await client.SendMailAsync(message);
    }

    private static string BuildHtmlBody(
        List<RsiScanResult> oversold,
        List<RsiScanResult> overbought,
        DateTime scannedAt)
    {
        var sb = new StringBuilder();
        sb.AppendLine(@"<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""UTF-8"">
<meta name=""viewport"" content=""width=device-width,initial-scale=1"">
<style>
  body{margin:0;padding:0;background:#0d0d0d;font-family:'Segoe UI',Arial,sans-serif;color:#e0e0e0}
  .wrapper{max-width:800px;margin:0 auto;padding:24px 16px}
  .header{background:linear-gradient(135deg,#0d47a1,#1565c0);border-radius:12px;padding:24px 28px;margin-bottom:24px}
  .header h1{margin:0 0 6px;font-size:1.4rem;color:#fff;letter-spacing:0.05em}
  .header p{margin:0;font-size:0.85rem;color:rgba(255,255,255,0.75)}
  .badge{display:inline-block;padding:3px 12px;border-radius:20px;font-size:0.75rem;font-weight:700;letter-spacing:0.06em}
  .badge-oversold{background:#1b5e20;color:#a5d6a7}
  .badge-overbought{background:#b71c1c;color:#ef9a9a}
  .section-title{font-size:0.8rem;font-weight:700;letter-spacing:0.1em;text-transform:uppercase;margin:0 0 12px;padding-bottom:8px;border-bottom:1px solid #2a2a2a}
  .section-title.os{color:#66bb6a}
  .section-title.ob{color:#ef5350}
  table{width:100%;border-collapse:collapse;margin-bottom:24px;background:#161616;border-radius:10px;overflow:hidden}
  th{background:#1e1e1e;padding:10px 14px;text-align:left;font-size:0.72rem;font-weight:700;letter-spacing:0.08em;color:#9e9e9e;text-transform:uppercase;border-bottom:1px solid #2a2a2a}
  td{padding:12px 14px;font-size:0.82rem;border-bottom:1px solid #1a1a1a;vertical-align:top}
  tr:last-child td{border-bottom:none}
  .sym{font-weight:700;font-size:0.9rem;color:#fff}
  .co{font-size:0.73rem;color:#9e9e9e;margin-top:2px}
  .rsi-os{color:#ef5350;font-weight:700;font-size:1rem}
  .rsi-ob{color:#ffa726;font-weight:700;font-size:1rem}
  .pos{color:#66bb6a}.neg{color:#ef5350}
  .pill{display:inline-block;padding:2px 8px;border-radius:12px;font-size:0.7rem;font-weight:600;margin:1px}
  .pill-bull{background:rgba(102,187,106,0.15);color:#66bb6a;border:1px solid rgba(102,187,106,0.3)}
  .pill-bear{background:rgba(239,83,80,0.15);color:#ef5350;border:1px solid rgba(239,83,80,0.3)}
  .pill-neu{background:rgba(158,158,158,0.15);color:#9e9e9e;border:1px solid rgba(158,158,158,0.3)}
  .trigger{font-size:0.77rem;color:#bdbdbd;max-width:220px}
  .footer{margin-top:28px;padding-top:16px;border-top:1px solid #2a2a2a;font-size:0.72rem;color:#616161;text-align:center}
  .prob-high{color:#66bb6a;font-weight:700}
  .prob-med{color:#ffa726;font-weight:700}
  .prob-low{color:#9e9e9e}
</style>
</head>
<body>
<div class=""wrapper"">");

        sb.AppendLine($@"  <div class=""header"">
    <h1>⚠️ RSI Confirmed Signal Alert</h1>
    <p>Portfolio Manager detected <strong>{oversold.Count + overbought.Count} new confirmed signal(s)</strong>
       on {scannedAt:dddd, MMMM d yyyy} at {scannedAt:HH:mm} UTC. Action may be required.</p>
  </div>");

        if (oversold.Count > 0)
        {
            sb.AppendLine(@"  <p class=""section-title os"">🟢 Oversold Confirmed Signals — Potential Buy Opportunity</p>");
            sb.AppendLine(BuildSignalTable(oversold, ScanType.Oversold));
        }

        if (overbought.Count > 0)
        {
            sb.AppendLine(@"  <p class=""section-title ob"">🔴 Overbought Confirmed Signals — Potential Sell / Caution</p>");
            sb.AppendLine(BuildSignalTable(overbought, ScanType.Overbought));
        }

        sb.AppendLine($@"  <div class=""footer"">
    <p>This alert was generated automatically by <strong>Portfolio Manager</strong>.<br>
    Scanned at {scannedAt:yyyy-MM-dd HH:mm:ss} UTC &nbsp;·&nbsp; This is not financial advice.<br>
    Always do your own research before making investment decisions.</p>
  </div>
</div>
</body>
</html>");

        return sb.ToString();
    }

    private static string BuildSignalTable(List<RsiScanResult> signals, ScanType type)
    {
        var sb = new StringBuilder();
        sb.AppendLine(@"  <table>
    <thead>
      <tr>
        <th>Ticker</th>
        <th>RSI (14)</th>
        <th>Price</th>
        <th>Change</th>
        <th>Key Indicators</th>
        <th>Probability</th>
        <th>Trigger / Analysis</th>
      </tr>
    </thead>
    <tbody>");

        foreach (var r in signals)
        {
            var rsiClass = type == ScanType.Oversold ? "rsi-os" : "rsi-ob";
            var changeClass = r.ChangePercent >= 0 ? "pos" : "neg";
            var changeSign = r.ChangePercent >= 0 ? "+" : "";

            var macdPill = r.MacdCrossover switch
            {
                "Bullish" => "<span class=\"pill pill-bull\">↑ MACD Bull</span>",
                "Bearish" => "<span class=\"pill pill-bear\">↓ MACD Bear</span>",
                _ => "<span class=\"pill pill-neu\">→ MACD Flat</span>"
            };
            var stochPill = r.StochasticsConfirm
                ? $"<span class=\"pill {(type == ScanType.Oversold ? "pill-bull" : "pill-bear")}\">Stoch {r.StochasticK:F0}</span>"
                : $"<span class=\"pill pill-neu\">Stoch {r.StochasticK:F0}</span>";
            var bbPill = $"<span class=\"pill {(r.BollingerBreakout ? (type == ScanType.Oversold ? "pill-bull" : "pill-bear") : "pill-neu")}\">BB {r.BollingerPosition}</span>";
            var volPill = r.VolumeSignal == "Validated"
                ? "<span class=\"pill pill-bull\">✓ Volume OK</span>"
                : r.VolumeSignal == "Low-Volume Trap"
                    ? "<span class=\"pill pill-bear\">⚠ Low Vol</span>"
                    : "<span class=\"pill pill-neu\">Vol Neutral</span>";

            var dma = r.Has200Dma
                ? $"<br><small>50D {(r.Dma50Deviation >= 0 ? "+" : "")}{r.Dma50Deviation:F1}% · 200D {(r.Dma200Deviation >= 0 ? "+" : "")}{r.Dma200Deviation:F1}%</small>"
                : $"<br><small>50D {(r.Dma50Deviation >= 0 ? "+" : "")}{r.Dma50Deviation:F1}%</small>";

            var probClass = r.ReversalProbability switch
            {
                "High" => "prob-high",
                "Medium" => "prob-med",
                _ => "prob-low"
            };

            sb.AppendLine($@"      <tr>
        <td><div class=""sym"">{r.Symbol}</div><div class=""co"">{r.CompanyName}</div></td>
        <td><span class=""{rsiClass}"">{r.Rsi:F1}</span></td>
        <td>${r.CurrentPrice:F2}</td>
        <td class=""{changeClass}"">{changeSign}{r.ChangePercent:F2}%</td>
        <td>{stochPill} {macdPill} {bbPill} {volPill}{dma}</td>
        <td><span class=""{probClass}"">{r.ReversalProbability}</span></td>
        <td class=""trigger"">{r.TriggerDetails}</td>
      </tr>");
        }

        sb.AppendLine("    </tbody>\n  </table>");
        return sb.ToString();
    }
}
