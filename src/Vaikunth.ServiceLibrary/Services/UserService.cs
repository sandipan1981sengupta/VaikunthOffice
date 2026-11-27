using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Vaikunth.DtoLibrary;
using Vaikunth.ServiceLibrary.Data;

namespace Vaikunth.ServiceLibrary;

/// <summary>Password hashing with ASP.NET's built-in PBKDF2 hasher. Only hashes are stored in Vaikunth_Users.</summary>
public static class Passwords
{
    private sealed class Account { }
    private static readonly PasswordHasher<Account> Hasher = new();
    private static readonly Account Anyone = new();

    public static string Hash(string password) => Hasher.HashPassword(Anyone, password);

    public static bool Verify(string hash, string password) =>
        Hasher.VerifyHashedPassword(Anyone, hash, password) != PasswordVerificationResult.Failed;

    public static string? Validate(string password) =>
        string.IsNullOrEmpty(password) || password.Length < 8 ? "Password must be at least 8 characters." : null;

    /// <summary>Avatar colour 0–5, stable for an email address.</summary>
    public static int ToneFor(string email)
    {
        var sum = 0;
        foreach (var ch in (email ?? "").ToLowerInvariant()) sum += ch;
        return sum % 6;
    }
}

public interface IUserService
{
    Task<LoginResult> SignInAsync(LoginRequest request);
    Task<UserDto?> GetAsync(Guid userId);
    Task<OperationResult> CreateAsync(CreateUserRequest request);
    /// <summary>True until the super admin exists (the /setup page).</summary>
    Task<bool> SetupNeededAsync();

    // ---- the client admin's People page ----
    Task<List<TeamMemberDto>> ListForTenantAsync(Guid tenantId);
    /// <summary>Adds a person with no password. Send them an invite link next.</summary>
    Task<OperationResult> InviteAsync(InviteRequest request);
    /// <summary>Name, department, job title and joining date of someone in the admin's own client.</summary>
    Task<OperationResult> UpdateMemberAsync(UpdateMemberRequest request);
    Task SetActiveAsync(Guid tenantId, Guid userId, bool isActive);
    Task SetRoleAsync(Guid tenantId, Guid userId, string role);
    Task<UserLookupDto?> FindByEmailAsync(string email);

    // ---- My account (anyone, the super admin too) ----
    /// <summary>Changes the person's own name, email and (when <paramref name="newPassword"/> is set) password.
    /// Check their current password with SignInAsync first. Fails when the email belongs to someone else.</summary>
    Task<OperationResult> UpdateAccountAsync(Guid userId, string displayName, string email, string? newPassword);
}

/// <summary>Users in SQL Server (database/Vaikunth_Update.sql: Vaikunth_User_*).</summary>
public sealed class UserService : IUserService
{
    private readonly SqlDb _db;
    public UserService(SqlDb db) => _db = db;

    private static UserDto Row(SqlDataReader r) => new(
        r.G("UserId"), r.GN("TenantId"), r.SN("TenantName"), r.S("DisplayName"), r.S("Email"), r.S("Role"), r.I("AvatarTone"),
        r.Utc("CreatedUtc"), (SuiteAppFlags)Convert.ToByte(r["AppsEnabled"]));

    public async Task<LoginResult> SignInAsync(LoginRequest request)
    {
        var rows = await _db.QueryAsync("dbo.Vaikunth_User_GetForLogin", p => p.AddText("@Email", request.Email.Trim(), 200),
            r => (User: Row(r), Hash: r.S("PasswordHash"), Active: r.B("IsActive"), TenantActive: r.B("TenantActive"), Unpaid: r.BOpt("TenantBillingBlocked")));
        if (rows.Count > 0 && rows[0].Hash.Length == 0)
            return new LoginResult(false, Error: "You haven't set a password yet. Use the link in your welcome email, or Forgot password to get a new one.");
        if (rows.Count == 0 || !Passwords.Verify(rows[0].Hash, request.Password))
            return new LoginResult(false, Error: "That email and password don't match.");
        if (!rows[0].Active)
            return new LoginResult(false, Error: "This account is switched off. Ask your admin.");
        if (!rows[0].TenantActive)
            return new LoginResult(false, Error: rows[0].Unpaid ? BillingRules.BlockedMessage
                : "Your company's Vaikunth Office account is switched off. Ask your admin to contact us.");
        await _db.ExecAsync("dbo.Vaikunth_User_TouchLogin", p => p.With("@UserId", rows[0].User.UserId));
        return new LoginResult(true, rows[0].User);
    }

    public Task<UserDto?> GetAsync(Guid userId) =>
        _db.QueryFirstAsync("dbo.Vaikunth_User_Get", p => p.With("@UserId", userId), Row);

    public async Task<OperationResult> CreateAsync(CreateUserRequest request)
    {
        if (Passwords.Validate(request.Password) is string problem) return OperationResult.Fail(problem);
        var id = Guid.NewGuid();
        var rows = await _db.QueryAsync("dbo.Vaikunth_User_Create", p => p
            .With("@UserId", id).With("@TenantId", request.TenantId)
            .AddText("@DisplayName", request.DisplayName.Trim(), 120).AddText("@Email", request.Email.Trim(), 200)
            .AddText("@PasswordHash", Passwords.Hash(request.Password), 400).AddText("@Role", request.Role, 20)
            .With("@AvatarTone", Passwords.ToneFor(request.Email)), r => r.I("Result"));
        return rows.FirstOrDefault() switch
        {
            1 => OperationResult.Ok(id),
            -1 => OperationResult.Fail("All of this client's licenses are in use. Raise Licenses on the Clients page."),
            -2 => OperationResult.Fail("The super admin already exists."),
            _ => OperationResult.Fail("That email is already used.")
        };
    }

    public Task<List<TeamMemberDto>> ListForTenantAsync(Guid tenantId) =>
        _db.QueryAsync("dbo.Vaikunth_User_ListForTenant", p => p.With("@TenantId", tenantId), r => new TeamMemberDto(
            r.G("UserId"), r.S("DisplayName"), r.S("Email"), r.S("Role"), r.I("AvatarTone"), r.B("IsActive"), r.B("IsInvited"),
            r.Utc("CreatedUtc"), r.UtcN("LastLoginUtc"), r.SN("InvitedByName"), r.UtcN("PhotoUpdatedUtc")?.Ticks ?? 0,
            r.SN("Department"), r.SN("JobTitle"), r["JoiningDate"] is DateTime j ? DateOnly.FromDateTime(j) : null));

    private static object? Day(DateOnly? d) => d?.ToDateTime(TimeOnly.MinValue);

    public async Task<OperationResult> InviteAsync(InviteRequest request)
    {
        if (MemberRules.Validate(request.DisplayName, request.Department, request.JobTitle, request.JoiningDate) is string problem) return OperationResult.Fail(problem);
        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@')) return OperationResult.Fail("Enter a valid email.");
        var id = Guid.NewGuid();
        var rows = await _db.QueryAsync("dbo.Vaikunth_User_Invite", p => p
            .With("@UserId", id).With("@TenantId", request.TenantId).AddText("@DisplayName", request.DisplayName.Trim(), 120)
            .AddText("@Email", request.Email.Trim(), 200).AddText("@Role", request.Role, 20)
            .With("@AvatarTone", Passwords.ToneFor(request.Email)).With("@InvitedBy", request.InvitedBy)
            .AddText("@Department", MemberRules.Clean(request.Department), 80).AddText("@JobTitle", MemberRules.Clean(request.JobTitle), 80)
            .With("@JoiningDate", Day(request.JoiningDate)), r => r.I("Result"));
        return rows.FirstOrDefault() switch
        {
            1 => OperationResult.Ok(id),
            -1 => OperationResult.Fail("All your company's licenses are in use. Ask Vaikunth Office for more licenses, or switch off someone who has left."),
            _ => OperationResult.Fail("That email is already used.")
        };
    }

    public async Task<OperationResult> UpdateMemberAsync(UpdateMemberRequest request)
    {
        if (MemberRules.Validate(request.DisplayName, request.Department, request.JobTitle, request.JoiningDate) is string problem) return OperationResult.Fail(problem);
        var rows = await _db.QueryAsync("dbo.Vaikunth_User_UpdateDetails", p => p
            .With("@TenantId", request.TenantId).With("@UserId", request.UserId).AddText("@DisplayName", request.DisplayName.Trim(), 120)
            .AddText("@Department", MemberRules.Clean(request.Department), 80).AddText("@JobTitle", MemberRules.Clean(request.JobTitle), 80)
            .With("@JoiningDate", Day(request.JoiningDate)), r => r.I("Result"));
        return rows.FirstOrDefault() == 1 ? OperationResult.Ok(request.UserId) : OperationResult.Fail("That person isn't in your company.");
    }

    public Task SetActiveAsync(Guid tenantId, Guid userId, bool isActive) =>
        _db.ExecAsync("dbo.Vaikunth_User_SetActive", p => p.With("@TenantId", tenantId).With("@UserId", userId).With("@IsActive", isActive));

    public Task SetRoleAsync(Guid tenantId, Guid userId, string role) =>
        _db.ExecAsync("dbo.Vaikunth_User_SetRole", p => p.With("@TenantId", tenantId).With("@UserId", userId).AddText("@Role", role, 20));

    public Task<UserLookupDto?> FindByEmailAsync(string email) =>
        _db.QueryFirstAsync("dbo.Vaikunth_User_GetByEmail", p => p.AddText("@Email", (email ?? "").Trim(), 200), r => new UserLookupDto(
            r.G("UserId"), r.GN("TenantId"), r.SN("TenantName"), r.S("DisplayName"), r.S("Email"), r.S("Role"), r.B("IsActive"), r.B("TenantActive"), r.B("IsInvited")));

    public async Task<bool> SetupNeededAsync() =>
        (await _db.QueryAsync("dbo.Vaikunth_Setup_IsNeeded", null, r => r.B("Needed"))).FirstOrDefault();

    public async Task<OperationResult> UpdateAccountAsync(Guid userId, string displayName, string email, string? newPassword)
    {
        var rows = await _db.QueryAsync("dbo.Vaikunth_User_UpdateAccount", p => p
            .With("@UserId", userId).AddText("@DisplayName", displayName.Trim(), 120).AddText("@Email", email.Trim().ToLowerInvariant(), 200)
            .AddText("@PasswordHash", string.IsNullOrEmpty(newPassword) ? null : Passwords.Hash(newPassword), 400), r => r.I("Result"));
        return rows.FirstOrDefault() switch
        {
            1 => OperationResult.Ok(userId),
            0 => OperationResult.Fail("Someone else already uses that email."),
            _ => OperationResult.Fail("Your account wasn't found."),
        };
    }
}
