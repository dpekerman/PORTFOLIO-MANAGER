using System.Text.Json;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services;

/// <summary>
/// Singleton that stores the email recipient list and persists it to a JSON file
/// so recipients survive application restarts.
/// </summary>
public class NotificationRecipientsService
{
    private readonly string _filePath;
    private readonly ILogger<NotificationRecipientsService> _logger;
    private List<string> _emails = [];
    private readonly object _lock = new();

    public NotificationRecipientsService(
        ILogger<NotificationRecipientsService> logger,
        IWebHostEnvironment env)
    {
        _logger = logger;
        _filePath = Path.Combine(env.ContentRootPath, "notification-recipients.json");
        Load();
    }

    public List<string> GetAll()
    {
        lock (_lock) return [.. _emails];
    }

    public void Save(IEnumerable<string> emails)
    {
        var valid = emails
            .Select(e => e.Trim().ToLowerInvariant())
            .Where(e => e.Contains('@') && e.Length > 5)
            .Distinct()
            .ToList();

        lock (_lock)
        {
            _emails = valid;
            Persist();
        }
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                var dto = JsonSerializer.Deserialize<NotificationRecipientsDto>(json);
                if (dto?.Emails is not null)
                {
                    lock (_lock) _emails = dto.Emails;
                    _logger.LogInformation("Loaded {Count} notification recipients.", _emails.Count);
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load notification recipients. Starting with empty list.");
        }
        lock (_lock) _emails = [];
    }

    private void Persist()
    {
        try
        {
            var json = JsonSerializer.Serialize(
                new NotificationRecipientsDto { Emails = _emails },
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not persist notification recipients.");
        }
    }
}
