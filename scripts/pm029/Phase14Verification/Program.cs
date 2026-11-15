using System.Text.Json;
using System.Text.Json.Serialization;
using PortfolioManager.Api.Models;
using PortfolioManager.Api.Services.Opportunities;
using PortfolioManager.Api.Services.Valuation;

if (args.Length != 3 || args[0] is not ("shadow" or "replay"))
{
    Console.Error.WriteLine("Usage: Phase14Verification <shadow|replay> <input.json> <new-output.json>");
    return 2;
}
var json = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    WriteIndented = true, Converters = { new JsonStringEnumConverter() }
};
var input = File.ReadAllText(args[1]);
string result;
if (args[0] == "replay")
{
    var package = JsonSerializer.Deserialize<SwingReplayPackage>(input, json)
        ?? throw new InvalidDataException("Replay package is empty.");
    var runner = new SwingReplayRunner();
    result = JsonSerializer.Serialize(runner.Evaluate(package), json);
    if (result != JsonSerializer.Serialize(runner.Evaluate(package), json))
        throw new InvalidOperationException("Replay was not deterministic.");
}
else
{
    var package = JsonSerializer.Deserialize<ShadowFile>(input, json)
        ?? throw new InvalidDataException("Shadow package is empty.");
    var calendar = new ExplicitFxPublicationCalendar(package.CoveredFrom, package.CoveredThrough, package.Calendar);
    var service = new SharedShadowValuationService(new VerifiedFinancialEvidenceAdapter(),
        new Pm029CadValuationCalculator(calendar));
    result = JsonSerializer.Serialize(service.Evaluate(package.Request), json);
    if (result != JsonSerializer.Serialize(service.Evaluate(package.Request), json))
        throw new InvalidOperationException("Shadow valuation was not deterministic.");
}
using (var output = new FileStream(args[2], FileMode.CreateNew, FileAccess.Write))
using (var writer = new StreamWriter(output))
    writer.Write(result);
Console.WriteLine("Deterministic local output created. No database, refresh service or application host was used.");
return 0;

internal sealed record ShadowFile(
    DateOnly CoveredFrom, DateOnly CoveredThrough,
    IReadOnlyList<Pm029FxPublicationDay> Calendar, ShadowValuationRequest Request);
