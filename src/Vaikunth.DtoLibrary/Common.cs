namespace Vaikunth.DtoLibrary;

/// <summary>The three apps of Vaikunth Office. The byte value is what the database stores in Vaikunth_Documents.AppKind.</summary>
public enum SuiteApp : byte
{
    /// <summary>Akshara Pages: documents (Word).</summary>
    Pages = 1,
    /// <summary>Hrishikesha Workbook: spreadsheets (Excel).</summary>
    Workbook = 2,
    /// <summary>Vishwaroopa Slides: presentations (PowerPoint).</summary>
    Slides = 3
}

public static class AppRoles
{
    /// <summary>Runs Vaikunth Office itself: creates and manages clients. Belongs to no client (TenantId NULL).</summary>
    public const string SuperAdmin = "SuperAdmin";
    /// <summary>Admin of one client company.</summary>
    public const string TenantAdmin = "TenantAdmin";
    public const string Member = "Member";
}

public static class AppClaims
{
    public const string DisplayName = "display_name";
    public const string AvatarTone = "avatar_tone";
    public const string TenantId = "tenant_id";
    public const string TenantName = "tenant_name";
    /// <summary>Which apps the client may use, as <see cref="SuiteAppFlags"/> (number).</summary>
    public const string Apps = "apps";
    /// <summary>When the person's profile photo last changed (ticks); missing or 0 = no photo, show initials.</summary>
    public const string PhotoVersion = "photo_v";
}

/// <summary>Which of the three apps a client has (Vaikunth_Tenants.AppsEnabled). 7 = all three.</summary>
[Flags]
public enum SuiteAppFlags : byte
{
    None = 0,
    Pages = 1,
    Workbook = 2,
    Slides = 4,
    All = Pages | Workbook | Slides
}

public static class SuiteAppFlagsExtensions
{
    public static SuiteAppFlags Flag(this SuiteApp app) => app switch
    {
        SuiteApp.Pages => SuiteAppFlags.Pages,
        SuiteApp.Workbook => SuiteAppFlags.Workbook,
        _ => SuiteAppFlags.Slides
    };

    public static bool Has(this SuiteAppFlags flags, SuiteApp app) => (flags & app.Flag()) != 0;
}

/// <summary>Result of a create/update call: success, or a message to show the user.</summary>
public sealed record OperationResult(bool Succeeded, string? Error = null, Guid? Id = null)
{
    public static OperationResult Ok(Guid? id = null) => new(true, null, id);
    public static OperationResult Fail(string error) => new(false, error);
}

public static class Names
{
    /// <summary>"Priya Sharma" → "PS", "Ravi" → "RA". Used for the round avatars.</summary>
    public static string Initials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "?";
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return "?";
        return parts.Length == 1
            ? parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant()
            : $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[^1][0])}";
    }
}
