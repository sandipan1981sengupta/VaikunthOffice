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
    public int EventsThisWeek { get; init; }
    /// <summary>"Next: Stand-up, Mon 10:00" (the person's next event in the coming week).</summary>
    public string? NextEvent { get; init; }
    public int UnreadMail { get; init; }
}

/// <summary>The Vaikunth Desk launcher: the three apps and recent files. Every app shows the same one at "/".</summary>
[Authorize]
public sealed class HomeController : Controller
{
    private readonly IDocumentService _documents;
    private readonly SuiteOptions _suite;
    private readonly CurrentApp _current;
    private readonly BillingDesk _billing;

    private readonly ICalendarService _calendar;
    private readonly IMailService _mail;

    public HomeController(IDocumentService documents, IOptions<SuiteOptions> suite, CurrentApp current, BillingDesk billing,
        ICalendarService calendar, IMailService mail)
    {
        _calendar = calendar;
        _mail = mail;
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
        var recent = await _documents.RecentAsync(User.TenantId(), User.UserId(), app, 60);
        ClientBillDto? bill = null;
        if (User.IsInRole(AppRoles.TenantAdmin) && await _billing.ForClientAsync(User.TenantId()) is { } b
            && (b.State is BillState.Overdue or BillState.OnHold || (b.State == BillState.Due && b.DaysLate(BillingCalculator.Today) == 0
                && b.DueOn is DateOnly d && d.DayNumber - BillingCalculator.Today.DayNumber <= 7)))
            bill = b;
        ViewData["Partial"] = Request.Headers["X-Partial"] == "1";
        // the calendar and mail figures; a database that hasn't had the update script yet just shows 0
        int events = 0, unread = 0; string? next = null;
        var apps = User.Apps();
        try
        {
            if (apps.Has(SuiteApp.Days))
            {
                var now = DateTime.UtcNow;
                var week = await _calendar.ListAsync(User.TenantId(), User.UserId(), now, now.AddDays(7));
                events = week.Count;
                if (week.FirstOrDefault(e => e.StartUtc >= now) is { } n)
                    next = $"Next: {n.Title}, {(n.AllDay ? n.StartUtc.ToString("ddd d MMM") : TimeZoneInfo.ConvertTimeFromUtc(n.StartUtc, India).ToString("ddd HH:mm"))}";
            }
            if (apps.Has(SuiteApp.Inbox)) unread = (await _mail.CountsAsync(User.TenantId(), User.UserId())).UnreadInbox;
        }
        catch (Microsoft.Data.SqlClient.SqlException) { }
        return View(new LauncherViewModel { Suite = _suite, Current = _current, Recent = recent, Filter = filter ?? "all", Bill = bill,
            EventsThisWeek = events, NextEvent = next, UnreadMail = unread });
    }

    private static readonly TimeZoneInfo India = FindIndia();
    private static TimeZoneInfo FindIndia()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"); }
        catch { try { return TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"); } catch { return TimeZoneInfo.Local; } }
    }

    /// <summary>After a file action: the page's script gets {ok, message, url} and reloads only the content.</summary>
    private IActionResult Back(string? ok, string? error)
    {
        if (Request.Headers.XRequestedWith != "XMLHttpRequest")
        {
            if (error is not null) TempData["Error"] = error; else if (ok is not null) TempData["Ok"] = ok;
            return Redirect("/");
        }
        return Json(new { ok = error is null, message = error ?? ok, url = "/" });
    }

    /// <summary>Rename a file from Recent files (the content stays as it is).</summary>
    [HttpPost("files/rename"), ValidateAntiForgeryToken]
    public async Task<IActionResult> RenameFile(Guid documentId, string? title)
    {
        var name = (title ?? "").Trim();
        if (name.Length == 0) return Back(null, "Type a name for the file.");
        if (name.Length > 200) return Back(null, "Keep the name under 200 characters.");
        var doc = await _documents.GetAsync(User.TenantId(), User.UserId(), documentId);
        if (doc is null) return Back(null, "This file isn't there any more.");
        var result = await _documents.SaveAsync(User.TenantId(), User.UserId(), new DocumentSave { DocumentId = doc.DocumentId, App = doc.App, Title = name, Content = doc.Content });
        return result.Succeeded ? Back($"Renamed to {name}.", null) : Back(null, result.Error);
    }

    /// <summary>Delete a file from Recent files. It is kept in the database (IsDeleted), so an admin can bring it back.</summary>
    [HttpPost("files/delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteFile(Guid documentId, string? title)
    {
        var done = await _documents.DeleteAsync(User.TenantId(), User.UserId(), documentId);
        return done ? Back($"{title ?? "The file"} is deleted.", null) : Back(null, "Only the person who made this file, or an admin, can delete it.");
    }

    [HttpPost("files/star"), ValidateAntiForgeryToken]
    public async Task<IActionResult> StarFile(Guid documentId, bool starred, string? title)
    {
        await _documents.SetStarredAsync(User.UserId(), documentId, starred);
        return Back(starred ? $"{title} is starred." : $"{title} is no longer starred.", null);
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

    /// <summary>Download a saved file from the home page's Recent files: the server makes the Office file (or the PDF);
    /// for Slides, which builds its file in the browser, the editor opens and downloads it at once.</summary>
    [HttpGet]
    public virtual async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var doc = await Documents.GetAsync(User.TenantId(), User.UserId(), id);
        if (doc is null) return NotFound("This file isn't there any more.");
        var name = SafeName(doc.Title);
        try
        {
            var file = string.IsNullOrEmpty(doc.Content) ? null : await ExportAsync(doc.Content, name, ct);
            if (file is null && Current.Info.App == SuiteApp.Reader) return NotFound("This file has no PDF in it yet.");
            if (file is null) return Redirect(CurrentApp.EditorUrl(Current.Info.App, id) + "&download=1");
            return File(file.Bytes, file.ContentType, name + file.Extension);
        }
        catch (System.Text.Json.JsonException)
        {
            return BadRequest("The file's content couldn't be read.");
        }
    }

    private static string SafeName(string? title)
    {
        var name = string.Concat((string.IsNullOrWhiteSpace(title) ? "Untitled" : title.Trim())
            .Select(c => Path.GetInvalidFileNameChars().Contains(c) || c is '"' or '<' or '>' or '|' or ':' or '*' or '?' or '\\' or '/' ? ' ' : c)).Trim();
        if (name.Length > 120) name = name[..120];
        return name.Length == 0 ? "Untitled" : name;
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
