using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NUglify;

namespace Vaikunth.UI.Infrastructure;

/// <summary>
/// The style and script bundles. Each bundle is one request instead of many: the files are joined in this order,
/// minified (except in Development, so they stay readable while debugging) and cached by the browser for a year;
/// the address carries a hash of the content, so a changed file gives a new address and is fetched again.
/// Paths are as the browser sees them (static web assets of a library are under _content/{Library}/).
/// </summary>
public static class BundleConfig
{
    private const string UI = "_content/Vaikunth.UI/";
    private static readonly string[] ControlNames =
    [
        "modern-table", "modern-input", "modern-dropdown", "modern-dropdown-with-label", "modern-datepicker", "modern-datepicker-with-label",
        "modern-month-year-picker", "modern-radio", "modern-checkbox", "tab-control", "modern-column-chart", "modern-bar-chart",
        "modern-line-chart", "modern-donut-chart"
    ];

    public static readonly IReadOnlyDictionary<string, string[]> All = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        // every page: icons and the suite's styles
        ["site.css"] = [UI + "lib/font-awesome-4.7.0/css/font-awesome.min.css", UI + "css/vaikunth.css"],
        // every page: toasts, message boxes and the suite script (VK)
        ["site.js"] = [UI + "js/web-control/toast.js", UI + "js/web-control/messagebox.js", UI + "js/vaikunth.js"],
        // the workspace pages (super admin): the frame and the ShankhNaad controls
        ["workspace.css"] =
        [
            UI + "css/workspace.css",
            UI + "lib/modern-controls/CSS/flatpickr.min.css", UI + "lib/modern-controls/CSS/flatpickr-monthSelect-4.6.13.css",
            UI + "lib/modern-controls/bootstrap-icon/bootstrap-icons.min.css",
            UI + "css/Theme/controls-theme.css", UI + "css/Theme/flatpickr-theme.css",
            .. ControlNames.Append("cee-group-box-modern").Select(n => UI + "css/web-control/" + n + ".css"),
            UI + "css/web-control/messagebox.css",
            UI + "css/Theme/vaikunth-controls.css"
        ],
        ["workspace.js"] =
        [
            UI + "lib/modern-controls/JS/jquery-3.7.1.min.js", UI + "lib/modern-controls/JS/flatpickr-v4.6.13.js",
            UI + "lib/modern-controls/JS/flatpickr-monthSelect-4.6.13.js", UI + "lib/modern-controls/JS/Chart.js-v4.5.1.js",
            .. ControlNames.Select(n => UI + "js/web-control/" + n + ".js"),
            UI + "js/web-control/toast.js", UI + "js/web-control/messagebox.js", UI + "js/vaikunth.js", UI + "js/workspace.js", UI + "js/ajax-panel.js"
        ],
        // each workspace page's own script (small, cached on its own so the big workspace.js stays shared)
        ["home.js"] = [UI + "js/home.js"],
        ["team.js"] = [UI + "js/team.js"],
        ["admin.js"] = [UI + "js/admin.js"],
        // each editor: the shared editor styles + the app's own, and the app's script
        ["pages.css"] = [UI + "css/editor.css", "_content/Vaikunth.AksharaPages/css/pages.css"],
        ["pages.js"] = ["_content/Vaikunth.AksharaPages/js/pages.js"],
        ["workbook.css"] = [UI + "css/editor.css", "_content/Vaikunth.HrishikeshaWorkbook/css/workbook.css"],
        ["workbook.js"] = ["_content/Vaikunth.HrishikeshaWorkbook/js/workbook.js"],
        ["slides.css"] = [UI + "css/editor.css", "_content/Vaikunth.VishwaroopaSlides/css/slides.css"],
        ["slides.js"] = ["_content/Vaikunth.VishwaroopaSlides/js/slides.js"],
        ["reader.css"] = [UI + "css/editor.css", "_content/Vaikunth.ShashvataReader/css/reader.css"],
        // pdf.js's worker (pdf.worker.min.js) is started by pdf.js itself, so it stays a file of its own
        ["reader.js"] = ["_content/Vaikunth.ShashvataReader/lib/pdfjs/pdf.min.js", "_content/Vaikunth.ShashvataReader/js/reader.js"],
        ["planner.css"] = [UI + "css/editor.css", "_content/Vaikunth.DhruvaPlanner/css/planner.css"],
        ["planner.js"] = ["_content/Vaikunth.DhruvaPlanner/js/planner.js"],
        // calendar and mail live in the workspace, so they only add their own file to workspace.css / workspace.js
        ["days.css"] = ["_content/Vaikunth.VaijayantiDays/css/days.css"],
        ["days.js"] = ["_content/Vaikunth.VaijayantiDays/js/days.js"],
        ["inbox.css"] = ["_content/Vaikunth.SudarshanaInbox/css/inbox.css"],
        ["inbox.js"] = ["_content/Vaikunth.SudarshanaInbox/js/inbox.js"],
    };
}

/// <summary>Builds the bundles from the files on disk and keeps them in memory until a file changes.</summary>
public sealed class BundleService
{
    private sealed record Built(string Signature, byte[] Body, string Hash);

    private readonly IWebHostEnvironment _env;
    private readonly ILogger<BundleService> _log;
    private readonly ConcurrentDictionary<string, Built> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly bool _minify;

    public BundleService(IWebHostEnvironment env, ILogger<BundleService> log)
    {
        _env = env;
        _log = log;
        _minify = !env.IsDevelopment();
    }

    /// <summary>The bundle's content and its hash, or null when there is no such bundle.</summary>
    public (byte[] Body, string Hash)? Get(string name)
    {
        if (!BundleConfig.All.TryGetValue(name, out var files)) return null;
        var provider = _env.WebRootFileProvider;
        var infos = files.Select(f => (Path: f, Info: provider.GetFileInfo(f))).ToList();
        var signature = string.Join("|", infos.Select(i => i.Info.Exists ? $"{i.Info.LastModified.Ticks}:{i.Info.Length}" : "missing"));
        if (_cache.TryGetValue(name, out var hit) && hit.Signature == signature) return (hit.Body, hit.Hash);

        var isCss = name.EndsWith(".css", StringComparison.OrdinalIgnoreCase);
        var sb = new StringBuilder();
        foreach (var (path, info) in infos)
        {
            if (!info.Exists) { _log.LogWarning("Bundle {Bundle}: {File} is missing.", name, path); continue; }
            string text;
            using (var stream = info.CreateReadStream())
            using (var reader = new StreamReader(stream)) text = reader.ReadToEnd();
            if (isCss) text = RebaseUrls(text, path);
            if (_minify && !path.Contains(".min.", StringComparison.OrdinalIgnoreCase)) text = Minify(text, isCss, path);
            sb.Append("/* ").Append(Path.GetFileName(path)).Append(" */\n").Append(text);
            sb.Append(isCss ? "\n" : "\n;\n");
        }
        var body = Encoding.UTF8.GetBytes(sb.ToString());
        var hash = Convert.ToHexString(SHA256.HashData(body))[..12].ToLowerInvariant();
        _cache[name] = new Built(signature, body, hash);
        return (body, hash);
    }

    public string? Hash(string name) => Get(name)?.Hash;

    private string Minify(string text, bool css, string path)
    {
        try
        {
            var result = css ? Uglify.Css(text) : Uglify.Js(text);
            if (!result.HasErrors && !string.IsNullOrEmpty(result.Code)) return result.Code;
            _log.LogWarning("Bundle file {File} could not be minified ({Error}); it is used as it is.", path, result.Errors.FirstOrDefault()?.ToString());
        }
        catch (Exception ex) { _log.LogWarning(ex, "Bundle file {File} could not be minified; it is used as it is.", path); }
        return text;
    }

    /// <summary>A stylesheet's url(fonts/x.woff2) is relative to its own folder; in a bundle served from /bundles/ it must start at the site root.</summary>
    private static string RebaseUrls(string css, string path)
    {
        var folder = "/" + path[..(path.LastIndexOf('/') + 1)];
        return Regex.Replace(css, @"url\(\s*(['""]?)(?!data:|https?:|//|/|#)([^'"")]+)\1\s*\)", m =>
        {
            var rel = m.Groups[2].Value.Trim();
            var parts = new List<string>(folder.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries));
            foreach (var seg in rel.Split('/'))
            {
                if (seg == "..") { if (parts.Count > 0) parts.RemoveAt(parts.Count - 1); }
                else if (seg != ".") parts.Add(seg);
            }
            return $"url('/{string.Join('/', parts)}')";
        });
    }
}

public static class BundleEndpoints
{
    public static IServiceCollection AddVaikunthBundles(this IServiceCollection services) => services.AddSingleton<BundleService>();

    /// <summary>GET /bundles/site.css?v=hash, /bundles/workspace.js?v=hash ...</summary>
    public static void MapVaikunthBundles(this IEndpointRouteBuilder app)
    {
        app.MapGet("/bundles/{name}", (string name, HttpContext ctx, BundleService bundles) =>
        {
            if (bundles.Get(name) is not var (body, hash)) return Results.NotFound();
            var etag = $"\"{hash}\"";
            ctx.Response.Headers.ETag = etag;
            ctx.Response.Headers.CacheControl = ctx.Request.Query["v"] == hash ? "public, max-age=31536000, immutable" : "no-cache";
            if (ctx.Request.Headers.IfNoneMatch == etag) return Results.StatusCode(StatusCodes.Status304NotModified);
            var type = name.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ? "text/css; charset=utf-8" : "text/javascript; charset=utf-8";
            return Results.Bytes(body, type);
        }).AllowAnonymous();
    }
}

/// <summary>&lt;vk-bundle name="site.css" /&gt; → one &lt;link&gt; or &lt;script&gt; with the bundle's current hash.</summary>
[HtmlTargetElement("vk-bundle", TagStructure = TagStructure.WithoutEndTag)]
public sealed class VkBundleTagHelper : TagHelper
{
    private readonly BundleService _bundles;
    public VkBundleTagHelper(BundleService bundles) => _bundles = bundles;

    public string Name { get; set; } = "";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var url = $"/bundles/{Name}?v={_bundles.Hash(Name) ?? "0"}";
        output.TagMode = TagMode.StartTagAndEndTag;
        if (Name.EndsWith(".css", StringComparison.OrdinalIgnoreCase))
        {
            output.TagName = "link";
            output.TagMode = TagMode.SelfClosing;
            output.Attributes.SetAttribute("rel", "stylesheet");
            output.Attributes.SetAttribute("href", url);
        }
        else
        {
            output.TagName = "script";
            output.Attributes.SetAttribute("src", url);
        }
    }
}
