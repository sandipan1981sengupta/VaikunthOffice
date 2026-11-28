namespace Vaikunth.DtoLibrary;

/// <summary>The seven apps of Vaikunth Desk. The byte value is what the database stores in Vaikunth_Documents.AppKind.</summary>
public enum SuiteApp : byte
{
    /// <summary>Akshara Pages: documents (Word).</summary>
    Pages = 1,
    /// <summary>Hrishikesha Workbook: spreadsheets (Excel).</summary>
    Workbook = 2,
    /// <summary>Vishwaroopa Slides: presentations (PowerPoint).</summary>
    Slides = 3,
    /// <summary>Shashvata Reader: PDFs, with notes and bookmarks.</summary>
    Reader = 4,
    /// <summary>Dhruva Planner: projects, tasks, board and timeline.</summary>
    Planner = 5,
    /// <summary>Vaijayanti Days: the calendar (events, meetings, invites).</summary>
    Days = 6,
    /// <summary>Sudarshana Inbox: email between the office's people (and out to other addresses).</summary>
    Inbox = 7
}

public static class AppRoles
{
    /// <summary>Runs Vaikunth Desk itself: creates and manages clients. Belongs to no client (TenantId NULL).</summary>
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

/// <summary>Which apps a client has (Vaikunth_Tenants.AppsEnabled). 127 = all seven.</summary>
[Flags]
public enum SuiteAppFlags : byte
{
    None = 0,
    Pages = 1,
    Workbook = 2,
    Slides = 4,
    Reader = 8,
    Planner = 16,
    Days = 32,
    Inbox = 64,
    All = Pages | Workbook | Slides | Reader | Planner | Days | Inbox
}

public static class SuiteAppFlagsExtensions
{
    public static SuiteAppFlags Flag(this SuiteApp app) => app switch
    {
        SuiteApp.Pages => SuiteAppFlags.Pages,
        SuiteApp.Workbook => SuiteAppFlags.Workbook,
        SuiteApp.Slides => SuiteAppFlags.Slides,
        SuiteApp.Reader => SuiteAppFlags.Reader,
        SuiteApp.Planner => SuiteAppFlags.Planner,
        SuiteApp.Days => SuiteAppFlags.Days,
        SuiteApp.Inbox => SuiteAppFlags.Inbox,
        _ => SuiteAppFlags.None
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
