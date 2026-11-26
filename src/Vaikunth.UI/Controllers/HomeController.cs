using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Vaikunth.DtoLibrary;
using Vaikunth.ServiceLibrary;
using Vaikunth.UI.Infrastructure;

namespace Vaikunth.UI.Controllers;

public sealed class LauncherViewModel
{
    public required SuiteOptions Suite { get; init; }
    public required CurrentApp Current { get; init; }
    public List<DocumentSummaryDto> Recent { get; init; } = new();
    public string Filter { get; init; } = "all";
}

/// <summary>The Vaikunth Office launcher: the three apps and recent files. Every app shows the same one at "/".</summary>
[Authorize]
public sealed class HomeController : Controller
{
    private readonly IDocumentService _documents;
    private readonly SuiteOptions _suite;
    private readonly CurrentApp _current;

    public HomeController(IDocumentService documents, IOptions<SuiteOptions> suite, CurrentApp current)
    {
        _documents = documents;
        _suite = suite.Value;
        _current = current;
    }

    public async Task<IActionResult> Index(string? filter = null, string? blocked = null)
    {
        if (User.IsSuperAdmin()) return Redirect("/admin");
        if (blocked is not null) TempData["Blocked"] = SuiteApps.For(blocked).Name;
        SuiteApp? app = filter switch { "pages" => SuiteApp.Pages, "workbook" => SuiteApp.Workbook, "slides" => SuiteApp.Slides, _ => null };
        var recent = await _documents.RecentAsync(User.TenantId(), User.UserId(), app, 12);
        return View(new LauncherViewModel { Suite = _suite, Current = _current, Recent = recent, Filter = filter ?? "all" });
    }

    [AllowAnonymous]
    public IActionResult Error() => View();
}

/// <summary>
/// Base for each app's editor: open, create and save files of that app.
/// Each web project adds a small EditorController : EditorControllerBase.
/// </summary>
[Authorize]
public abstract class EditorControllerBase : Controller
{
    protected readonly IDocumentService Documents;
    protected readonly CurrentApp Current;

    protected EditorControllerBase(IDocumentService documents, CurrentApp current)
    {
        Documents = documents;
        Current = current;
    }

    /// <summary>The body of a brand-new file of this app.</summary>
    protected abstract string BlankContent();

    /// <summary>Super admins have no files, and a client only gets the apps on its plan.</summary>
    public override void OnActionExecuting(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context)
    {
        if (User.IsSuperAdmin()) context.Result = Redirect("/admin");
        else if (!User.Apps().Has(Current.Info.App)) context.Result = Redirect($"/?blocked={Current.Info.Key}");
        base.OnActionExecuting(context);
    }

    /// <summary>Opens a file, or the newest one of this app when no id is given.</summary>
    public virtual async Task<IActionResult> Index(Guid? id)
    {
        DocumentDto? doc = null;
        var tenant = User.TenantId();
        if (id is Guid docId) doc = await Documents.GetAsync(tenant, User.UserId(), docId);
        else if ((await Documents.RecentAsync(tenant, User.UserId(), Current.Info.App, 1)).FirstOrDefault() is { } latest)
            doc = await Documents.GetAsync(tenant, User.UserId(), latest.DocumentId);
        if (doc is null) return RedirectToAction(nameof(New));
        return View("Index", doc);
    }

    public virtual async Task<IActionResult> New()
    {
        var result = await Documents.SaveAsync(User.TenantId(), User.UserId(), new DocumentSave { App = Current.Info.App, Title = "Untitled", Content = BlankContent() });
        return RedirectToAction(nameof(Index), new { id = result.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public virtual async Task<IActionResult> Save([FromBody] DocumentSave save)
    {
        save.App = Current.Info.App;
        var result = await Documents.SaveAsync(User.TenantId(), User.UserId(), save);
        return result.Succeeded
            ? Json(new { ok = true, id = result.Id, savedAt = DateTime.Now.ToString("HH:mm") })
            : Json(new { ok = false, error = result.Error });
    }
}
