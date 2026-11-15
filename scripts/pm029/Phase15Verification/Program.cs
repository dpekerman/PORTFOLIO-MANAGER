using System.Text.Json;
using Microsoft.Extensions.Hosting;

namespace PortfolioManager.Phase15Verification;

public static class Program
{
    public static IHostBuilder CreateHostBuilder(string[] args) => new HostBuilder();

    public static async Task Main(string[] args)
    {
        if (args.Length != 3 || args[0] != "manifest")
            throw new ArgumentException("Usage: Phase15Verification manifest <read-only-export.json> <local-only-manifest.json>");
        var input = await File.ReadAllTextAsync(args[1]);
        var export = JsonSerializer.Deserialize<LegacyEvidenceExport>(input)
            ?? throw new InvalidDataException("Evidence export is empty.");
        var manifest = EvidenceManifest.Classify(export);
        await using var output = new FileStream(args[2], FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(output, manifest, EvidenceManifest.JsonOptions);
        Console.WriteLine($"Redacted manifest created: {manifest.Records.Count} records; complete portfolio valuation unavailable.");
    }
}
