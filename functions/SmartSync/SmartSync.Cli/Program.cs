using Mth.SmartSync;

// Local runner for the SMART sync.
//   smart-sync --env https://orgxxxx.crm4.dynamics.com --source file:..\testdata [--dry-run]
//   smart-sync --env ... --source api          (needs SMART_CLIENT_ID / SMART_CLIENT_SECRET)
// Dataverse login: the device-code token cache of scripts/Dataverse.psm1 (Connect-Dataverse), or an app registration
// via DATAVERSE_TENANT_ID / DATAVERSE_CLIENT_ID / DATAVERSE_CLIENT_SECRET.

string? Arg(string name) { var i = Array.IndexOf(args, name); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
var env = Arg("--env") ?? Environment.GetEnvironmentVariable("DATAVERSE_URL");
var source = Arg("--source") ?? "file:testdata";
if (string.IsNullOrWhiteSpace(env)) { Console.Error.WriteLine("Missing --env <dataverse url>"); return 2; }

ISmartSource smart = source.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
    ? new FileSmartSource(Path.GetFullPath(source[5..]))
    : new HttpSmartSource(new HttpClient { BaseAddress = new Uri(Environment.GetEnvironmentVariable("SMART_API_URL") ?? "https://api.smart.byggefakta.dk") },
        Environment.GetEnvironmentVariable("SMART_CLIENT_ID") ?? throw new InvalidOperationException("SMART_CLIENT_ID missing"),
        Environment.GetEnvironmentVariable("SMART_CLIENT_SECRET") ?? throw new InvalidOperationException("SMART_CLIENT_SECRET missing"));

var clientId = Environment.GetEnvironmentVariable("DATAVERSE_CLIENT_ID");
ITokenProvider tokens = string.IsNullOrEmpty(clientId)
    ? new DevTokenProvider(env)
    : new ClientSecretTokenProvider(Environment.GetEnvironmentVariable("DATAVERSE_TENANT_ID")!, clientId, Environment.GetEnvironmentVariable("DATAVERSE_CLIENT_SECRET")!, env);

var runner = new SyncRunner(smart, new DataverseClient(env, tokens), Console.WriteLine);
var result = await runner.RunAsync(new SyncOptions { DryRun = args.Contains("--dry-run"), StateName = Arg("--state") ?? "SMART Projects" });
return result.Failed > 0 ? 1 : 0;
