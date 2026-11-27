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

    public static readonly IReadOnlyList<SuiteAppInfo> All = [Pages, Workbook, Slides];

    public static SuiteAppInfo For(SuiteApp app) => app switch
    {
        SuiteApp.Workbook => Workbook,
        SuiteApp.Slides => Slides,
        _ => Pages
    };

    public static SuiteAppInfo For(string? key) =>
        All.FirstOrDefault(a => string.Equals(a.Key, key, StringComparison.OrdinalIgnoreCase) || string.Equals(a.App.ToString(), key, StringComparison.OrdinalIgnoreCase)) ?? Pages;
}

/// <summary>appsettings "Suite": where each app runs, so the launcher can link across them.</summary>
public sealed class SuiteOptions
{
    public string PagesUrl { get; set; } = "http://localhost:5101";
    public string WorkbookUrl { get; set; } = "http://localhost:5102";
    public string SlidesUrl { get; set; } = "http://localhost:5103";
    public string DataProtectionKeysPath { get; set; } = "";
    /// <summary>The address people use, for links in emails (e.g. https://pages.yourcompany.com). Empty = this request's address.</summary>
    public string PublicBaseUrl { get; set; } = "";

    /// <summary>The "set your password" page for an emailed link.</summary>
    public string SetPasswordUrl(Microsoft.AspNetCore.Http.HttpRequest request, string token)
    {
        var baseUrl = string.IsNullOrWhiteSpace(PublicBaseUrl) ? $"{request.Scheme}://{request.Host}" : PublicBaseUrl.TrimEnd('/');
        return $"{baseUrl}/account/set-password?token={Uri.EscapeDataString(token)}";
    }

    public string UrlFor(SuiteApp app) => (app switch
    {
        SuiteApp.Workbook => WorkbookUrl,
        SuiteApp.Slides => SlidesUrl,
        _ => PagesUrl
    }).TrimEnd('/');
}

/// <summary>Which app is running in this process (each web project registers its own).</summary>
public sealed record CurrentApp(SuiteAppInfo Info);
