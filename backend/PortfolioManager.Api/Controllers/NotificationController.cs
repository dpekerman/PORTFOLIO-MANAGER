using Microsoft.AspNetCore.Mvc;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services;

namespace PortfolioManager.Api.Controllers;

/// <summary>Manages email alert recipients and notification settings.</summary>
[ApiController]
[Route("api/[controller]")]
public class NotificationController(
    NotificationRecipientsService recipients,
    SignalNotificationTracker tracker,
    EmailNotificationService emailNotifier,
    IRsiScannerService scanner,
    ILogger<NotificationController> logger) : ControllerBase
{
    /// <summary>Returns the current list of notification email recipients.</summary>
    [HttpGet("recipients")]
    public ActionResult<NotificationRecipientsDto> GetRecipients()
        => Ok(new NotificationRecipientsDto { Emails = recipients.GetAll() });

    /// <summary>Replaces the full recipient list (duplicates and invalid addresses are removed).</summary>
    [HttpPut("recipients")]
    public ActionResult<NotificationRecipientsDto> UpdateRecipients([FromBody] NotificationRecipientsDto dto)
    {
        if (dto?.Emails is null)
            return BadRequest("Provide an emails array.");

        if (dto.Emails.Count > 50)
            return BadRequest("Maximum 50 recipients.");

        recipients.Save(dto.Emails);
        var saved = recipients.GetAll();
        logger.LogInformation("Notification recipients updated — {Count} address(es).", saved.Count);
        return Ok(new NotificationRecipientsDto { Emails = saved });
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

    /// <summary>
    /// Force an immediate RSI scan + notification check (bypasses the background service schedule).
    /// Useful to manually trigger emails for currently CONFIRMED signals.
    /// POST /api/notification/scan-now
    /// </summary>
    [HttpPost("scan-now")]
    public async Task<ActionResult<object>> ScanAndNotifyNow(CancellationToken ct)
    {
        logger.LogInformation("Manual scan-and-notify triggered via API.");

        var result = await scanner.ScanAsync(ct: ct);

        if (result.IsDemo)
            return Ok(new { triggered = false, reason = "Demo data — no emails sent." });

        // Reset the tracker so all current CONFIRMED signals will fire again
        tracker.ResetAll();

        await emailNotifier.NotifyNewConfirmedSignalsAsync(result);

        var confirmedCount =
            (result.OversoldChain?.Count(r => r.Status == SignalStatus.Confirmed) ?? 0) +
            (result.OverboughtChain?.Count(r => r.Status == SignalStatus.Confirmed) ?? 0);

        return Ok(new
        {
            triggered = true,
            confirmedSignals = confirmedCount,
            recipientCount = recipients.GetAll().Count,
            message = confirmedCount > 0
                ? $"Email sent for {confirmedCount} CONFIRMED signal(s) to {recipients.GetAll().Count} recipient(s)."
                : "No CONFIRMED signals found — no email sent.",
        });
    }
}

/// <summary>Request body for the send-test endpoint.</summary>
public sealed record SendTestEmailRequest(string ToEmail);
