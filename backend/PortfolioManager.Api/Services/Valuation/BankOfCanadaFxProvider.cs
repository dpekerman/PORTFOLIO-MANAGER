using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using PortfolioManager.Api.Models;

namespace PortfolioManager.Api.Services.Valuation;

public sealed class BankOfCanadaFxProvider(HttpClient httpClient, TimeProvider timeProvider) : IBankOfCanadaFxProvider
{
    private const string Series = "FXUSDCAD";
    private const string Unit = "Canadian dollars per US dollar";
    private const string Endpoint = "https://www.bankofcanada.ca/valet/observations/FXUSDCAD/json";

    public async Task<IReadOnlyList<Pm029FxObservation>> GetUsdCadObservationsAsync(
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken ct = default)
    {
        if (endDate < startDate)
            throw new ArgumentOutOfRangeException(nameof(endDate));

        var url = $"{Endpoint}?start_date={startDate:yyyy-MM-dd}&end_date={endDate:yyyy-MM-dd}";
        using var response = await httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ValetResponse>(cancellationToken: ct)
            ?? throw new InvalidDataException("Bank of Canada Valet returned an empty response.");
        if (payload.SeriesDetail is null
            || !payload.SeriesDetail.TryGetValue(Series, out var detail)
            || !string.Equals(detail.Metadata?.Unit, Unit, StringComparison.Ordinal))
            throw new InvalidDataException("Bank of Canada Valet did not confirm the FXUSDCAD quote unit.");

        var retrievedAtUtc = timeProvider.GetUtcNow().ToUniversalTime();
        var observations = new List<Pm029FxObservation>();
        var seenDates = new HashSet<DateOnly>();
        foreach (var observation in payload.Observations ?? [])
        {
            if (!DateOnly.TryParseExact(observation.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var date))
                throw new InvalidDataException("Bank of Canada Valet returned an invalid observation date.");
            if (date < startDate || date > endDate)
                throw new InvalidDataException("Bank of Canada Valet returned an observation outside the requested date range.");
            if (!seenDates.Add(date))
                throw new InvalidDataException("Bank of Canada Valet returned duplicate FXUSDCAD dates.");
            if (!decimal.TryParse(observation.Value?.Value, NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out var rate)
                || rate <= 0m)
                throw new InvalidDataException($"Bank of Canada Valet returned an invalid FXUSDCAD value for {date:yyyy-MM-dd}.");

            observations.Add(new Pm029FxObservation(
                "Bank of Canada", Series, "USD", "CAD", date, rate, retrievedAtUtc));
        }

        return observations;
    }

    private sealed class ValetResponse
    {
        [JsonPropertyName("seriesDetail")]
        public Dictionary<string, SeriesDetail>? SeriesDetail { get; set; }

        [JsonPropertyName("observations")]
        public List<ValetObservation>? Observations { get; set; }
    }

    private sealed class SeriesDetail
    {
        [JsonPropertyName("metadata")]
        public SeriesMetadata? Metadata { get; set; }
    }

    private sealed class SeriesMetadata
    {
        [JsonPropertyName("unit")]
        public string? Unit { get; set; }
    }

    private sealed class ValetObservation
    {
        [JsonPropertyName("d")]
        public string? Date { get; set; }

        [JsonPropertyName("FXUSDCAD")]
        public ValetValue? Value { get; set; }
    }

    private sealed class ValetValue
    {
        [JsonPropertyName("v")]
        public string? Value { get; set; }
    }
}
