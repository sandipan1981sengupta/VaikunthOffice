namespace Vaikunth.DtoLibrary;

/// <summary>A signed-in person. TenantId is null for the super admin.</summary>
public sealed record UserDto(
    Guid UserId, Guid? TenantId, string? TenantName, string DisplayName, string Email, string Role, int AvatarTone,
    DateTime CreatedUtc, SuiteAppFlags Apps = SuiteAppFlags.All);

public sealed class LoginRequest
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
}

/// <summary>What a sign-in check returns: the user, or the reason it failed.</summary>
public sealed record LoginResult(bool Succeeded, UserDto? User = null, string? Error = null);

public sealed class CreateUserRequest
{
    /// <summary>Null only for the super admin.</summary>
    public Guid? TenantId { get; set; }
    public string DisplayName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string Role { get; set; } = AppRoles.Member;
}

/// <summary>One person on a client admin's People page.</summary>
public sealed record TeamMemberDto(
    Guid UserId, string DisplayName, string Email, string Role, int AvatarTone, bool IsActive,
    /// <summary>Added but hasn't set a password from the email link yet.</summary>
    bool IsInvited, DateTime CreatedUtc, DateTime? LastLoginUtc, string? InvitedByName, long PhotoVersion = 0,
    string? Department = null, string? JobTitle = null, DateOnly? JoiningDate = null)
{
    public string Status => !IsActive ? "Off" : IsInvited ? "Invited" : "Active";
}

/// <summary>A client admin adds someone; they get an email with a link to set their password.</summary>
public sealed class InviteRequest
{
    public Guid TenantId { get; set; }
    public Guid InvitedBy { get; set; }
    public string DisplayName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = AppRoles.Member;
    public string? Department { get; set; }
    /// <summary>Their job, e.g. "Accountant" (Role above is what they may do in Vaikunth Desk).</summary>
    public string? JobTitle { get; set; }
    public DateOnly? JoiningDate { get; set; }
}

/// <summary>A client admin corrects a person's details on the People page (Vaikunth_User_UpdateDetails).</summary>
public sealed class UpdateMemberRequest
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = "";
    public string? Department { get; set; }
    public string? JobTitle { get; set; }
    public DateOnly? JoiningDate { get; set; }
}

/// <summary>Checks shared by the SQL and mock services.</summary>
public static class MemberRules
{
    public static string? Validate(string? displayName, string? department, string? jobTitle, DateOnly? joiningDate)
    {
        if (string.IsNullOrWhiteSpace(displayName)) return "Enter the person's name.";
        if ((department?.Length ?? 0) > 80 || (jobTitle?.Length ?? 0) > 80) return "Department and job title can be at most 80 characters.";
        if (joiningDate is DateOnly d && (d.Year < 1950 || d > DateOnly.FromDateTime(DateTime.Today.AddYears(1))))
            return "Check the joining date.";
        return null;
    }

    public static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

/// <summary>The person behind an email (for "Forgot password?").</summary>
public sealed record UserLookupDto(Guid UserId, Guid? TenantId, string? TenantName, string DisplayName, string Email, string Role, bool IsActive, bool TenantActive, bool IsInvited);

public enum PasswordLinkPurpose : byte
{
    /// <summary>Welcome email for a new client admin or an invited member. Valid 7 days.</summary>
    Invite = 1,
    /// <summary>Forgot password, or an admin sending a reset link. Valid 2 hours.</summary>
    Reset = 2
}

/// <summary>What a set-password link points at (for showing the page).</summary>
public sealed record PasswordLinkInfo(
    PasswordLinkPurpose Purpose, DateTime ExpiresUtc, DateTime? UsedUtc, string DisplayName, string Email,
    bool IsActive, string? TenantName, bool TenantActive)
{
    public bool IsUsable => UsedUtc is null && ExpiresUtc > DateTime.UtcNow && IsActive && TenantActive;
}

/// <summary>0 password set · 1 link not valid (wrong, used or expired) · 2 account or client switched off.</summary>
public enum PasswordLinkUseResult { Done = 0, Invalid = 1, Inactive = 2 }

/// <summary>A profile photo as stored in Vaikunth_Users.Photo. Version changes every time the photo does (for caching).</summary>
public sealed record UserPhotoDto(byte[] Data, string ContentType, long Version);
