using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Vaikunth.DtoLibrary;
using Vaikunth.ServiceLibrary;
using Vaikunth.UI.Controllers;
using Vaikunth.UI.Infrastructure;

namespace Vaikunth.SudarshanaInbox.Controllers;

public sealed class InboxViewModel
{
    public List<TeamMemberDto> People { get; init; } = new();
    public MailCountsDto Counts { get; init; } = new(0, 0, 0, 0);
    public bool NeedsUpdate { get; init; }
    public bool OpenNew { get; init; }
    public string? NewTo { get; init; }
    public bool EmailConfigured { get; init; }
}

/// <summary>What the compose window sends: a message to send, or a draft to keep.</summary>
public sealed class ComposeRequest
{
    public Guid? DraftId { get; set; }
    public string To { get; set; } = "";
    public string Cc { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public bool IsDraft { get; set; }
}

/// <summary>Sudarshana Inbox: /inbox shows the mail; folders, messages and sending are JSON calls from inbox.js.
/// Addresses of the company's own people are delivered to their Inbox here; any other address gets a real email.</summary>
[Area("inbox")]
public sealed class EditorController(CurrentApp current, IMailService mail, IUserService users, IAppEmailSender email) : SuiteAppControllerBase(current)
{
    public async Task<IActionResult> Index(bool @new = false, string? to = null)
    {
        var people = (await users.ListForTenantAsync(User.TenantId())).Where(p => p.IsActive).OrderBy(p => p.DisplayName).ToList();
        MailCountsDto counts = new(0, 0, 0, 0);
        var needsUpdate = false;
        try { counts = await mail.CountsAsync(User.TenantId(), User.UserId()); }
        catch (SqlException ex) when (NeedsUpdate(ex)) { needsUpdate = true; }
        ViewData["Partial"] = Request.Headers["X-Partial"] == "1";
        return View(new InboxViewModel { People = people, Counts = counts, NeedsUpdate = needsUpdate, OpenNew = @new, NewTo = to, EmailConfigured = email.IsConfigured });
    }

    /// <summary>The home page's "New email" opens the Inbox with a new message open.</summary>
    public IActionResult New() => Redirect("/inbox?new=1");

    private static MailFolder? FolderOf(string? name) => (name ?? "").ToLowerInvariant() switch
    {
        "inbox" => MailFolder.Inbox, "sent" => MailFolder.Sent, "drafts" => MailFolder.Drafts, "trash" => MailFolder.Trash, _ => null
    };

    private static object Shape(MailSummaryDto m) => new
    {
        id = m.MessageId, folder = m.Folder.ToString().ToLowerInvariant(), fromId = m.FromUserId, from = m.FromName, fromEmail = m.FromEmail, to = m.ToText,
        subject = m.Subject, preview = m.Preview, when = m.WhenUtc, read = m.IsRead, starred = m.IsStarred, draft = m.IsDraft
    };

    private static object Shape(MailCountsDto c) => new { inbox = c.UnreadInbox, drafts = c.Drafts, starred = c.Starred, trash = c.Trash };

    [HttpGet]
    public async Task<IActionResult> List(string folder = "inbox")
    {
        try
        {
            var starred = string.Equals(folder, "starred", StringComparison.OrdinalIgnoreCase);
            var f = FolderOf(folder) ?? MailFolder.Inbox;
            var list = await mail.ListAsync(User.TenantId(), User.UserId(), f, starred);
            var counts = await mail.CountsAsync(User.TenantId(), User.UserId());
            return Json(new { ok = true, messages = list.Select(Shape), counts = Shape(counts) });
        }
        catch (SqlException ex) when (NeedsUpdate(ex)) { return Fail(UpdateMessage); }
    }

    [HttpGet]
    public async Task<IActionResult> Counts()
    {
        try { return Json(new { ok = true, counts = Shape(await mail.CountsAsync(User.TenantId(), User.UserId())) }); }
        catch (SqlException ex) when (NeedsUpdate(ex)) { return Fail(UpdateMessage); }
    }

    [HttpGet]
    public async Task<IActionResult> Open(Guid id)
    {
        try
        {
            var m = await mail.OpenAsync(User.TenantId(), User.UserId(), id);
            if (m is null) return Fail("This message isn't there any more.");
            return Json(new
            {
                ok = true,
                message = new
                {
                    id = m.MessageId, folder = m.Folder.ToString().ToLowerInvariant(), fromId = m.FromUserId, from = m.FromName, fromEmail = m.FromEmail,
                    to = m.ToText, cc = m.CcText, subject = m.Subject, body = m.BodyHtml, when = m.WhenUtc, read = m.IsRead, starred = m.IsStarred, draft = m.IsDraft,
                    mine = m.FromUserId == User.UserId()
                }
            });
        }
        catch (SqlException ex) when (NeedsUpdate(ex)) { return Fail(UpdateMessage); }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Send([FromBody] ComposeRequest request)
    {
        if (request is null) return Fail("Nothing to send.");
        var to = MailText.Addresses(request.To);
        var cc = MailText.Addresses(request.Cc).Where(a => !to.Contains(a)).ToList();
        var all = to.Concat(cc).ToList();
        if (!request.IsDraft)
        {
            if (to.Count == 0) return Fail("Add at least one person in To (a name from your company, or an email address).");
            if (string.IsNullOrWhiteSpace(request.Subject) && string.IsNullOrWhiteSpace(MailText.Preview(request.Body)))
                return Fail("The message is empty. Write a subject or a few words.");
            if (all.Count > 100) return Fail("A message can go to 100 addresses at most.");
        }
        try
        {
            var people = await users.ListForTenantAsync(User.TenantId());
            var byEmail = people.Where(p => p.IsActive).GroupBy(p => p.Email.ToLowerInvariant()).ToDictionary(g => g.Key, g => g.First());
            var inside = all.Where(byEmail.ContainsKey).Select(a => byEmail[a]).Where(p => p.UserId != User.UserId()).ToList();
            var outside = all.Where(a => !byEmail.ContainsKey(a)).ToList();
            var result = await mail.SaveAsync(User.TenantId(), User.UserId(), new MailSend
            {
                DraftId = request.DraftId, ToText = request.To, CcText = request.Cc, Subject = request.Subject, BodyHtml = request.Body,
                IsDraft = request.IsDraft, Recipients = inside.Select(p => p.UserId).ToList()
            });
            if (!result.Succeeded) return Fail(result.Error!);
            if (request.IsDraft) return Json(new { ok = true, id = result.Id, message = "Draft saved." });

            var failed = new List<string>();
            foreach (var address in outside)
                if (!await email.SendAsync(address, string.IsNullOrWhiteSpace(request.Subject) ? "(no subject)" : request.Subject.Trim(), OutsideHtml(request.Body)))
                    failed.Add(address);
            var parts = new List<string>();
            if (inside.Count > 0) parts.Add($"delivered to {inside.Count} {(inside.Count == 1 ? "person" : "people")} here");
            if (outside.Count - failed.Count > 0) parts.Add($"emailed to {outside.Count - failed.Count} outside address{(outside.Count - failed.Count == 1 ? "" : "es")}");
            var msg = "Sent" + (parts.Count > 0 ? ": " + string.Join(" and ", parts) : "") + ".";
            if (failed.Count > 0) msg += $" Couldn't email {string.Join(", ", failed)}.";
            return Json(new { ok = true, id = result.Id, message = msg, warn = failed.Count > 0 });
        }
        catch (SqlException ex) when (NeedsUpdate(ex)) { return Fail(UpdateMessage); }
    }

    private string OutsideHtml(string body)
    {
        var who = WebUtility.HtmlEncode($"{User.DisplayName()} <{User.Identity?.Name}>");
        return $"""
            <div style="font-family:Segoe UI,Arial,sans-serif;font-size:14px;line-height:1.55;color:#1d1f3a">{MailText.Clean(body)}</div>
            <p style="margin-top:24px;padding-top:10px;border-top:1px solid #e3e5f2;font-family:Segoe UI,Arial,sans-serif;font-size:12px;color:#6b6f8f">
            Sent by {who} from Sudarshana Inbox · Vaikunth Desk. Reply to {WebUtility.HtmlEncode(User.Identity?.Name ?? "")} to answer.</p>
            """;
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Star(Guid id, bool starred) => await Do(() => mail.UpdateAsync(User.TenantId(), User.UserId(), id, isStarred: starred),
        starred ? "Starred." : "Star taken off.");

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Read(Guid id, bool read) => await Do(() => mail.UpdateAsync(User.TenantId(), User.UserId(), id, isRead: read),
        read ? "Marked as read." : "Marked as unread.");

    /// <summary>Back out of Trash: to Sent when the person wrote it, otherwise to the Inbox.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(Guid id)
    {
        try
        {
            var m = await mail.OpenAsync(User.TenantId(), User.UserId(), id);
            if (m is null) return Fail("This message isn't there any more.");
            var folder = m.IsDraft ? MailFolder.Drafts : m.FromUserId == User.UserId() ? MailFolder.Sent : MailFolder.Inbox;
            await mail.UpdateAsync(User.TenantId(), User.UserId(), id, folder: folder);
            return Json(new { ok = true, message = $"Moved back to {folder}." });
        }
        catch (SqlException ex) when (NeedsUpdate(ex)) { return Fail(UpdateMessage); }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            var r = await mail.DeleteAsync(User.TenantId(), User.UserId(), id);
            return r switch
            {
                1 => Json(new { ok = true, message = "Moved to Trash.", gone = false }),
                2 => Json(new { ok = true, message = "Deleted for good.", gone = true }),
                _ => Fail("This message isn't there any more.")
            };
        }
        catch (SqlException ex) when (NeedsUpdate(ex)) { return Fail(UpdateMessage); }
    }

    /// <summary>Empty the Trash.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EmptyTrash()
    {
        try
        {
            var list = await mail.ListAsync(User.TenantId(), User.UserId(), MailFolder.Trash);
            foreach (var m in list) await mail.DeleteAsync(User.TenantId(), User.UserId(), m.MessageId);
            return Json(new { ok = true, message = list.Count == 0 ? "Trash is already empty." : $"{list.Count} {(list.Count == 1 ? "message" : "messages")} deleted for good." });
        }
        catch (SqlException ex) when (NeedsUpdate(ex)) { return Fail(UpdateMessage); }
    }

    private async Task<IActionResult> Do(Func<Task<bool>> action, string ok)
    {
        try { return await action() ? Json(new { ok = true, message = ok }) : Fail("This message isn't there any more."); }
        catch (SqlException ex) when (NeedsUpdate(ex)) { return Fail(UpdateMessage); }
    }
}
