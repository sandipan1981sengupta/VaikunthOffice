using System.Security.Claims;
using Vaikunth.DtoLibrary;

namespace Vaikunth.UI.Infrastructure;

public static class UserExtensions
{
    public static Guid UserId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    public static string DisplayName(this ClaimsPrincipal user) => user.FindFirstValue(AppClaims.DisplayName) ?? user.Identity?.Name ?? "";

    public static string FirstName(this ClaimsPrincipal user) => user.DisplayName().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";

    public static string Initials(this ClaimsPrincipal user) => Names.Initials(user.DisplayName());

    public static Guid TenantId(this ClaimsPrincipal user) =>
        Guid.TryParse(user.FindFirstValue(AppClaims.TenantId), out var id) ? id : Guid.Empty;

    public static string TenantName(this ClaimsPrincipal user) => user.FindFirstValue(AppClaims.TenantName) ?? "";

    public static bool IsSuperAdmin(this ClaimsPrincipal user) => user.IsInRole(AppRoles.SuperAdmin);

    /// <summary>The apps the person's client has. The super admin sees all three.</summary>
    public static SuiteAppFlags Apps(this ClaimsPrincipal user) =>
        byte.TryParse(user.FindFirstValue(AppClaims.Apps), out var a) ? (SuiteAppFlags)a : SuiteAppFlags.All;

    /// <summary>The signed-in person's photo address, or null when they have none (then initials are shown).</summary>
    public static string? PhotoUrl(this ClaimsPrincipal user) =>
        long.TryParse(user.FindFirstValue(AppClaims.PhotoVersion), out var v) && v > 0 ? PhotoUrl(user.UserId(), v) : null;

    /// <summary>Address of anyone's photo; the version makes browsers fetch it again only after it changes.</summary>
    public static string? PhotoUrl(Guid userId, long version) => version > 0 ? $"/account/photo/{userId:N}?v={version}" : null;

    public static int AvatarTone(this ClaimsPrincipal user) => int.TryParse(user.FindFirstValue(AppClaims.AvatarTone), out var t) ? t : 0;

    /// <summary>"Good morning" etc. by the server's clock (the launcher greeting).</summary>
    public static string Greeting(DateTime now) => now.Hour switch
    {
        < 12 => "Good morning",
        < 17 => "Good afternoon",
        _ => "Good evening"
    };

    /// <summary>"Just now", "3 hours ago", "Yesterday", "12 Nov".</summary>
    public static string Ago(DateTime utc)
    {
        var span = DateTime.UtcNow - utc;
        if (span.TotalMinutes < 1) return "Just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} min ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours} hour{((int)span.TotalHours == 1 ? "" : "s")} ago";
        if (span.TotalHours < 48) return "Yesterday";
        return utc.ToLocalTime().ToString("d MMM");
    }
}
