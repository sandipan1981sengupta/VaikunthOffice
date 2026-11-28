using Microsoft.Data.SqlClient;
using Vaikunth.DtoLibrary;
using Vaikunth.ServiceLibrary.Data;

namespace Vaikunth.ServiceLibrary;

/// <summary>
/// Vaijayanti Days: a person's calendar. They see the events they made and the ones they were invited to,
/// always inside their own company. Only the person who made an event can change or delete it.
/// </summary>
public interface ICalendarService
{
    Task<List<CalendarEventDto>> ListAsync(Guid tenantId, Guid userId, DateTime fromUtc, DateTime toUtc);
    Task<CalendarEventDto?> GetAsync(Guid tenantId, Guid userId, Guid eventId);
    Task<OperationResult> SaveAsync(Guid tenantId, Guid userId, EventSave save);
    Task<bool> DeleteAsync(Guid tenantId, Guid userId, Guid eventId);
    Task<bool> RespondAsync(Guid tenantId, Guid userId, Guid eventId, EventResponse response);
}

/// <summary>Checks shared by the SQL and mock calendars.</summary>
public static class EventRules
{
    public static OperationResult? Check(EventSave s)
    {
        s.Title = (s.Title ?? "").Trim();
        if (s.Title.Length == 0) return OperationResult.Fail("Give the event a name.");
        if (s.Title.Length > 200) s.Title = s.Title[..200];
        s.Location = string.IsNullOrWhiteSpace(s.Location) ? null : s.Location.Trim()[..Math.Min(200, s.Location.Trim().Length)];
        s.Notes = string.IsNullOrWhiteSpace(s.Notes) ? null : s.Notes.Trim()[..Math.Min(4000, s.Notes.Trim().Length)];
        s.Color = EventColors.Clean(s.Color);
        s.StartUtc = DateTime.SpecifyKind(s.StartUtc, DateTimeKind.Utc);
        s.EndUtc = DateTime.SpecifyKind(s.EndUtc, DateTimeKind.Utc);
        if (s.AllDay)
        {
            s.StartUtc = s.StartUtc.Date;
            s.EndUtc = s.EndUtc.Date <= s.StartUtc ? s.StartUtc.AddDays(1) : s.EndUtc.Date;
        }
        if (s.EndUtc < s.StartUtc) return OperationResult.Fail("The event ends before it starts.");
        if ((s.EndUtc - s.StartUtc).TotalDays > 366) return OperationResult.Fail("An event can last a year at most.");
        s.People = s.People.Distinct().ToList();
        return null;
    }
}

public sealed class CalendarService(SqlDb db) : ICalendarService
{
    private static CalendarEventDto Map(SqlDataReader r) => new(
        r.G("EventId"), r.G("OwnerId"), r.S("OwnerName"), r.S("Title"), r.Utc("StartUtc"), r.Utc("EndUtc"), r.B("AllDay"),
        r.SN("Location"), r.SN("Notes"), r.S("Color"), new List<EventPersonDto>());

    private async Task<List<CalendarEventDto>> Load(Guid tenantId, Guid userId, DateTime? from, DateTime? to, Guid? eventId)
    {
        void P(SqlParameterCollection p) => p.With("@TenantId", tenantId).With("@UserId", userId)
            .AddDateTime2("@From", from).AddDateTime2("@To", to).With("@EventId", eventId);
        var events = await db.QueryAsync("dbo.Vaikunth_Event_List", P, Map);
        if (events.Count == 0) return events;
        var people = await db.QueryAsync("dbo.Vaikunth_Event_People", P,
            r => (EventId: r.G("EventId"), Person: new EventPersonDto(r.G("UserId"), r.S("Name"), r.S("Email"), (EventResponse)Convert.ToByte(r["Response"]))));
        var byId = events.ToDictionary(e => e.EventId);
        foreach (var p in people)
            if (byId.TryGetValue(p.EventId, out var e)) e.People.Add(p.Person);
        return events;
    }

    public Task<List<CalendarEventDto>> ListAsync(Guid tenantId, Guid userId, DateTime fromUtc, DateTime toUtc) =>
        Load(tenantId, userId, fromUtc, toUtc, null);

    public async Task<CalendarEventDto?> GetAsync(Guid tenantId, Guid userId, Guid eventId) =>
        (await Load(tenantId, userId, null, null, eventId)).FirstOrDefault();

    public async Task<OperationResult> SaveAsync(Guid tenantId, Guid userId, EventSave save)
    {
        if (EventRules.Check(save) is { } bad) return bad;
        var id = save.EventId ?? Guid.NewGuid();
        var rows = await db.QueryAsync("dbo.Vaikunth_Event_Save", p => p
            .With("@EventId", id).With("@TenantId", tenantId).With("@UserId", userId).AddText("@Title", save.Title, 200)
            .AddDateTime2("@StartUtc", save.StartUtc).AddDateTime2("@EndUtc", save.EndUtc).With("@AllDay", save.AllDay)
            .AddText("@Location", save.Location, 200).AddText("@Notes", save.Notes, 4000).With("@Color", save.Color)
            .AddText("@People", string.Join(",", save.People), -1), r => r.I("Result"));
        return rows.FirstOrDefault() == 1 ? OperationResult.Ok(id) : OperationResult.Fail("Only the person who made this event can change it.");
    }

    public async Task<bool> DeleteAsync(Guid tenantId, Guid userId, Guid eventId) =>
        (await db.QueryAsync("dbo.Vaikunth_Event_Delete", p => p.With("@TenantId", tenantId).With("@UserId", userId).With("@EventId", eventId),
            r => r.I("Result"))).FirstOrDefault() > 0;

    public async Task<bool> RespondAsync(Guid tenantId, Guid userId, Guid eventId, EventResponse response) =>
        (await db.QueryAsync("dbo.Vaikunth_Event_Respond", p => p.With("@TenantId", tenantId).With("@UserId", userId).With("@EventId", eventId)
            .With("@Response", (byte)response), r => r.I("Result"))).FirstOrDefault() > 0;
}
