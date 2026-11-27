using Vaikunth.DtoLibrary;

namespace Vaikunth.UI.Infrastructure;

/// <summary>Name, colours and icon of one app in the suite.</summary>
public sealed record SuiteAppInfo(SuiteApp App, string Key, string Name, string Kind, string Color, string Tint, string Icon, string Tagline)
{
    public string IconUrl => $"/_content/Vaikunth.UI/images/{Icon}";
}

public static class SuiteApps
{
    public static readonly SuiteAppInfo Pages = new(SuiteApp.Pages, "pages", "Akshara Pages", "Word",
        "#1E2060", "#E8E9F7", "app-pages.svg", "Write documents, letters and reports");
    public static readonly SuiteAppInfo Workbook = new(SuiteApp.Workbook, "workbook", "Hrishikesha Workbook", "Excel",
        "#115C45", "#E3F4EC", "app-workbook.svg", "Sheets, formulas and numbers that add up");
    public static readonly SuiteAppInfo Slides = new(SuiteApp.Slides, "slides", "Vishwaroopa Slides", "PowerPoint",
        "#C24A1C", "#FBEAE2", "app-slides.svg", "Slides that tell the whole story");
    public static readonly SuiteAppInfo Reader = new(SuiteApp.Reader, "reader", "Shashvata Reader", "PDF",
        "#8E1B3A", "#F8E4EA", "app-reader.svg", "Read PDFs, keep notes and bookmarks");
    public static readonly SuiteAppInfo Planner = new(SuiteApp.Planner, "planner", "Dhruva Planner", "Projects",
        "#5B2A86", "#EEE6F7", "app-planner.svg", "Plan projects on a board and a timeline");

    /// <summary>In the order the person listed them: documents, presentations, spreadsheets, PDFs, projects.</summary>
    public static readonly IReadOnlyList<SuiteAppInfo> All = [Pages, Slides, Workbook, Reader, Planner];

    /// <summary>The landing page, People, Clients, sign-in: pages that belong to the whole suite, not one app.</summary>
    public static readonly SuiteAppInfo Suite = new(SuiteApp.Pages, "suite", "Vaikunth Office", "Office suite",
        "#1E2060", "#E8E9F7", "vaikunth-logo.svg", "Documents, slides, workbooks, PDFs and projects for your whole office");

    /// <summary>The kind of file the Download button makes for an app.</summary>
    public static string DownloadExt(SuiteApp app) => app switch
    {
        SuiteApp.Workbook or SuiteApp.Planner => ".xlsx",
        SuiteApp.Slides => ".pptx",
        SuiteApp.Reader => ".pdf",
        _ => ".docx"
    };

    public static SuiteAppInfo For(SuiteApp app) => app switch
    {
        SuiteApp.Workbook => Workbook,
        SuiteApp.Slides => Slides,
        SuiteApp.Reader => Reader,
        SuiteApp.Planner => Planner,
        _ => Pages
    };

    public static SuiteAppInfo For(string? key) =>
        All.FirstOrDefault(a => string.Equals(a.Key, key, StringComparison.OrdinalIgnoreCase) || string.Equals(a.App.ToString(), key, StringComparison.OrdinalIgnoreCase)) ?? Pages;
}

/// <summary>appsettings "Suite": sign-in keys and the address used in emailed links.</summary>
public sealed class SuiteOptions
{
    public string DataProtectionKeysPath { get; set; } = "";
    /// <summary>The address people use, for links in emails (e.g. https://pages.yourcompany.com). Empty = this request's address.</summary>
    public string PublicBaseUrl { get; set; } = "";

    /// <summary>The "set your password" page for an emailed link.</summary>
    public string SetPasswordUrl(Microsoft.AspNetCore.Http.HttpRequest request, string token)
    {
        var baseUrl = string.IsNullOrWhiteSpace(PublicBaseUrl) ? $"{request.Scheme}://{request.Host}" : PublicBaseUrl.TrimEnd('/');
        return $"{baseUrl}/account/set-password?token={Uri.EscapeDataString(token)}";
    }

}

/// <summary>
/// Which part of Vaikunth Office this request is in: /pages, /slides, /workbook, /reader and /planner are the five apps,
/// everything else (landing page, People, Clients, sign-in) is the suite itself.
/// </summary>
public sealed record CurrentApp(SuiteAppInfo Info, bool IsSuite)
{
    public static CurrentApp ForPath(string? path)
    {
        var first = (path ?? "").Trim('/').Split('/', 2)[0];
        var app = SuiteApps.All.FirstOrDefault(a => string.Equals(a.Key, first, StringComparison.OrdinalIgnoreCase));
        return app is null ? new CurrentApp(SuiteApps.Suite, true) : new CurrentApp(app, false);
    }

    /// <summary>Address of this app's editor, e.g. /workbook/editor or /workbook/editor?id=...</summary>
    public static string EditorUrl(SuiteApp app, Guid? id = null) =>
        $"/{SuiteApps.For(app).Key}/editor{(id is null ? "" : "?id=" + id)}";

    public static string NewUrl(SuiteApp app) => $"/{SuiteApps.For(app).Key}/editor/new";
}
