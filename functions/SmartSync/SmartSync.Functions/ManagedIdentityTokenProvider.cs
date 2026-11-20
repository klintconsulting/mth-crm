using Azure.Core;
using Azure.Identity;

namespace Mth.SmartSync.Functions;

/// <summary>
/// Dataverse token from the Function App's system-assigned managed identity (no secret anywhere).
/// The identity is registered in Dataverse as an application user (see scripts/Deploy-SmartSyncAzure.ps1).
/// </summary>
public sealed class ManagedIdentityTokenProvider : ITokenProvider
{
    private readonly TokenCredential _credential = new ManagedIdentityCredential(ManagedIdentityId.SystemAssigned);
    private readonly string[] _scopes;

    public ManagedIdentityTokenProvider(string environmentUrl) { _scopes = new[] { environmentUrl.TrimEnd('/') + "/.default" }; }

    public async Task<string> GetTokenAsync(CancellationToken ct) =>
        (await _credential.GetTokenAsync(new TokenRequestContext(_scopes), ct)).Token; // Azure.Identity caches and refreshes
}
