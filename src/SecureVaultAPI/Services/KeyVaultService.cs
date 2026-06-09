using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace SecureVaultAPI.Services;

public class KeyVaultService : IKeyVaultService
{
    private readonly SecretClient _client;

    public KeyVaultService(IConfiguration config)
    {
        var vaultUri = config["KeyVault:VaultUri"]
            ?? throw new InvalidOperationException("KeyVault:VaultUri not configured");
        _client = new SecretClient(new Uri(vaultUri), new DefaultAzureCredential());
    }

    public async Task<string> GetSecretAsync(string secretName)
    {
        var secret = await _client.GetSecretAsync(secretName);
        return secret.Value.Value;
    }
}