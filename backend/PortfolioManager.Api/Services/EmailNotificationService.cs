using System.Net.Mail;
using System.Text;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
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
            await SendViaMailKitAsync(message);
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


        var oversold  = signals.Where(s => s.ScanType == ScanType.Oversold).ToList();
        var overbought = signals.Where(s => s.ScanType == ScanType.Overbought).ToList();

        // Build a compact ticker list for the subject: "RY.TO (79.9), BCE.TO (68.2)"
        var tickerSummary = string.Join(", ", signals.Select(s => $"{s.Symbol} RSI:{s.Rsi:F1}"));
        var subject = signals.Count == 1
            ? $"⚠️ RSI ALERT — {signals[0].Symbol} (RSI {signals[0].Rsi:F1}) Confirmed Signal"
            : $"⚠️ RSI ALERT — {signals.Count} Confirmed Signals: {tickerSummary}";

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

        await SendViaMailKitAsync(message);
    }

    /// <summary>Sends a MailMessage via MailKit — handles Gmail StartTLS correctly on Linux.</summary>
    private async Task SendViaMailKitAsync(MailMessage mailMessage, CancellationToken ct = default)
    {
        var mimeMessage = MimeMessage.CreateFromMailMessage(mailMessage);
        using var client = new MailKit.Net.Smtp.SmtpClient();
        // Port 465 = implicit SSL; port 587 = STARTTLS (Gmail default)
        var options = _settings.SmtpPort == 465
            ? SecureSocketOptions.SslOnConnect
            : SecureSocketOptions.StartTls;
        await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, options, ct);
        await client.AuthenticateAsync(_settings.Username, _settings.Password.Replace(" ", ""), ct);
        await client.SendAsync(mimeMessage, ct);
        await client.DisconnectAsync(true, ct);
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
  body{margin:0;padding:0;background:#f5f5f5;font-family:'Segoe UI',Arial,sans-serif;color:#333}
  .wrapper{max-width:800px;margin:0 auto;padding:24px 16px}
  .header{background:linear-gradient(135deg,#0d47a1,#1565c0);border-radius:12px;padding:24px 28px;margin-bottom:24px}
  .header h1{margin:0 0 6px;font-size:1.4rem;color:#fff;letter-spacing:0.05em}
  .header p{margin:0;font-size:0.85rem;color:rgba(255,255,255,0.85)}
  .badge{display:inline-block;padding:3px 12px;border-radius:20px;font-size:0.75rem;font-weight:700;letter-spacing:0.06em}
  .section-title{font-size:0.8rem;font-weight:700;letter-spacing:0.1em;text-transform:uppercase;margin:0 0 12px;padding-bottom:8px;border-bottom:2px solid #e0e0e0}
  .section-title.os{color:#2e7d32}
  .section-title.ob{color:#c62828}
  table{width:100%;border-collapse:collapse;margin-bottom:24px;background:#fff;border-radius:10px;overflow:hidden;box-shadow:0 1px 4px rgba(0,0,0,0.08)}
  th{background:#f0f4ff;padding:10px 14px;text-align:left;font-size:0.72rem;font-weight:700;letter-spacing:0.08em;color:#555;text-transform:uppercase;border-bottom:2px solid #dde3f0}
  td{padding:12px 14px;font-size:0.82rem;border-bottom:1px solid #f0f0f0;vertical-align:top}
  tr:last-child td{border-bottom:none}
  tr:hover td{background:#fafbff}
  .sym{font-weight:700;font-size:0.9rem;color:#1a1a2e}
  .co{font-size:0.73rem;color:#666;margin-top:2px}
  .rsi-os{color:#c62828;font-weight:700;font-size:1rem}
  .rsi-ob{color:#e65100;font-weight:700;font-size:1rem}
  .pos{color:#2e7d32}.neg{color:#c62828}
  .pill{display:inline-block;padding:2px 8px;border-radius:12px;font-size:0.7rem;font-weight:600;margin:1px}
  .pill-bull{background:#e8f5e9;color:#2e7d32;border:1px solid #a5d6a7}
  .pill-bear{background:#ffebee;color:#c62828;border:1px solid #ef9a9a}
  .pill-neu{background:#f5f5f5;color:#757575;border:1px solid #e0e0e0}
  .trigger{font-size:0.77rem;color:#555;max-width:220px}
  .footer{margin-top:28px;padding-top:16px;border-top:1px solid #e0e0e0;font-size:0.72rem;color:#999;text-align:center}
  .prob-high{color:#2e7d32;font-weight:700}
  .prob-med{color:#e65100;font-weight:700}
  .prob-low{color:#999}
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

    private static readonly string[] EasternTzIds = ["Eastern Standard Time", "America/New_York"];

    private static TimeZoneInfo? GetEasternTz()
    {
        foreach (var id in EasternTzIds)
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch { /* try next */ }
        }
        return null;
    }

    /// <summary>
    /// Sends an EOD email for signals already saved in the DailySignals table (manual trigger).
    /// Always fires — no deduplication — since this is an explicit user action.
    /// </summary>
    public async Task NotifySavedEodSignalsAsync(List<DailySignal> signals, DateTime triggeredAt)
    {
        if (signals.Count == 0) return;

        var recipientList = recipients.GetAll();
        if (recipientList.Count == 0) return;

        if (!_settings.Enabled) return;

        if (string.IsNullOrWhiteSpace(_settings.Username) || string.IsNullOrWhiteSpace(_settings.Password))
            return;

        try
        {
            await SendSavedSignalsEmailAsync(signals, recipientList, triggeredAt);
            logger.LogInformation("Manual EOD email sent for {Count} saved signal(s) to {Recipients} recipient(s).",
                signals.Count, recipientList.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send manual EOD signal email.");
        }
    }

    private async Task SendSavedSignalsEmailAsync(List<DailySignal> signals, List<string> recipientEmails, DateTime triggeredAt)
    {


        var tickerSummary = string.Join(", ", signals.Select(s => $"{s.Symbol} RSI:{s.Rsi:F1}"));
        var subject = signals.Count == 1
            ? $"📊 EOD Signals — {signals[0].Symbol} (RSI {signals[0].Rsi:F1}) End-of-Day Record"
            : $"📊 EOD Signals — {signals.Count} Records: {tickerSummary}";

        var body = BuildSavedSignalsHtmlBody(signals, triggeredAt);

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
        message.Headers.Add("X-Priority", "1");
        message.Headers.Add("X-MSMail-Priority", "High");
        message.Headers.Add("Importance", "High");

        foreach (var email in recipientEmails)
            message.To.Add(email);

        await SendViaMailKitAsync(message);
    }

    private static string BuildSavedSignalsHtmlBody(List<DailySignal> signals, DateTime triggeredAt)
    {
        var oversold   = signals.Where(s => s.ScanType == "Oversold").ToList();
        var overbought = signals.Where(s => s.ScanType == "Overbought").ToList();

        var sb = new StringBuilder();
        sb.AppendLine($@"<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""UTF-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1"">
<style>
  body{{margin:0;padding:0;background:#f5f5f5;font-family:'Segoe UI',Arial,sans-serif;color:#333}}
  .wrapper{{max-width:820px;margin:0 auto;padding:24px 16px}}
  .header{{background:linear-gradient(135deg,#1a237e,#283593);border-radius:12px;padding:24px 28px;margin-bottom:24px}}
  .header h1{{margin:0 0 6px;font-size:1.4rem;color:#fff;letter-spacing:0.05em}}
  .header p{{margin:0;font-size:0.85rem;color:rgba(255,255,255,0.85)}}
  .banner{{background:linear-gradient(135deg,#1565c0,#1976d2);border-radius:8px;padding:12px 20px;margin-bottom:20px;color:#fff;font-weight:600;font-size:0.9rem}}
  h2{{font-size:1rem;color:#333;margin:20px 0 8px;padding-bottom:4px;border-bottom:2px solid #e0e0e0}}
  table{{width:100%;border-collapse:collapse;background:#fff;border-radius:8px;overflow:hidden;box-shadow:0 1px 4px rgba(0,0,0,0.08);margin-bottom:20px}}
  th{{background:#1a237e;color:#fff;padding:10px 12px;text-align:left;font-size:0.8rem;text-transform:uppercase;letter-spacing:0.05em}}
  td{{padding:10px 12px;border-bottom:1px solid #f0f0f0;font-size:0.88rem;vertical-align:top}}
  .sym{{font-weight:700;font-size:0.95rem}}
  .co{{color:#888;font-size:0.78rem}}
  .rsi-os{{background:#fff3e0;color:#e65100;border-radius:4px;padding:2px 6px;font-weight:700}}
  .rsi-ob{{background:#fce4ec;color:#c62828;border-radius:4px;padding:2px 6px;font-weight:700}}
  .pill{{border-radius:4px;padding:2px 7px;font-size:0.75rem;font-weight:600;display:inline-block}}
  .pill-eod{{background:#fff3e0;color:#e65100;border:1px solid #ffcc02;font-weight:700}}
  .pill-conf{{background:#e8f5e9;color:#2e7d32;border:1px solid #a5d6a7;font-weight:700}}
  .pill-warn{{background:#f3e5f5;color:#7b1fa2;border:1px solid #ce93d8;font-weight:700}}
  .trigger{{color:#555;font-size:0.82rem}}
</style>
</head>
<body><div class=""wrapper"">
  <div class=""header"">
    <h1>📊 EOD Signals — Manual Notification</h1>
    <p>Today's saved end-of-day signals as of {triggeredAt:HH:mm} UTC — manually triggered.</p>
  </div>
  <div class=""banner"">
    📅 {signals.Count} signal(s) recorded for {triggeredAt:dddd, MMMM d yyyy}
  </div>");

        if (oversold.Count > 0)
        {
            sb.AppendLine("  <h2>🟢 Oversold Signals</h2>");
            sb.AppendLine(BuildSavedSignalTable(oversold));
        }

        if (overbought.Count > 0)
        {
            sb.AppendLine("  <h2>🔴 Overbought Signals</h2>");
            sb.AppendLine(BuildSavedSignalTable(overbought));
        }

        sb.AppendLine("</div></body></html>");
        return sb.ToString();
    }

    private static string BuildSavedSignalTable(List<DailySignal> signals)
    {
        var sb = new StringBuilder();
        sb.AppendLine(@"  <table>
    <thead>
      <tr>
        <th>Ticker</th><th>RSI</th><th>Price</th><th>Signal Type</th><th>Trigger Details</th>
      </tr>
    </thead>
    <tbody>");

        foreach (var s in signals)
        {
            var rsiClass  = s.ScanType == "Oversold" ? "rsi-os" : "rsi-ob";
            var typePill  = s.SignalType switch
            {
                "EodConfirm"   => "<span class=\"pill pill-eod\">🎯 EOD CONFIRM</span>",
                "Confirmed"    => "<span class=\"pill pill-conf\">✅ CONFIRMED</span>",
                _              => "<span class=\"pill pill-warn\">⚠️ EARLY WARNING</span>",
            };
            sb.AppendLine($@"      <tr>
        <td><div class=""sym"">{s.Symbol}</div><div class=""co"">{s.CompanyName}</div></td>
        <td><span class=""{rsiClass}"">{s.Rsi:F1}</span></td>
        <td>${s.Price:F2}</td>
        <td>{typePill}</td>
        <td class=""trigger"">{s.TriggerDetails}</td>
      </tr>");
        }

        sb.AppendLine("    </tbody>\n  </table>");
        return sb.ToString();
    }

    /// <summary>
    /// Sends the RSI 2-Stage EOD Signal Report.
    /// Section 1: ✅ CONFIRMED &amp; PROMOTED — signals inserted into DailySignals this run.
    /// Section 2: ⏳ REVERSALS AWAITING CONFIRMATION — Bull/Bear Turns that did not pass the Stage-2 gate.
    /// Fires only when there is at least one newly-seen signal (deduplication via tracker).
    /// </summary>
    public async Task NotifyEodReportAsync(
        List<RsiScanResult> confirmed,
        List<RsiScanResult> awaiting,
        DateTime scannedAt)
    {
        if (!tracker.HasNewEodActivity(confirmed, awaiting)) return;
        if (confirmed.Count == 0 && awaiting.Count == 0) return;

        var recipientList = recipients.GetAll();
        if (recipientList.Count == 0) return;
        if (!_settings.Enabled) return;
        if (string.IsNullOrWhiteSpace(_settings.Username) || string.IsNullOrWhiteSpace(_settings.Password)) return;

        try
        {
            var tz = GetEasternTz();
            var etNow = tz is not null ? TimeZoneInfo.ConvertTimeFromUtc(scannedAt, tz) : scannedAt;
            var dateTimeStr = etNow.ToString("dddd, MMMM d, yyyy") + " \u2014 " + etNow.ToString("h:mm tt") + " ET";

            var tickerParts = confirmed.Select(s => $"{s.Symbol} RSI:{s.Rsi:F1}")
                .Concat(awaiting.Select(s => $"{s.Symbol} \u23f3"))
                .ToList();
            var subject = confirmed.Count > 0
                ? $"\ud83d\udcca RSI 2-Stage \u2014 {confirmed.Count} Confirmed: {string.Join(", ", confirmed.Select(s => s.Symbol))}"
                : $"\u23f3 RSI 2-Stage \u2014 {awaiting.Count} Awaiting: {string.Join(", ", awaiting.Select(s => s.Symbol))}";

            var body = BuildEod2HtmlBody(confirmed, awaiting, dateTimeStr);



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
            message.Headers.Add("X-Priority", "1");
            message.Headers.Add("X-MSMail-Priority", "High");
            message.Headers.Add("Importance", "High");

            foreach (var email in recipientList)
                message.To.Add(email);

            await SendViaMailKitAsync(message);
            logger.LogInformation(
                "RSI 2-Stage EOD Report sent: {Confirmed} confirmed, {Awaiting} awaiting \u2014 {Recipients} recipient(s).",
                confirmed.Count, awaiting.Count, recipientList.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send RSI 2-Stage EOD Report email.");
        }
    }

    private static string FormatEasternTime(DateTime utc)
    {
        var tz = GetEasternTz();
        var et = tz is not null ? TimeZoneInfo.ConvertTimeFromUtc(utc, tz) : utc;
        return et.ToString("dddd, MMMM d, yyyy") + " \u2014 " + et.ToString("h:mm tt") + " ET";
    }

    private static string BuildEod2HtmlBody(
        List<RsiScanResult> confirmed,
        List<RsiScanResult> awaiting,
        string dateTimeStr)
    {
        var sb = new StringBuilder();
        sb.AppendLine(@"<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""UTF-8"">
<meta name=""viewport"" content=""width=device-width,initial-scale=1"">
<style>
  body{margin:0;padding:0;background:#f4f6fa;font-family:'Segoe UI',Arial,sans-serif;color:#1a1a2e}
  .wrapper{max-width:700px;margin:0 auto;padding:24px 16px}
  .header{background:linear-gradient(135deg,#1a237e,#283593);border-radius:12px;padding:24px 28px;margin-bottom:24px;color:#fff}
  .header h1{margin:0 0 6px;font-size:1.35rem;letter-spacing:0.04em}
  .header .dt{margin:0 0 12px;font-size:0.85rem;opacity:.85}
  .header .intro{margin:0;font-size:0.82rem;opacity:.8;line-height:1.55}
  .section-head{font-size:0.85rem;font-weight:700;letter-spacing:0.08em;text-transform:uppercase;padding:10px 16px;border-radius:8px 8px 0 0;margin:24px 0 0}
  .section-head.conf{background:#1b5e20;color:#fff}
  .section-head.await{background:#e65100;color:#fff}
  .card{background:#fff;border-radius:0 0 8px 8px;box-shadow:0 2px 6px rgba(0,0,0,.08);margin-bottom:4px;padding:0}
  .card-inner{padding:16px 20px}
  .card+.section-head{margin-top:20px}
  .card-title{font-size:1rem;font-weight:700;color:#1a237e;margin:0 0 12px;padding-bottom:8px;border-bottom:1px solid #e8eaf6}
  .card-title span{font-weight:400;font-size:0.82rem;color:#666;margin-left:6px}
  .fields{display:table;width:100%;border-collapse:collapse}
  .field{display:table-row}
  .field .lbl{display:table-cell;width:44%;padding:3px 0;font-size:0.8rem;color:#666;font-weight:600;letter-spacing:0.03em}
  .field .val{display:table-cell;padding:3px 0;font-size:0.82rem;color:#1a1a2e;font-weight:500}
  .status-bar{margin-top:14px;padding:8px 12px;border-radius:6px;font-size:0.82rem;font-weight:700;letter-spacing:0.04em}
  .status-conf{background:#e8f5e9;color:#2e7d32;border:1px solid #a5d6a7}
  .status-await{background:#fff3e0;color:#e65100;border:1px solid #ffcc02}
  .warn-bar{margin-top:8px;padding:7px 12px;border-radius:6px;background:#fff8e1;color:#bf360c;font-size:0.79rem;font-weight:600;border:1px solid #ffe082}
  .action-bar{margin-top:8px;padding:7px 12px;border-radius:6px;background:#f3e5f5;color:#4a148c;font-size:0.79rem;font-weight:600;border:1px solid #ce93d8}
  .pill{display:inline-block;padding:2px 8px;border-radius:10px;font-size:0.72rem;font-weight:700}
  .pill-ok{background:#e8f5e9;color:#2e7d32;border:1px solid #a5d6a7}
  .pill-fail{background:#ffebee;color:#c62828;border:1px solid #ef9a9a}
  .pill-warn{background:#fff3e0;color:#e65100;border:1px solid #ffcc02}
  .empty{padding:16px 20px;background:#fff;border-radius:0 0 8px 8px;color:#999;font-size:0.82rem;font-style:italic}
  .footer{margin-top:28px;padding-top:16px;border-top:1px solid #dde3f0;font-size:0.72rem;color:#999;text-align:center;line-height:1.7}
</style>
</head>
<body>
<div class=""wrapper"">");

        sb.AppendLine($@"  <div class=""header"">
    <h1>&#x1F4CA; RSI 2-Stage EOD Signal Report</h1>
    <p class=""dt"">{dateTimeStr}</p>
    <p class=""intro"">Confirmed signals originate from previously staged Oversold or Overbought setups.<br>
    A signal is promoted only after RSI momentum reverses and required Price and Volume confirmation pass.</p>
  </div>");

        // ── Section 1: Confirmed & Promoted ──────────────────────────────────
        sb.AppendLine($@"  <div class=""section-head conf"">&#x2705; CONFIRMED &amp; PROMOTED ({confirmed.Count})</div>");
        if (confirmed.Count == 0)
        {
            sb.AppendLine(@"  <div class=""empty"">No signals were promoted to DailySignals during this EOD run.</div>");
        }
        else
        {
            sb.AppendLine(@"  <div class=""card"">");
            foreach (var r in confirmed)
            {
                bool ema9Conf = r.ScanType == ScanType.Oversold ? r.CurrentPrice > r.Ema9Price : r.CurrentPrice < r.Ema9Price;
                decimal? riskPct = (r.CurrentPrice > 0 && r.DynamicStopLoss > 0)
                    ? Math.Round(Math.Abs(r.CurrentPrice - r.DynamicStopLoss) / r.CurrentPrice * 100m, 1)
                    : null;
                var rsiDeltaStr = r.RsiDelta1D.HasValue
                    ? (r.RsiDelta1D.Value >= 0 ? $"+{r.RsiDelta1D.Value:F2} &#x2191;" : $"{r.RsiDelta1D.Value:F2} &#x2193;")
                    : "\u2014";
                var trendShiftClean = r.TrendShift.Replace("\ud83d\udfe2 ", "").Replace("\ud83d\udfe1 ", "").Replace("\ud83d\udd34 ", "");
                var turnLabel = string.IsNullOrEmpty(r.TurnStrength) || r.TurnStrength == "Normal"
                    ? trendShiftClean
                    : $"{trendShiftClean} \u2014 {r.TurnStrength}";
                var trendSetup = r.TrendSetup200.Length > 0 ? r.TrendSetup200 : (r.Sma200 > 0 ? (r.CurrentPrice > r.Sma200 ? "Trend-Aligned" : "Counter-Trend") : "\u2014");
                var ema9Pill = ema9Conf
                    ? "<span class=\"pill pill-ok\">&#x2713; Confirmed</span>"
                    : "<span class=\"pill pill-warn\">&#x23F3; Pending</span>";

                sb.AppendLine($@"    <div class=""card-inner"">
      <div class=""card-title"">{r.Symbol}<span>— {r.ScanType}</span></div>
      <div class=""fields"">
        <div class=""field""><span class=""lbl"">RSI</span><span class=""val"">{r.Rsi:F1}</span></div>
        <div class=""field""><span class=""lbl"">RSI &#x394;1D</span><span class=""val"">{rsiDeltaStr}</span></div>
        <div class=""field""><span class=""lbl"">Trend Shift</span><span class=""val"">{turnLabel}</span></div>
        <div class=""field""><span class=""lbl"">EOD Price</span><span class=""val""><span class=""pill pill-ok"">&#x2713; Passed</span></span></div>
        <div class=""field""><span class=""lbl"">Volume</span><span class=""val""><span class=""pill pill-ok"">&#x2713; {r.VolumeRatio:F2}x &#x2014; Validated</span></span></div>
        <div class=""field""><span class=""lbl"">EMA9 (Supporting)</span><span class=""val"">{ema9Pill}</span></div>
        <div class=""field""><span class=""lbl"">Entry</span><span class=""val"">{"$"}{r.CurrentPrice:F2}</span></div>
        {(r.DynamicStopLoss > 0 ? $"<div class=\"field\"><span class=\"lbl\">Stop</span><span class=\"val\">${r.DynamicStopLoss:F2}</span></div>" : "")}
        {(r.DynamicStopLoss > 0 ? $"<div class=\"field\"><span class=\"lbl\">Risk / Share</span><span class=\"val\">${Math.Abs(r.CurrentPrice - r.DynamicStopLoss):F2}{(riskPct.HasValue ? $" / {riskPct:F1}%" : "")}</span></div>" : "")}
        {(r.Sma200 > 0 ? $"<div class=\"field\"><span class=\"lbl\">SMA200</span><span class=\"val\">${r.Sma200:F2}</span></div>" : "")}
        {(trendSetup != "\u2014" ? $"<div class=\"field\"><span class=\"lbl\">Setup</span><span class=\"val\">{trendSetup}</span></div>" : "")}
      </div>
      <div class=""status-bar status-conf"">&#x2705; CONFIRMED &amp; PROMOTED</div>
      {(r.ChaseRisk == "Elevated" ? "<div class=\"warn-bar\">&#x26A0; Explosive reversal \u2014 elevated chase risk</div>" : "")}
    </div>");
            }
            sb.AppendLine(@"  </div>");
        }

        // ── Section 2: Awaiting Confirmation ─────────────────────────────────
        sb.AppendLine($@"  <div class=""section-head await"">&#x23F3; REVERSALS AWAITING CONFIRMATION ({awaiting.Count})</div>");
        if (awaiting.Count == 0)
        {
            sb.AppendLine(@"  <div class=""empty"">No active setups are in the CONFIRMING stage.</div>");
        }
        else
        {
            sb.AppendLine(@"  <div class=""card"">");
            foreach (var r in awaiting)
            {
                bool eodPriceConf = r.DailyAtr > 0 && (r.ScanType == ScanType.Oversold
                    ? r.CurrentPrice > r.OpenPrice && r.CurrentPrice >= r.DayHigh - (0.25m * r.DailyAtr)
                    : r.CurrentPrice < r.OpenPrice && r.CurrentPrice <= r.DayLow + (0.25m * r.DailyAtr));
                bool ema9Conf = r.ScanType == ScanType.Oversold ? r.CurrentPrice > r.Ema9Price : r.CurrentPrice < r.Ema9Price;
                var trendShiftClean = r.TrendShift.Replace("\ud83d\udfe2 ", "").Replace("\ud83d\udfe1 ", "").Replace("\ud83d\udd34 ", "");
                var eodPricePill = eodPriceConf
                    ? "<span class=\"pill pill-ok\">&#x2713; Passed</span>"
                    : "<span class=\"pill pill-fail\">&#x274C; Failed</span>";
                var ema9Pill = ema9Conf
                    ? "<span class=\"pill pill-ok\">&#x2713; Confirmed</span>"
                    : "<span class=\"pill pill-warn\">&#x23F3; Pending</span>";
                // Stage-2 volume threshold is 1.5x — display pass/fail against that threshold.
                var volPill = r.VolumeRatio >= 1.5m
                    ? $"<span class=\"pill pill-ok\">&#x2713; {r.VolumeRatio:F2}x \u2014 Validated</span>"
                    : r.VolumeRatio < 0.8m
                        ? $"<span class=\"pill pill-fail\">&#x26A0; {r.VolumeRatio:F2}x \u2014 Low-Volume Trap</span>"
                        : $"<span class=\"pill pill-fail\">&#x274C; {r.VolumeRatio:F2}x \u2014 Below 1.5x</span>";

                sb.AppendLine($@"    <div class=""card-inner"">
      <div class=""card-title"">{r.Symbol}<span>— {r.ScanType}</span></div>
      <div class=""fields"">
        <div class=""field""><span class=""lbl"">RSI</span><span class=""val"">{r.Rsi:F1}</span></div>
        <div class=""field""><span class=""lbl"">Trend Shift</span><span class=""val"">{trendShiftClean}</span></div>
        <div class=""field""><span class=""lbl"">EOD Price (Required)</span><span class=""val"">{eodPricePill}</span></div>
        <div class=""field""><span class=""lbl"">Volume (Required)</span><span class=""val"">{volPill}</span></div>
        <div class=""field""><span class=""lbl"">EMA9 (Supporting)</span><span class=""val"">{ema9Pill}</span></div>
      </div>
      <div class=""status-bar status-await"">CONFIRMING</div>
      <div class=""action-bar"">Continue Monitoring</div>
    </div>");
            }
            sb.AppendLine(@"  </div>");
        }

        sb.AppendLine($@"  <div class=""footer"">
    RSI 2-Stage EOD Signal Report &nbsp;&middot;&nbsp; <strong>Portfolio Manager</strong><br>
    {dateTimeStr} &nbsp;&middot;&nbsp; Not financial advice. Do your own research.
  </div>
</div>
</body>
</html>");

        return sb.ToString();
    }

    /// <summary>
    /// Checks the scan result for new EOD CONFIRM signals (legacy path — kept for backward compatibility).
    /// Replaced by <see cref="NotifyEodReportAsync"/> in the background service.
    /// </summary>
    public async Task NotifyNewEodConfirmedSignalsAsync(ScannerResponse scanResult)
    {
        var allResults = (scanResult.OversoldChain ?? [])
            .Concat(scanResult.OverboughtChain ?? [])
            .ToList();

        var newlyEod = tracker.GetNewlyEodConfirmedAndSync(allResults);

        if (newlyEod.Count == 0) return;

        var recipientList = recipients.GetAll();
        if (recipientList.Count == 0) return;

        if (!_settings.Enabled) return;

        if (string.IsNullOrWhiteSpace(_settings.Username) || string.IsNullOrWhiteSpace(_settings.Password))
            return;

        try
        {
            await SendEodAlertEmailAsync(newlyEod, recipientList, scanResult.ScannedAt);
            logger.LogInformation(
                "EOD Confirm email sent for {Count} signal(s) to {Recipients} recipient(s).",
                newlyEod.Count, recipientList.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send EOD Confirm alert email.");
        }
    }

    private async Task SendEodAlertEmailAsync(
        List<RsiScanResult> signals,
        List<string> recipientEmails,
        DateTime scannedAt)
    {


        var oversold   = signals.Where(s => s.ScanType == ScanType.Oversold).ToList();
        var overbought = signals.Where(s => s.ScanType == ScanType.Overbought).ToList();
        var tickerSummary = string.Join(", ", signals.Select(s => $"{s.Symbol} RSI:{s.Rsi:F1}"));
        var hasEodConfirm = signals.Any(s => s.Status == SignalStatus.EodConfirm);
        var prefix = hasEodConfirm ? "🎯 EOD CONFIRM" : "✅ EOD Signal";
        var subject = signals.Count == 1
            ? $"{prefix} — {signals[0].Symbol} (RSI {signals[0].Rsi:F1}) End-of-Day Signal"
            : $"{prefix} — {signals.Count} Signals: {tickerSummary}";

        var body = BuildEodHtmlBody(oversold, overbought, scannedAt);

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
        message.Headers.Add("X-Priority", "1");
        message.Headers.Add("X-MSMail-Priority", "High");
        message.Headers.Add("Importance", "High");

        foreach (var email in recipientEmails)
            message.To.Add(email);

        await SendViaMailKitAsync(message);
    }

    private static string BuildEodHtmlBody(
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
  body{margin:0;padding:0;background:#f5f5f5;font-family:'Segoe UI',Arial,sans-serif;color:#333}
  .wrapper{max-width:820px;margin:0 auto;padding:24px 16px}
  .header{background:linear-gradient(135deg,#1a237e,#283593);border-radius:12px;padding:24px 28px;margin-bottom:24px}
  .header h1{margin:0 0 6px;font-size:1.4rem;color:#fff;letter-spacing:0.05em}
  .header p{margin:0;font-size:0.85rem;color:rgba(255,255,255,0.85)}
  .eod-banner{background:linear-gradient(135deg,#ff6f00,#f57c00);border-radius:8px;padding:12px 20px;margin-bottom:20px;color:#fff;font-weight:600;font-size:0.9rem}
  .badge{display:inline-block;padding:3px 12px;border-radius:20px;font-size:0.75rem;font-weight:700;letter-spacing:0.06em}
  .section-title{font-size:0.8rem;font-weight:700;letter-spacing:0.1em;text-transform:uppercase;margin:0 0 12px;padding-bottom:8px;border-bottom:2px solid #e0e0e0}
  .section-title.os{color:#2e7d32}.section-title.ob{color:#c62828}
  table{width:100%;border-collapse:collapse;margin-bottom:24px;background:#fff;border-radius:10px;overflow:hidden;box-shadow:0 1px 4px rgba(0,0,0,0.08)}
  th{background:#fff8e1;padding:10px 14px;text-align:left;font-size:0.72rem;font-weight:700;letter-spacing:0.08em;color:#555;text-transform:uppercase;border-bottom:2px solid #ffe082}
  td{padding:12px 14px;font-size:0.82rem;border-bottom:1px solid #f0f0f0;vertical-align:top}
  tr:last-child td{border-bottom:none}
  .sym{font-weight:700;font-size:0.9rem;color:#1a1a2e}
  .co{font-size:0.73rem;color:#666;margin-top:2px}
  .rsi-os{color:#c62828;font-weight:700;font-size:1rem}.rsi-ob{color:#e65100;font-weight:700;font-size:1rem}
  .pos{color:#2e7d32}.neg{color:#c62828}
  .pill{display:inline-block;padding:2px 8px;border-radius:12px;font-size:0.7rem;font-weight:600;margin:1px}
  .pill-bull{background:#e8f5e9;color:#2e7d32;border:1px solid #a5d6a7}
  .pill-bear{background:#ffebee;color:#c62828;border:1px solid #ef9a9a}
  .pill-neu{background:#f5f5f5;color:#757575;border:1px solid #e0e0e0}
  .pill-eod{background:#fff3e0;color:#e65100;border:1px solid #ffcc02;font-weight:700}
  .pill-conf{background:#e8f5e9;color:#2e7d32;border:1px solid #a5d6a7;font-weight:700}
  .trigger{font-size:0.77rem;color:#555;max-width:240px}
  .footer{margin-top:28px;padding-top:16px;border-top:1px solid #e0e0e0;font-size:0.72rem;color:#999;text-align:center}
</style>
</head>
<body>
<div class=""wrapper"">");

        sb.AppendLine($@"  <div class=""header"">
    <h1>&#x1F3AF; RSI 2-Stage EOD Signal Report</h1>
    <p>Confirmed signals originate from previously staged Oversold or Overbought setups.
       A signal is promoted only after RSI momentum reverses and required Price and Volume confirmation pass.</p>
    <p>{oversold.Count + overbought.Count} signal(s) — {FormatEasternTime(scannedAt)}.</p>
  </div>
  <div class=""eod-banner"">
    &#x23F0; EOD Confirm signals fire between 3:30&ndash;4:00&nbsp;PM Eastern Time when a staged setup passes
    the Stage-2 gate: RSI reversal &middot; Price vs&nbsp;9-EMA &middot; Volume &ge;&nbsp;1.5&times;
  </div>");

        if (oversold.Count > 0)
        {
            sb.AppendLine(@"  <p class=""section-title os"">🟢 Oversold EOD Confirm — High-Confidence Buy Setup Near Close</p>");
            sb.AppendLine(BuildEodSignalTable(oversold, ScanType.Oversold));
        }

        if (overbought.Count > 0)
        {
            sb.AppendLine(@"  <p class=""section-title ob"">🔴 Overbought EOD Confirm — High-Confidence Sell / Exit Near Close</p>");
            sb.AppendLine(BuildEodSignalTable(overbought, ScanType.Overbought));
        }

        sb.AppendLine($@"  <div class=""footer"">
    <p>EOD Confirm Alert — <strong>Portfolio Manager</strong>.<br>
    Scanned at {scannedAt:yyyy-MM-dd HH:mm:ss} UTC &nbsp;·&nbsp; Not financial advice.<br>
    Do your own research before making investment decisions.</p>
  </div>
</div></body></html>");

        return sb.ToString();
    }

    private static string BuildEodSignalTable(List<RsiScanResult> signals, ScanType type)
    {
        var sb = new StringBuilder();
        sb.AppendLine(@"  <table>
    <thead>
      <tr>
        <th>Ticker</th><th>RSI</th><th>Price</th><th>9-EMA</th>
        <th>ATR (14)</th><th>Volume</th><th>EOD Confirm Details</th>
      </tr>
    </thead>
    <tbody>");

        foreach (var r in signals)
        {
            var rsiClass = type == ScanType.Oversold ? "rsi-os" : "rsi-ob";
            var changeClass = r.ChangePercent >= 0 ? "pos" : "neg";
            var changeSign  = r.ChangePercent >= 0 ? "+" : "";
            var pvsEma = type == ScanType.Oversold
                ? (r.CurrentPrice > r.Ema9Price ? "<span class=\"pill pill-bull\">↑ > 9-EMA</span>" : "<span class=\"pill pill-bear\">↓ < 9-EMA</span>")
                : (r.CurrentPrice < r.Ema9Price ? "<span class=\"pill pill-bear\">↓ < 9-EMA</span>" : "<span class=\"pill pill-bull\">↑ > 9-EMA</span>");

            sb.AppendLine($@"      <tr>
        <td><div class=""sym"">{r.Symbol}</div><div class=""co"">{r.CompanyName}</div></td>
        <td><span class=""{rsiClass}"">{r.Rsi:F1}</span></td>
        <td>${r.CurrentPrice:F2}<br><small class=""{changeClass}"">{changeSign}{r.ChangePercent:F2}%</small></td>
        <td>${r.Ema9Price:F2} {pvsEma}</td>
        <td>${r.DailyAtr:F4}</td>
        <td><span class=""pill pill-bull"">Vol {r.VolumeRatio:F1}x</span></td>
        <td class=""trigger"">{(r.Status == SignalStatus.EodConfirm ? "<span class=\"pill pill-eod\">🎯 EOD CONFIRM</span>" : "<span class=\"pill pill-conf\">✅ CONFIRMED</span>")}<br>{r.TriggerDetails}</td>
      </tr>");
        }

        sb.AppendLine("    </tbody>\n  </table>");
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
