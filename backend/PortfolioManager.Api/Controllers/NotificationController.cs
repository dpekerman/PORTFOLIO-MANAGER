using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PortfolioManager.Api.Data;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers;

/// <summary>Manages email alert recipients and notification settings.</summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class NotificationController(
    NotificationRecipientsService recipients,
    SignalNotificationTracker tracker,
    EmailNotificationService emailNotifier,
    IRsiScannerService scanner,
    AppDbContext db,
    ILogger<NotificationController> logger) : ControllerBase
{
    /// <summary>Returns the current list of notification email recipients.</summary>
    [HttpGet("recipients")]
    public ActionResult<NotificationRecipientsDto> GetRecipients()
        => Ok(new NotificationRecipientsDto { Emails = recipients.GetAll() });

    /// <summary>Replaces the full recipient list (duplicates and invalid addresses are removed).</summary>
    [Authorize(Roles = "Admin")]
    [HttpPut("recipients")]
    public ActionResult<NotificationRecipientsDto> UpdateRecipients([FromBody] NotificationRecipientsDto dto)
    {
        if (dto?.Emails is null)
            return BadRequest("Provide an emails array.");

        if (dto.Emails.Count > 50)
            return BadRequest("Maximum 50 recipients.");

        recipients.Save(dto.Emails);
        var saved = recipients.GetAll();
        logger.LogInformation("Notification recipients updated Ã¢â‚¬â€ {Count} address(es).", saved.Count);
        return Ok(new NotificationRecipientsDto { Emails = saved });
    }

    /// <summary>
    /// Score alert: the client sends the eligible Priority Candidate symbols; scores are re-read
    /// server-side, and ONE email is sent for symbols at/above the threshold not yet alerted today.
    /// </summary>
    [HttpPost("score-alert")]
    public async Task<ActionResult<object>> ScoreAlert(
        [FromBody] ScoreAlertRequest request,
        [FromServices] IPortfolioActionScoreService actionScores,
        [FromServices] ScoreAlertTracker scoreTracker,
        [FromServices] IAuditService audit,
        [FromServices] IWebHostEnvironment env,
        CancellationToken ct)
    {
        if (request?.Symbols is null || request.Symbols.Count > 200)
            return BadRequest("Provide up to 200 symbols.");

        var threshold = Math.Clamp(request.Threshold, 1m, 100m);

        // Same window as the RSI scanner's background alerts (9:00-16:30 ET, Mon-Fri); Development bypasses it.
        var easternNow = MarketHoursGate.GetEasternNow();
        if (!env.IsDevelopment() && (easternNow is null || !MarketHoursGate.IsMarketHours(easternNow.Value)))
            return Ok(new { sent = false, reason = "OutsideMarketHours", symbols = Array.Empty<string>() });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var wanted = request.Symbols.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var qualifying = (await actionScores.GetScoresAsync(userId, ct))
            .Where(s => wanted.Contains(s.Symbol) && s.TotalScore >= threshold)
            .ToList();

        var etTz = TimeZoneInfo.GetSystemTimeZones()
            .FirstOrDefault(z => z.Id is "Eastern Standard Time" or "America/New_York");
        var day = (etTz is not null ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, etTz) : DateTime.UtcNow)
            .ToString("yyyy-MM-dd");

        var fresh = scoreTracker.GetNewAndSync(userId, day, qualifying.Select(q => q.Symbol));
        if (fresh.Count == 0)
            return Ok(new { sent = false, symbols = Array.Empty<string>() });

        var toSend = qualifying.Where(q => fresh.Contains(q.Symbol, StringComparer.OrdinalIgnoreCase)).ToList();
        var sentTo = await emailNotifier.SendScoreAlertAsync(toSend, threshold);
        var sent = sentTo.Count > 0;
        if (!sent)
        {
            scoreTracker.Forget(userId, fresh);
        }
        else
        {
            var sentAtUtc = DateTime.UtcNow;
            var symbolList = string.Join(", ", toSend.Select(t => t.Symbol));
            await audit.LogAsync(
                AuditCategories.Automation, AuditActions.Other,
                $"Score alert email (score ≥ {threshold:F0}) sent at {sentAtUtc:yyyy-MM-dd HH:mm:ss} UTC to {sentTo.Count} recipient(s): {string.Join(", ", sentTo)} — {symbolList}",
                "ScoreAlertEmail", ct: ct,
                changesJson: System.Text.Json.JsonSerializer.Serialize(new[]
                {
                    new AuditChangeDto("Recipients", null, string.Join(", ", sentTo)),
                    new AuditChangeDto("Symbols", null, string.Join(", ", toSend.Select(t => $"{t.Symbol} ({t.TotalScore:F0})"))),
                    new AuditChangeDto("Sent at (UTC)", null, sentAtUtc.ToString("yyyy-MM-dd HH:mm:ss")),
                }));
        }

        return Ok(new { sent, symbols = toSend.Select(t => t.Symbol) });
    }

    /// <summary>Sends a sample Priority Candidate score alert (marked [TEST]) to registered recipients; does not touch the daily alert tracker.</summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("score-alert-test")]
    public async Task<ActionResult<object>> ScoreAlertTest()
    {
        var sample = new ActionScoreDto(
            "TEST", "Sample Company (test data)", "Core", "Tier 1",
            24m, 22m, 18m, 12m, 76m, "HIGH_PRIORITY", "Improving", 31.5m, "Underweight", 123.45m);
        var sentTo = await emailNotifier.SendScoreAlertAsync([sample], 70m, isTest: true);
        return sentTo.Count > 0
            ? Ok(new { success = true, message = $"Test score alert sent to {sentTo.Count} recipient(s).", recipientCount = sentTo.Count })
            : UnprocessableEntity(new { success = false, error = "Test score alert not sent. Check email settings and recipients." });
    }

    /// <summary>Returns diagnostic status: tracked signals + saved recipients.</summary>
    [HttpGet("status")]
    public ActionResult<object> GetStatus()
        => Ok(new
        {
            trackedSignals = tracker.TrackedCount,
            recipientCount = recipients.GetAll().Count,
            recipients = recipients.GetAll(),
        });

    /// <summary>
    /// Sends a test email to verify SMTP configuration.
    /// POST /api/notification/send-test  { "toEmail": "user@example.com" }
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("send-test")]
    public async Task<ActionResult<object>> SendTestEmail([FromBody] SendTestEmailRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.ToEmail) || !request.ToEmail.Contains('@'))
            return BadRequest(new { success = false, error = "Provide a valid toEmail address." });

        var error = await emailNotifier.SendTestEmailAsync(request.ToEmail);
        if (error is null)
            return Ok(new { success = true, message = $"Test email sent to {request.ToEmail}." });

        return UnprocessableEntity(new { success = false, error });
    }

    /// <summary>Sends a representative Automation summary to registered recipients without running automation.</summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("send-automation-test")]
    public async Task<ActionResult<object>> SendAutomationTestEmail()
    {
        var result = await emailNotifier.SendAutomationTestEmailAsync();
        return result.Success
            ? Ok(new { success = true, message = result.Message, recipientCount = result.RecipientCount })
            : UnprocessableEntity(new { success = false, error = result.Message, recipientCount = result.RecipientCount });
    }

    /// <summary>
    /// Force an immediate RSI scan + notification check (bypasses the background service schedule).
    /// Useful to manually trigger emails for currently CONFIRMED signals.
    /// POST /api/notification/scan-now
    /// </summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("scan-now")]
    public async Task<ActionResult<object>> ScanAndNotifyNow(CancellationToken ct)
    {
        logger.LogInformation("Manual scan-and-notify triggered via API.");

        var result = await scanner.ScanAsync(ct: ct);

        if (result.IsDemo)
            return Ok(new { triggered = false, reason = "Demo data Ã¢â‚¬â€ no emails sent." });

        // Query today's saved EOD signals (SignalDate is stored as ET date)
        var etTz = TimeZoneInfo.GetSystemTimeZones()
            .FirstOrDefault(z => z.Id is "Eastern Standard Time" or "America/New_York");
        var etNow = etTz is not null
            ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, etTz)
            : DateTime.UtcNow;
        var today = etNow.ToString("yyyy-MM-dd");
        var todaySignals = await db.DailySignals
            .Where(s => s.SignalDate == today)
            .OrderBy(s => s.Symbol)
            .ToListAsync(ct);

        await emailNotifier.NotifySavedEodSignalsAsync(todaySignals, DateTime.UtcNow);

        return Ok(new
        {
            triggered = true,
            confirmedSignals = todaySignals.Count,
            recipientCount = recipients.GetAll().Count,
            message = todaySignals.Count > 0
                ? $"Email sent for {todaySignals.Count} EOD signal(s) to {recipients.GetAll().Count} recipient(s)."
                : "No EOD signals saved for today Ã¢â‚¬â€ no email sent.",
        });
    }
}

/// <summary>Request body for the send-test endpoint.</summary>
public sealed record SendTestEmailRequest(string ToEmail);

/// <summary>Request body for the score-alert endpoint.</summary>
public sealed record ScoreAlertRequest(decimal Threshold, List<string> Symbols);