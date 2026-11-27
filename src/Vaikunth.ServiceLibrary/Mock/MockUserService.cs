using Microsoft.Extensions.Configuration;
using Vaikunth.DtoLibrary;

namespace Vaikunth.ServiceLibrary.Mock;

/// <summary>
/// Used while there is no database (ConnectionStrings:Default empty). Everything is in memory, per app.
///   • Mock:SuperAdminEmail (default superadmin@vaikunth.local) signs in as the super admin.
///   • The sample client "Acme Corp" has people such as priya.sharma@acme.com (its admin).
///   • People added on the Clients or People pages sign in with the password they set from their email link.
///   • Any other email signs in as an admin of Acme Corp.
/// Mock:Password (default "vaikunth123") is the password of the super admin, the samples and the "any email" sign-in.
/// </summary>
public sealed class MockUserService : IUserService
{
    public static readonly Guid DemoUserId = Guid.Parse("6f1c2b0e-0d5a-4e8f-9c11-2a3b4c5d6e70");
    private readonly string _password;
    // the super admin's sign-in; My account can change these until the app restarts (then appsettings Mock:* again)
    private string _superEmail;
    private string _superPassword;
    private string _superName = "Super Admin";

    public MockUserService(IConfiguration config)
    {
        _password = config["Mock:Password"] is { Length: > 0 } p ? p : "vaikunth123";
        _superEmail = config["Mock:SuperAdminEmail"] is { Length: > 0 } e ? e : "superadmin@vaikunth.local";
        _superPassword = _password;
    }

    public Task<LoginResult> SignInAsync(LoginRequest request)
    {
        var email = (request.Email ?? "").Trim();
        if (email.Length == 0) return Fail("Enter your email.");

        if (string.Equals(email, _superEmail, StringComparison.OrdinalIgnoreCase))
            return request.Password == _superPassword
                ? Ok(new UserDto(MockData.SuperAdminId, null, null, _superName, _superEmail, AppRoles.SuperAdmin, 4, DateTime.UtcNow))
                : Fail();

        if (MockData.People.TryGetValue(email, out var person))
        {
            if (person.Password is null) return Fail("You haven't set a password yet. Use the link in your welcome email, or Forgot password to get a new one.");
            if (person.Password != request.Password) return Fail();
            if (!person.IsActive) return Fail("This account is switched off. Ask your admin.");
            var t = MockData.Tenants[person.TenantId!.Value];
            if (!t.IsActive) return Fail("Your company's Vaikunth Office account is switched off. Ask your admin to contact us.");
            if (t.BillingBlocked) return Fail(BillingRules.BlockedMessage);
            person.LastLoginUtc = DateTime.UtcNow;
            return Ok(new UserDto(person.UserId, t.TenantId, t.Name, person.DisplayName, person.Email, person.Role, Passwords.ToneFor(email), DateTime.UtcNow, t.Apps));
        }

        if (request.Password != _password) return Fail();
        var acme = MockData.Tenants[MockData.AcmeId];
        return Ok(new UserDto(DemoUserId, acme.TenantId, acme.Name, NameFromEmail(email), email, AppRoles.TenantAdmin, Passwords.ToneFor(email), DateTime.UtcNow, acme.Apps));
    }

    private static Task<LoginResult> Ok(UserDto u) => Task.FromResult(new LoginResult(true, u));
    private static Task<LoginResult> Fail(string? why = null) =>
        Task.FromResult(new LoginResult(false, Error: why ?? "That email and password don't match."));

    public Task<UserDto?> GetAsync(Guid userId)
    {
        var p = MockData.People.Values.FirstOrDefault(x => x.UserId == userId);
        return Task.FromResult<UserDto?>(p is null ? null : new UserDto(p.UserId, p.TenantId, null, p.DisplayName, p.Email, p.Role, 1, p.CreatedUtc));
    }

    public Task<OperationResult> CreateAsync(CreateUserRequest request)
    {
        if (Passwords.Validate(request.Password) is string problem) return Task.FromResult(OperationResult.Fail(problem));
        var person = new MockData.Person { TenantId = request.TenantId, DisplayName = request.DisplayName, Email = request.Email.Trim(), Role = request.Role, Password = request.Password };
        return Task.FromResult(MockData.People.TryAdd(person.Email, person) ? OperationResult.Ok(person.UserId) : OperationResult.Fail("That email is already used."));
    }

    /// <summary>In mock mode the super admin always exists (Mock:SuperAdminEmail).</summary>
    public Task<bool> SetupNeededAsync() => Task.FromResult(false);

    public Task<List<TeamMemberDto>> ListForTenantAsync(Guid tenantId) => Task.FromResult(MockData.People.Values
        .Where(p => p.TenantId == tenantId)
        .OrderBy(p => p.Role == AppRoles.TenantAdmin ? 0 : 1).ThenBy(p => p.DisplayName)
        .Select(p => new TeamMemberDto(p.UserId, p.DisplayName, p.Email, p.Role, Passwords.ToneFor(p.Email), p.IsActive, p.Password is null,
            p.CreatedUtc, p.LastLoginUtc, p.InvitedByName, MockUserPhotoService.Photos.TryGetValue(p.UserId, out var ph) ? ph.Version : 0,
            p.Department, p.JobTitle, p.JoiningDate))
        .ToList());

    public Task<OperationResult> InviteAsync(InviteRequest r)
    {
        if (MemberRules.Validate(r.DisplayName, r.Department, r.JobTitle, r.JoiningDate) is string problem) return Task.FromResult(OperationResult.Fail(problem));
        if (string.IsNullOrWhiteSpace(r.Email) || !r.Email.Contains('@')) return Task.FromResult(OperationResult.Fail("Enter a valid email."));
        if (MockData.Tenants.TryGetValue(r.TenantId, out var t) &&
            t.ExtraUsers + MockData.People.Values.Count(p => p.TenantId == r.TenantId && p.IsActive) >= MockData.MaxUsers(t))
            return Task.FromResult(OperationResult.Fail("All your company's licenses are in use. Ask Vaikunth Office for more licenses, or switch off someone who has left."));
        var by = MockData.People.Values.FirstOrDefault(p => p.UserId == r.InvitedBy)?.DisplayName;
        var person = new MockData.Person
        {
            TenantId = r.TenantId, DisplayName = r.DisplayName.Trim(), Email = r.Email.Trim().ToLowerInvariant(),
            Role = r.Role == AppRoles.TenantAdmin ? AppRoles.TenantAdmin : AppRoles.Member, InvitedByName = by ?? "Your admin",
            Department = MemberRules.Clean(r.Department), JobTitle = MemberRules.Clean(r.JobTitle), JoiningDate = r.JoiningDate
        };
        return Task.FromResult(MockData.People.TryAdd(person.Email, person) ? OperationResult.Ok(person.UserId) : OperationResult.Fail("That email is already used."));
    }

    public Task<OperationResult> UpdateMemberAsync(UpdateMemberRequest r)
    {
        if (MemberRules.Validate(r.DisplayName, r.Department, r.JobTitle, r.JoiningDate) is string problem) return Task.FromResult(OperationResult.Fail(problem));
        var p = MockData.People.Values.FirstOrDefault(x => x.UserId == r.UserId && x.TenantId == r.TenantId);
        if (p is null) return Task.FromResult(OperationResult.Fail("That person isn't in your company."));
        p.DisplayName = r.DisplayName.Trim(); p.Department = MemberRules.Clean(r.Department); p.JobTitle = MemberRules.Clean(r.JobTitle); p.JoiningDate = r.JoiningDate;
        return Task.FromResult(OperationResult.Ok(p.UserId));
    }

    public Task SetActiveAsync(Guid tenantId, Guid userId, bool isActive)
    {
        var p = MockData.People.Values.FirstOrDefault(x => x.UserId == userId && x.TenantId == tenantId);
        if (p is not null) p.IsActive = isActive;
        return Task.CompletedTask;
    }

    public Task SetRoleAsync(Guid tenantId, Guid userId, string role)
    {
        var p = MockData.People.Values.FirstOrDefault(x => x.UserId == userId && x.TenantId == tenantId);
        if (p is not null && (role == AppRoles.TenantAdmin || role == AppRoles.Member)) p.Role = role;
        return Task.CompletedTask;
    }

    public Task<UserLookupDto?> FindByEmailAsync(string email)
    {
        if (!MockData.People.TryGetValue((email ?? "").Trim(), out var p)) return Task.FromResult<UserLookupDto?>(null);
        var t = MockData.Tenants[p.TenantId!.Value];
        return Task.FromResult<UserLookupDto?>(new UserLookupDto(p.UserId, t.TenantId, t.Name, p.DisplayName, p.Email, p.Role, p.IsActive, t.IsActive && !t.BillingBlocked, p.Password is null));
    }

    /// <summary>"priya.sharma@acme.com" → "Priya Sharma".</summary>
    private static string NameFromEmail(string email)
    {
        var local = email.Split('@')[0];
        var parts = local.Split(['.', '_', '-'], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => char.ToUpperInvariant(s[0]) + s[1..]);
        var name = string.Join(' ', parts);
        return name.Length == 0 ? "Demo User" : name;
    }

    public Task<OperationResult> UpdateAccountAsync(Guid userId, string displayName, string email, string? newPassword)
    {
        email = email.Trim();
        var taken = MockData.People.ContainsKey(email) && !string.Equals(MockData.People[email].UserId.ToString(), userId.ToString())
                    || (userId != MockData.SuperAdminId && string.Equals(email, _superEmail, StringComparison.OrdinalIgnoreCase));
        if (taken) return Task.FromResult(OperationResult.Fail("Someone else already uses that email."));

        if (userId == MockData.SuperAdminId)
        {
            _superName = displayName.Trim();
            _superEmail = email;
            if (!string.IsNullOrEmpty(newPassword)) _superPassword = newPassword;
            return Task.FromResult(OperationResult.Ok(userId));
        }
        var person = MockData.People.Values.FirstOrDefault(x => x.UserId == userId);
        if (person is null)
            return Task.FromResult(OperationResult.Fail("This sample sign-in can't be changed. Try it with priya.sharma@acme.com or the super admin."));
        MockData.People.TryRemove(person.Email, out _);
        person.DisplayName = displayName.Trim();
        person.Email = email;
        if (!string.IsNullOrEmpty(newPassword)) person.Password = newPassword;
        MockData.People[email] = person;
        return Task.FromResult(OperationResult.Ok(userId));
    }
}
