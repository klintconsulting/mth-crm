using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Mth.SmartSync.Functions;

/// <summary>
/// Timer-triggered SMART sync (replaces the Salesforce scheduled batch "weekdays at 07:00").
/// Schedule: app setting SMART_SCHEDULE (NCRONTAB, default "0 0 6 * * 1-5" = 06:00 UTC = 07:00 Danish winter time, Mon-Fri).
/// Settings (app settings / Key Vault references):
///   DATAVERSE_URL, DATAVERSE_TENANT_ID, DATAVERSE_CLIENT_ID, DATAVERSE_CLIENT_SECRET   (application user in Dataverse)
///   SMART_SOURCE = "api" (default) or "file:&lt;folder&gt;" for test data deployed with the app
///   SMART_API_URL (default https://api.smart.byggefakta.dk), SMART_CLIENT_ID, SMART_CLIENT_SECRET
/// </summary>
public sealed class SmartSyncFunction
{
    private readonly ILogger<SmartSyncFunction> _log;
    public SmartSyncFunction(ILogger<SmartSyncFunction> log) { _log = log; }

    [Function("SmartSync")]
    public async Task Run([TimerTrigger("%SMART_SCHEDULE%")] TimerInfo timer, CancellationToken ct)
    {
        string Setting(string name, string? fallback = null) =>
            Environment.GetEnvironmentVariable(name) ?? fallback ?? throw new InvalidOperationException($"App setting {name} is missing");

        var env = Setting("DATAVERSE_URL");
        var source = Setting("SMART_SOURCE", "api");
        ISmartSource smart = source.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
            ? new FileSmartSource(Path.Combine(AppContext.BaseDirectory, source[5..]))
            : new HttpSmartSource(new HttpClient { BaseAddress = new Uri(Setting("SMART_API_URL", "https://api.smart.byggefakta.dk")) },
                Setting("SMART_CLIENT_ID"), Setting("SMART_CLIENT_SECRET"));
        var tokens = new ClientSecretTokenProvider(Setting("DATAVERSE_TENANT_ID"), Setting("DATAVERSE_CLIENT_ID"), Setting("DATAVERSE_CLIENT_SECRET"), env);

        var runner = new SyncRunner(smart, new DataverseClient(env, tokens), m => _log.LogInformation("{Message}", m));
        var result = await runner.RunAsync(new SyncOptions(), ct);
        if (result.Failed > 0) _log.LogWarning("SMART sync finished with {Failed} failed project(s); they are retried next run.", result.Failed);
    }
}
