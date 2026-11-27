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
    /// <summary>For a client admin: this client's bill, when a payment is due soon or late (the banner on the launcher).</summary>
    public ClientBillDto? Bill { get; init; }
}

/// <summary>The Vaikunth Office launcher: the three apps and recent files. Every app shows the same one at "/".</summary>
[Authorize]
public sealed class HomeController : Controller
{
    private readonly IDocumentService _documents;
    private readonly SuiteOptions _suite;
    private readonly CurrentApp _current;
    private readonly BillingDesk _billing;

    public HomeController(IDocumentService documents, IOptions<SuiteOptions> suite, CurrentApp current, BillingDesk billing)
    {
        _billing = billing;
        _documents = documents;
        _suite = suite.Value;
        _current = current;
    }

    public async Task<IActionResult> Index(string? filter = null, string? blocked = null)
    {
        if (User.IsSuperAdmin()) return Redirect("/admin");
        if (blocked is not null) TempData["Blocked"] = SuiteApps.For(blocked).Name;
        SuiteApp? app = SuiteApps.All.FirstOrDefault(a => a.Key == filter)?.App;
        var recent = await _documents.RecentAsync(User.TenantId(), User.UserId(), app, 12);
        ClientBillDto? bill = null;
        if (User.IsInRole(AppRoles.TenantAdmin) && await _billing.ForClientAsync(User.TenantId()) is { } b
            && (b.State is BillState.Overdue or BillState.OnHold || (b.State == BillState.Due && b.DaysLate(BillingCalculator.Today) == 0
                && b.DueOn is DateOnly d && d.DayNumber - BillingCalculator.Today.DayNumber <= 7)))
            bill = b;
        return View(new LauncherViewModel { Suite = _suite, Current = _current, Recent = recent, Filter = filter ?? "all", Bill = bill });
    }

    [AllowAnonymous]
    public IActionResult Error() => View();
}

/// <summary>What the editor sends to make an Office file: the file name and the current content (saved or not).</summary>
public sealed class ExportRequest
{
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
}

/// <summary>An Office file ready to download.</summary>
public sealed record ExportedFile(byte[] Bytes, string ContentType, string Extension);

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

    /// <summary>Turns the editor's content into a Microsoft Office file (.docx / .xlsx), or null when the browser builds it (Slides).</summary>
    protected virtual Task<ExportedFile?> ExportAsync(string content, string title, CancellationToken ct) => Task.FromResult<ExportedFile?>(null);

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
        return Redirect(CurrentApp.EditorUrl(Current.Info.App, result.Id));
    }

    /// <summary>Download a copy for Microsoft Office. Nothing is saved on the server.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public virtual async Task<IActionResult> Export([FromBody] ExportRequest request, CancellationToken ct)
    {
        if (request is null || string.IsNullOrEmpty(request.Content)) return BadRequest("Nothing to download.");
        var name = string.Concat((string.IsNullOrWhiteSpace(request.Title) ? "Untitled" : request.Title.Trim())
            .Select(c => Path.GetInvalidFileNameChars().Contains(c) || c is '"' or '<' or '>' or '|' or ':' or '*' or '?' or '\\' or '/' ? ' ' : c)).Trim();
        if (name.Length > 120) name = name[..120];
        try
        {
            var file = await ExportAsync(request.Content, name, ct);
            if (file is null) return NotFound("This app makes its file in the browser.");
            return File(file.Bytes, file.ContentType, (name.Length == 0 ? "Untitled" : name) + file.Extension);
        }
        catch (System.Text.Json.JsonException)
        {
            return BadRequest("The file's content couldn't be read.");
        }
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
