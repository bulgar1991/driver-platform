using System.Security.Cryptography;
using System.Text;
using DriverPlatform.Application.Common;
using DriverPlatform.Domain.Entities;
using Microsoft.Extensions.Options;

namespace DriverPlatform.Application.Partners;

public sealed record NewApiKey(string Key, string Prefix, string Hash);

public interface IPartnerKeys
{
    /// <summary>"md_{8 chars}_{43 chars}"; only the prefix and the SHA-256 are stored.</summary>
    NewApiKey Create();
    /// <summary>The prefix of a well-formed key, or null.</summary>
    string? PrefixOf(string key);
    bool Matches(string key, string hash);
    /// <summary>Signs webhooks; derived from the partner id, so it is never stored.</summary>
    string WebhookSecret(Partner partner);
}

public sealed class PartnerKeys(IOptions<LinksOptions> links) : IPartnerKeys
{
    private const string Alphabet = "abcdefghjkmnpqrstuvwxyz23456789";

    public NewApiKey Create()
    {
        var prefix = RandomNumberGenerator.GetString(Alphabet, 8);
        var key = $"md_{prefix}_{Base64Url(RandomNumberGenerator.GetBytes(32))}";
        return new NewApiKey(key, prefix, Hash(key));
    }

    /// <remarks>The secret is base64url and may itself contain "_", so the key is not split on it.</remarks>
    public string? PrefixOf(string key) =>
        key.Length >= 52 && key.StartsWith("md_", StringComparison.Ordinal) && key[11] == '_' ? key[3..11] : null;

    public bool Matches(string key, string hash) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Hash(key)), Encoding.ASCII.GetBytes(hash));

    public string WebhookSecret(Partner partner)
    {
        var mac = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(links.Value.SigningKey),
            Encoding.UTF8.GetBytes($"partner-webhook:{partner.Id:N}:{partner.SecretVersion}"));
        return "whsec_" + Base64Url(mac);
    }

    private static string Hash(string key) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key)));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
