using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Vaikunth.DtoLibrary;
using Vaikunth.ServiceLibrary;
using Vaikunth.UI.Controllers;
using Vaikunth.UI.Infrastructure;

namespace Vaikunth.VaijayantiDays.Controllers;

/// <summary>The calendar page and the small JSON calls its script makes.</summary>
public sealed class DaysViewModel
{
    public List<TeamMemberDto> People { get; init; } = new();
    public Guid Me { get; init; }
    public bool NeedsUpdate { get; init; }
    public bool OpenNew { get; init; }
}

/// <summary>Body of /days/editor/save: the event, and its time as the person's browser writes it (for the invite email).</summary>
public sealed class DaysSaveRequest
{
    public EventSave Event { get; set; } = new();
    public string? WhenText { get; set; }
}

/// <summary>Vaijayanti Days: /days shows the calendar; events load and save as JSON. Invited people get a message in Sudarshana Inbox.</summary>
[Area("days")]
public sealed class EditorController(CurrentApp current, ICalendarService calendar, IUserService users, IMailService mail) : SuiteAppControllerBase(current)
{
    public async Task<IActionResult> Index(bool @new = false)
    {
        var people = (await users.ListForTenantAsync(User.TenantId())).Where(p => p.IsActive && p.UserId != User.UserId()).OrderBy(p => p.DisplayName).ToList();
        var needsUpdate = false;
        try { await calendar.ListAsync(User.TenantId(), User.UserId(), DateTime.UtcNow, DateTime.UtcNow.AddMinutes(1)); }
        catch (SqlException ex) when (NeedsUpdate(ex)) { needsUpdate = true; }
        ViewData["Partial"] = Request.Headers["X-Partial"] == "1";
        return View(new DaysViewModel { People = people, Me = User.UserId(), NeedsUpdate = needsUpdate, OpenNew = @new });
    }

    /// <summary>The home page's "New event" opens the calendar with the new event window open.</summary>
    public IActionResult New() => Redirect("/days?new=1");

    private object Shape(CalendarEventDto e)
    {
        var me = User.UserId();
        return new
        {
            id = e.EventId, title = e.Title, start = e.StartUtc, end = e.EndUtc, allDay = e.AllDay, location = e.Location, notes = e.Notes,
            color = e.Color, owner = e.OwnerName, ownerId = e.OwnerId, mine = e.IsMine(me),
            my = e.People.FirstOrDefault(p => p.UserId == me)?.Response.ToString().ToLowerInvariant(),
            people = e.People.Select(p => new { id = p.UserId, name = p.Name, email = p.Email, response = p.Response.ToString().ToLowerInvariant() })
        };
    }

    [HttpGet]
    public async Task<IActionResult> Events(DateTime from, DateTime to)
    {
        from = DateTime.SpecifyKind(from.ToUniversalTime(), DateTimeKind.Utc);
        to = DateTime.SpecifyKind(to.ToUniversalTime(), DateTimeKind.Utc);
        if (to <= from || (to - from).TotalDays > 400) return Fail("Pick a shorter range.");
        try
        {
            var list = await calendar.ListAsync(User.TenantId(), User.UserId(), from, to);
            return Json(new { ok = true, events = list.Select(Shape) });
        }
        catch (SqlException ex) when (NeedsUpdate(ex)) { return Fail(UpdateMessage); }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([FromBody] DaysSaveRequest request)
    {
        if (request?.Event is not { } save) return Fail("Nothing to save.");
        try
        {
            var before = save.EventId is Guid id ? await calendar.GetAsync(User.TenantId(), User.UserId(), id) : null;
            var result = await calendar.SaveAsync(User.TenantId(), User.UserId(), save);
            if (!result.Succeeded) return Fail(result.Error!);
            var saved = await calendar.GetAsync(User.TenantId(), User.UserId(), result.Id!.Value);
            var invited = 0;
            if (saved is not null && User.Apps().Has(SuiteApp.Inbox))
            {
                var had = before?.People.Select(p => p.UserId).ToHashSet() ?? new HashSet<Guid>();
                var fresh = saved.People.Where(p => !had.Contains(p.UserId)).ToList();
                if (fresh.Count > 0)
                {
                    invited = fresh.Count;
                    await mail.SaveAsync(User.TenantId(), User.UserId(), new MailSend
                    {
                        ToText = string.Join("; ", fresh.Select(p => $"{p.Name} <{p.Email}>")),
                        Subject = $"Invitation: {saved.Title}",
                        BodyHtml = InviteHtml(saved, request.WhenText),
                        Recipients = fresh.Select(p => p.UserId).ToList()
                    });
                }
            }
            var msg = before is null ? "Event added." : "Event saved.";
            if (invited > 0) msg += $" {invited} {(invited == 1 ? "person was" : "people were")} invited in Sudarshana Inbox.";
            return Json(new { ok = true, message = msg, @event = saved is null ? null : Shape(saved) });
        }
        catch (SqlException ex) when (NeedsUpdate(ex)) { return Fail(UpdateMessage); }
    }

    private string InviteHtml(CalendarEventDto e, string? when)
    {
        static string H(string? s) => WebUtility.HtmlEncode(s ?? "");
        return $"""
            <div style="border-left:4px solid #0F7C74;padding:4px 0 4px 14px">
            <p style="margin:0 0 6px;font-size:12px;letter-spacing:.06em;text-transform:uppercase;color:#0F7C74"><b>Vaijayanti Days · Invitation</b></p>
            <h2 style="margin:0 0 6px">{H(e.Title)}</h2>
            <p style="margin:0"><b>When:</b> {H(string.IsNullOrWhiteSpace(when) ? e.StartUtc.ToString("ddd d MMM yyyy HH:mm") + " UTC" : when)}</p>
            {(string.IsNullOrEmpty(e.Location) ? "" : $"<p style=\"margin:0\"><b>Where:</b> {H(e.Location)}</p>")}
            <p style="margin:0"><b>Organiser:</b> {H(User.DisplayName())}</p>
            {(string.IsNullOrEmpty(e.Notes) ? "" : $"<p style=\"margin:10px 0 0;white-space:pre-wrap\">{H(e.Notes)}</p>")}
            </div>
            <p>Open <a href="/days">Vaijayanti Days</a> to answer Yes, Maybe or No.</p>
            """;
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            return await calendar.DeleteAsync(User.TenantId(), User.UserId(), id)
                ? Json(new { ok = true, message = "Event deleted." })
                : Fail("Only the person who made this event can delete it.");
        }
        catch (SqlException ex) when (NeedsUpdate(ex)) { return Fail(UpdateMessage); }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Respond(Guid id, EventResponse response)
    {
        try
        {
            var ok = await calendar.RespondAsync(User.TenantId(), User.UserId(), id, response);
            var text = response switch { EventResponse.Yes => "You're going.", EventResponse.No => "You said no.", EventResponse.Maybe => "You said maybe.", _ => "Saved." };
            return ok ? Json(new { ok = true, message = text }) : Fail("You aren't invited to this event.");
        }
        catch (SqlException ex) when (NeedsUpdate(ex)) { return Fail(UpdateMessage); }
    }
}
