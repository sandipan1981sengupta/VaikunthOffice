using Vaikunth.DtoLibrary;

namespace Vaikunth.ServiceLibrary.Mock;

/// <summary>In-memory calendar with a few sample events for Acme. Lost on restart.</summary>
public sealed class MockCalendarService : ICalendarService
{
    private sealed class Ev
    {
        public Guid EventId; public Guid TenantId; public Guid OwnerId; public string Title = ""; public DateTime StartUtc; public DateTime EndUtc;
        public bool AllDay; public string? Location; public string? Notes; public string Color = "indigo";
        public Dictionary<Guid, EventResponse> People = new();
    }

    private readonly List<Ev> _events = new();
    private readonly object _lock = new();

    public MockCalendarService()
    {
        MockData.Person P(string email) => MockData.People[email];
        var priya = P("priya.sharma@acme.com"); var arjun = P("arjun.nair@acme.com"); var fatima = P("fatima.khan@acme.com");
        var rohan = P("rohan.das@acme.com"); var meera = P("meera.iyer@acme.com");
        // times are written in India time (UTC+5:30) so the sample reads naturally there
        var monday = DateTime.Today.AddDays(-(((int)DateTime.Today.DayOfWeek + 6) % 7));
        DateTime Ist(DateTime day, int h, int m = 0) => DateTime.SpecifyKind(day.Date.AddHours(h).AddMinutes(m).AddMinutes(-330), DateTimeKind.Utc);
        void E(MockData.Person owner, string title, DateTime start, DateTime end, string color, string? where, string? notes, bool allDay = false,
               params (MockData.Person P, EventResponse R)[] people)
        {
            var e = new Ev { EventId = Guid.NewGuid(), TenantId = MockData.AcmeId, OwnerId = owner.UserId, Title = title, StartUtc = start, EndUtc = end,
                Color = color, Location = where, Notes = notes, AllDay = allDay };
            foreach (var (p, r) in people) e.People[p.UserId] = r;
            _events.Add(e);
        }
        for (var w = -2; w <= 3; w++)
        {
            var mon = monday.AddDays(7 * w);
            E(priya, "Monday stand-up", Ist(mon, 10), Ist(mon, 10, 30), "indigo", "Board room", "Ten minutes each: what's done, what's next, what's stuck.", false,
              (arjun, EventResponse.Yes), (fatima, EventResponse.Yes), (rohan, EventResponse.Maybe));
        }
        E(priya, "Quarterly review with Finance", Ist(monday.AddDays(1), 15), Ist(monday.AddDays(1), 16, 30), "saffron", "Conference room 2",
          "Bring the Q3 numbers and the travel budget.", false, (fatima, EventResponse.Yes), (arjun, EventResponse.Pending));
        E(arjun, "Client visit: Shree Textiles", Ist(monday.AddDays(2), 11), Ist(monday.AddDays(2), 13), "teal", "Surat office", "Demo of the new order sheet.", false,
          (priya, EventResponse.Pending));
        E(priya, "Lunch with Meera (welcome)", Ist(monday.AddDays(3), 13), Ist(monday.AddDays(3), 14), "green", "Cafe Lotus", null, false, (meera, EventResponse.Yes));
        E(rohan, "Release planning", Ist(monday.AddDays(4), 16), Ist(monday.AddDays(4), 17), "violet", "Online", "Agree what goes into the December release.", false,
          (priya, EventResponse.Yes), (arjun, EventResponse.No));
        E(priya, "Diwali office celebration", DateTime.SpecifyKind(monday.AddDays(11), DateTimeKind.Utc), DateTime.SpecifyKind(monday.AddDays(12), DateTimeKind.Utc),
          "rose", "Office terrace", "Sweets, rangoli and the team photo.", true, (arjun, EventResponse.Yes), (fatima, EventResponse.Yes), (rohan, EventResponse.Yes), (meera, EventResponse.Pending));
        E(priya, "Payroll closes", Ist(monday.AddDays(9), 9), Ist(monday.AddDays(9), 9, 30), "slate", null, null, false, (fatima, EventResponse.Yes));
        E(priya, "Team offsite", DateTime.SpecifyKind(monday.AddDays(17), DateTimeKind.Utc), DateTime.SpecifyKind(monday.AddDays(19), DateTimeKind.Utc),
          "teal", "Lonavala", "Two days: planning for next year and some fun.", true, (arjun, EventResponse.Maybe), (fatima, EventResponse.Yes), (rohan, EventResponse.Yes));
    }

    private static string NameOf(Guid id) => MockData.People.Values.FirstOrDefault(p => p.UserId == id)?.DisplayName ?? "";

    private static CalendarEventDto Dto(Ev e) => new(e.EventId, e.OwnerId, NameOf(e.OwnerId), e.Title, e.StartUtc, e.EndUtc, e.AllDay, e.Location, e.Notes, e.Color,
        e.People.Select(kv => MockData.People.Values.FirstOrDefault(p => p.UserId == kv.Key) is { } p
            ? new EventPersonDto(p.UserId, p.DisplayName, p.Email, kv.Value) : null).OfType<EventPersonDto>().OrderBy(p => p.Name).ToList());

    private static bool Sees(Ev e, Guid tenantId, Guid userId) => e.TenantId == tenantId && (e.OwnerId == userId || e.People.ContainsKey(userId));

    public Task<List<CalendarEventDto>> ListAsync(Guid tenantId, Guid userId, DateTime fromUtc, DateTime toUtc)
    {
        lock (_lock)
            return Task.FromResult(_events.Where(e => Sees(e, tenantId, userId) && e.EndUtc > fromUtc && e.StartUtc < toUtc)
                .OrderBy(e => e.StartUtc).Select(Dto).ToList());
    }

    public Task<CalendarEventDto?> GetAsync(Guid tenantId, Guid userId, Guid eventId)
    {
        lock (_lock) return Task.FromResult(_events.Where(e => e.EventId == eventId && Sees(e, tenantId, userId)).Select(Dto).FirstOrDefault());
    }

    public Task<OperationResult> SaveAsync(Guid tenantId, Guid userId, EventSave save)
    {
        if (EventRules.Check(save) is { } bad) return Task.FromResult(bad);
        lock (_lock)
        {
            var e = save.EventId is Guid id ? _events.FirstOrDefault(x => x.EventId == id) : null;
            if (save.EventId is not null && (e is null || e.OwnerId != userId || e.TenantId != tenantId))
                return Task.FromResult(OperationResult.Fail("Only the person who made this event can change it."));
            if (e is null) { e = new Ev { EventId = Guid.NewGuid(), TenantId = tenantId, OwnerId = userId }; _events.Add(e); }
            e.Title = save.Title; e.StartUtc = save.StartUtc; e.EndUtc = save.EndUtc; e.AllDay = save.AllDay; e.Location = save.Location; e.Notes = save.Notes; e.Color = save.Color;
            var valid = save.People.Where(pid => pid != userId && MockData.People.Values.Any(p => p.UserId == pid && p.TenantId == tenantId)).ToHashSet();
            foreach (var gone in e.People.Keys.Where(k => !valid.Contains(k)).ToList()) e.People.Remove(gone);
            foreach (var pid in valid) e.People.TryAdd(pid, EventResponse.Pending);
            return Task.FromResult(OperationResult.Ok(e.EventId));
        }
    }

    public Task<bool> DeleteAsync(Guid tenantId, Guid userId, Guid eventId)
    {
        lock (_lock) return Task.FromResult(_events.RemoveAll(e => e.EventId == eventId && e.TenantId == tenantId && e.OwnerId == userId) > 0);
    }

    public Task<bool> RespondAsync(Guid tenantId, Guid userId, Guid eventId, EventResponse response)
    {
        lock (_lock)
        {
            var e = _events.FirstOrDefault(x => x.EventId == eventId && x.TenantId == tenantId);
            if (e is null || !e.People.ContainsKey(userId)) return Task.FromResult(false);
            e.People[userId] = response;
            return Task.FromResult(true);
        }
    }
}

/// <summary>In-memory mail with a few sample messages for Acme. Lost on restart.</summary>
public sealed class MockMailService : IMailService
{
    private sealed class Msg
    {
        public Guid MessageId; public Guid TenantId; public Guid FromUserId; public string ToText = ""; public string CcText = "";
        public string Subject = ""; public string BodyHtml = ""; public string Preview = ""; public DateTime? SentUtc; public DateTime UpdatedUtc;
    }
    private sealed class Box { public MailFolder Folder; public bool IsRead; public bool IsStarred; }

    private readonly Dictionary<Guid, Msg> _messages = new();
    private readonly Dictionary<(Guid Msg, Guid User), Box> _boxes = new();
    private readonly object _lock = new();

    public MockMailService()
    {
        MockData.Person P(string email) => MockData.People[email];
        var priya = P("priya.sharma@acme.com"); var arjun = P("arjun.nair@acme.com"); var fatima = P("fatima.khan@acme.com");
        var rohan = P("rohan.das@acme.com"); var meera = P("meera.iyer@acme.com");
        string To(params MockData.Person[] ps) => string.Join("; ", ps.Select(p => $"{p.DisplayName} <{p.Email}>"));
        void M(MockData.Person from, MockData.Person[] to, string subject, string body, double hoursAgo, bool read = false, bool star = false)
        {
            var m = new Msg { MessageId = Guid.NewGuid(), TenantId = MockData.AcmeId, FromUserId = from.UserId, ToText = To(to), Subject = subject,
                BodyHtml = body, Preview = MailText.Preview(body), SentUtc = DateTime.UtcNow.AddHours(-hoursAgo), UpdatedUtc = DateTime.UtcNow.AddHours(-hoursAgo) };
            _messages[m.MessageId] = m;
            _boxes[(m.MessageId, from.UserId)] = new Box { Folder = MailFolder.Sent, IsRead = true };
            foreach (var t in to) _boxes[(m.MessageId, t.UserId)] = new Box { Folder = MailFolder.Inbox, IsRead = read, IsStarred = star };
        }
        M(fatima, [priya], "Q3 numbers are ready",
          "<p>Hi Priya,</p><p>The Q3 numbers are in the <b>Finance Q3</b> workbook. Revenue is up <b>12%</b> on last quarter; travel is a little over budget.</p><p>Can we go through it on Tuesday?</p><p>Thanks,<br>Fatima</p>", 1.2, star: true);
        M(arjun, [priya, fatima], "Shree Textiles wants a demo",
          "<p>Hello both,</p><p>Shree Textiles liked the order sheet and want a demo at their Surat office this Wednesday. I have put it in the calendar.</p><p>Arjun</p>", 3.5);
        M(rohan, [priya], "December release: what's in",
          "<p>Priya,</p><p>Here is the short list for December:</p><ul><li>Faster sign-in</li><li>Planner board colours</li><li>Export to PDF from Slides</li></ul><p>Let me know if anything is missing.</p><p>Rohan</p>", 20, read: true);
        M(meera, [priya], "Thank you for the welcome!",
          "<p>Dear Priya,</p><p>Thank you for the warm welcome to the team. I am looking forward to lunch on Thursday.</p><p>Warm regards,<br>Meera</p>", 28, read: true, star: true);
        M(priya, [arjun, fatima, rohan, meera], "Diwali office celebration",
          "<p>Dear all,</p><p>We are celebrating Diwali on the office terrace. Sweets, rangoli and the team photo. Please accept the invite in Vaijayanti Days.</p><p>Priya</p>", 46);
        M(fatima, [priya], "Payroll closes next week",
          "<p>A reminder that payroll closes next week. Please send any changes before then.</p><p>Fatima</p>", 70, read: true);
        M(arjun, [priya], "Travel claim for Surat",
          "<p>Hi Priya, I have attached my travel claim for the Surat visit in the Workbook. Thank you!</p>", 96, read: true);
        // a draft
        var d = new Msg { MessageId = Guid.NewGuid(), TenantId = MockData.AcmeId, FromUserId = priya.UserId, ToText = To(rohan), Subject = "Office laptops",
            BodyHtml = "<p>Rohan, can you check which laptops need</p>", UpdatedUtc = DateTime.UtcNow.AddHours(-5) };
        d.Preview = MailText.Preview(d.BodyHtml);
        _messages[d.MessageId] = d;
        _boxes[(d.MessageId, priya.UserId)] = new Box { Folder = MailFolder.Drafts, IsRead = true };
    }

    private static MockData.Person? Person(Guid id) => MockData.People.Values.FirstOrDefault(p => p.UserId == id);

    private MailSummaryDto Summary(Msg m, Box b)
    {
        var f = Person(m.FromUserId);
        return new(m.MessageId, b.Folder, m.FromUserId, f?.DisplayName ?? "", f?.Email ?? "", m.ToText, m.Subject, m.Preview,
            m.SentUtc ?? m.UpdatedUtc, b.IsRead, b.IsStarred, m.SentUtc is null);
    }

    public Task<List<MailSummaryDto>> ListAsync(Guid tenantId, Guid userId, MailFolder folder, bool starred = false)
    {
        lock (_lock)
            return Task.FromResult(_boxes.Where(kv => kv.Key.User == userId && _messages[kv.Key.Msg].TenantId == tenantId
                    && (starred ? kv.Value.IsStarred && kv.Value.Folder != MailFolder.Trash : kv.Value.Folder == folder))
                .Select(kv => Summary(_messages[kv.Key.Msg], kv.Value)).OrderByDescending(s => s.WhenUtc).ToList());
    }

    public Task<MailMessageDto?> OpenAsync(Guid tenantId, Guid userId, Guid messageId)
    {
        lock (_lock)
        {
            if (!_boxes.TryGetValue((messageId, userId), out var b) || _messages[messageId].TenantId != tenantId) return Task.FromResult<MailMessageDto?>(null);
            b.IsRead = true;
            var m = _messages[messageId]; var f = Person(m.FromUserId);
            return Task.FromResult<MailMessageDto?>(new MailMessageDto(m.MessageId, b.Folder, m.FromUserId, f?.DisplayName ?? "", f?.Email ?? "", m.ToText, m.CcText,
                m.Subject, m.BodyHtml, m.SentUtc ?? m.UpdatedUtc, b.IsRead, b.IsStarred, m.SentUtc is null));
        }
    }

    public Task<OperationResult> SaveAsync(Guid tenantId, Guid userId, MailSend send)
    {
        lock (_lock)
        {
            Msg? m = null;
            if (send.DraftId is Guid id && _messages.TryGetValue(id, out m)
                && (m.FromUserId != userId || m.TenantId != tenantId || m.SentUtc is not null))
                return Task.FromResult(OperationResult.Fail("This draft isn't yours, or it was already sent."));
            if (m is null) { m = new Msg { MessageId = send.DraftId ?? Guid.NewGuid(), TenantId = tenantId, FromUserId = userId }; _messages[m.MessageId] = m; }
            m.ToText = send.ToText ?? ""; m.CcText = send.CcText ?? ""; m.Subject = send.Subject ?? ""; m.BodyHtml = MailText.Clean(send.BodyHtml);
            m.Preview = MailText.Preview(m.BodyHtml); m.UpdatedUtc = DateTime.UtcNow; m.SentUtc = send.IsDraft ? null : DateTime.UtcNow;
            _boxes[(m.MessageId, userId)] = new Box { Folder = send.IsDraft ? MailFolder.Drafts : MailFolder.Sent, IsRead = true };
            if (!send.IsDraft)
                foreach (var r in send.Recipients.Distinct())
                    if (r != userId && Person(r) is { IsActive: true } p && p.TenantId == tenantId && !_boxes.ContainsKey((m.MessageId, r)))
                        _boxes[(m.MessageId, r)] = new Box { Folder = MailFolder.Inbox };
            return Task.FromResult(OperationResult.Ok(m.MessageId));
        }
    }

    public Task<bool> UpdateAsync(Guid tenantId, Guid userId, Guid messageId, MailFolder? folder = null, bool? isRead = null, bool? isStarred = null)
    {
        lock (_lock)
        {
            if (!_boxes.TryGetValue((messageId, userId), out var b) || _messages[messageId].TenantId != tenantId) return Task.FromResult(false);
            if (folder is { } f) b.Folder = f;
            if (isRead is { } r) b.IsRead = r;
            if (isStarred is { } s) b.IsStarred = s;
            return Task.FromResult(true);
        }
    }

    public Task<int> DeleteAsync(Guid tenantId, Guid userId, Guid messageId)
    {
        lock (_lock)
        {
            if (!_boxes.TryGetValue((messageId, userId), out var b) || _messages[messageId].TenantId != tenantId) return Task.FromResult(0);
            if (b.Folder is MailFolder.Trash or MailFolder.Drafts)
            {
                _boxes.Remove((messageId, userId));
                if (!_boxes.Keys.Any(k => k.Msg == messageId)) _messages.Remove(messageId);
                return Task.FromResult(2);
            }
            b.Folder = MailFolder.Trash;
            return Task.FromResult(1);
        }
    }

    public Task<MailCountsDto> CountsAsync(Guid tenantId, Guid userId)
    {
        lock (_lock)
        {
            var mine = _boxes.Where(kv => kv.Key.User == userId && _messages[kv.Key.Msg].TenantId == tenantId).Select(kv => kv.Value).ToList();
            return Task.FromResult(new MailCountsDto(mine.Count(b => b.Folder == MailFolder.Inbox && !b.IsRead), mine.Count(b => b.Folder == MailFolder.Drafts),
                mine.Count(b => b.IsStarred && b.Folder != MailFolder.Trash), mine.Count(b => b.Folder == MailFolder.Trash)));
        }
    }
}
