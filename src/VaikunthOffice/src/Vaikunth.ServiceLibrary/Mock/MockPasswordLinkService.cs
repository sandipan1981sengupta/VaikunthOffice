using Vaikunth.DtoLibrary;

namespace Vaikunth.ServiceLibrary.Mock;

/// <summary>Password links in memory, with the same rules as the SQL version (one a minute, one use, expiry).</summary>
public sealed class MockPasswordLinkService : IPasswordLinkService
{
    private static string Key(string token) => Convert.ToHexString(PasswordTokens.Hash(token));
    private static MockData.Person? PersonOf(Guid userId) => MockData.People.Values.FirstOrDefault(p => p.UserId == userId);

    public Task<string?> CreateAsync(Guid userId, PasswordLinkPurpose purpose)
    {
        var now = DateTime.UtcNow;
        if (MockData.Links.Values.Any(l => l.UserId == userId && l.CreatedUtc > now.AddSeconds(-60))) return Task.FromResult<string?>(null);
        foreach (var old in MockData.Links.Values.Where(l => l.UserId == userId && l.UsedUtc is null)) old.UsedUtc = now;
        var token = PasswordTokens.NewToken();
        MockData.Links[Key(token)] = new MockData.Link(userId, purpose, now, now + PasswordTokens.Lifetime(purpose));
        return Task.FromResult<string?>(token);
    }

    public Task<PasswordLinkInfo?> GetAsync(string token)
    {
        if (!MockData.Links.TryGetValue(Key(token), out var l) || PersonOf(l.UserId) is not { } p) return Task.FromResult<PasswordLinkInfo?>(null);
        var t = MockData.Tenants[p.TenantId!.Value];
        return Task.FromResult<PasswordLinkInfo?>(new PasswordLinkInfo(l.Purpose, l.ExpiresUtc, l.UsedUtc, p.DisplayName, p.Email, p.IsActive, t.Name, t.IsActive));
    }

    public Task<PasswordLinkUseResult> UseAsync(string token, string newPassword)
    {
        if (!MockData.Links.TryGetValue(Key(token), out var l) || l.UsedUtc is not null || l.ExpiresUtc <= DateTime.UtcNow || PersonOf(l.UserId) is not { } p)
            return Task.FromResult(PasswordLinkUseResult.Invalid);
        if (!p.IsActive || !MockData.Tenants[p.TenantId!.Value].IsActive) return Task.FromResult(PasswordLinkUseResult.Inactive);
        p.Password = newPassword;
        l.UsedUtc = DateTime.UtcNow;
        return Task.FromResult(PasswordLinkUseResult.Done);
    }
}
