using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using Vaikunth.DtoLibrary;
using Vaikunth.ServiceLibrary.Data;

namespace Vaikunth.ServiceLibrary;

/// <summary>
/// One-time "set your password" links (Vaikunth_PasswordLinks). The emailed link carries a random secret;
/// only its SHA-256 hash is stored.
/// </summary>
public interface IPasswordLinkService
{
    /// <summary>A new link's secret, or null when one was made for this person less than a minute ago.</summary>
    Task<string?> CreateAsync(Guid userId, PasswordLinkPurpose purpose);
    Task<PasswordLinkInfo?> GetAsync(string token);
    Task<PasswordLinkUseResult> UseAsync(string token, string newPassword);
}

public static class PasswordTokens
{
    public static TimeSpan Lifetime(PasswordLinkPurpose p) => p == PasswordLinkPurpose.Invite ? TimeSpan.FromDays(7) : TimeSpan.FromHours(2);

    /// <summary>32 random bytes, URL-safe.</summary>
    public static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] Hash(string token) => SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token ?? ""));
}

public sealed class PasswordLinkService : IPasswordLinkService
{
    private readonly SqlDb _db;
    public PasswordLinkService(SqlDb db) => _db = db;

    public async Task<string?> CreateAsync(Guid userId, PasswordLinkPurpose purpose)
    {
        var token = PasswordTokens.NewToken();
        var rows = await _db.QueryAsync("dbo.Vaikunth_PasswordLink_Create", p => p
            .With("@UserId", userId).With("@TokenHash", PasswordTokens.Hash(token)).With("@Purpose", (byte)purpose)
            .AddDateTime2("@ExpiresUtc", DateTime.UtcNow + PasswordTokens.Lifetime(purpose)), r => r.I("Result"));
        return rows.FirstOrDefault() == 0 ? token : null;
    }

    public Task<PasswordLinkInfo?> GetAsync(string token) =>
        _db.QueryFirstAsync("dbo.Vaikunth_PasswordLink_Get", p => p.With("@TokenHash", PasswordTokens.Hash(token)), r => new PasswordLinkInfo(
            (PasswordLinkPurpose)Convert.ToByte(r["Purpose"]), r.Utc("ExpiresUtc"), r.UtcN("UsedUtc"), r.S("DisplayName"), r.S("Email"),
            r.B("IsActive"), r.SN("TenantName"), r.B("TenantActive")));

    public async Task<PasswordLinkUseResult> UseAsync(string token, string newPassword)
    {
        var rows = await _db.QueryAsync("dbo.Vaikunth_PasswordLink_Use",
            p => p.With("@TokenHash", PasswordTokens.Hash(token)).AddText("@PasswordHash", Passwords.Hash(newPassword), 400), r => r.I("Result"));
        return (PasswordLinkUseResult)rows.FirstOrDefault(1);
    }
}
