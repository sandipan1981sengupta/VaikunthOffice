namespace Vaikunth.DtoLibrary;

// ===================== Vaijayanti Days (calendar) =====================

/// <summary>How an invited person answered.</summary>
public enum EventResponse : byte { Pending = 0, Yes = 1, No = 2, Maybe = 3 }

/// <summary>Someone invited to an event.</summary>
public sealed record EventPersonDto(Guid UserId, string Name, string Email, EventResponse Response);

/// <summary>One event in a person's calendar: their own, or one they were invited to.</summary>
public sealed record CalendarEventDto(
    Guid EventId, Guid OwnerId, string OwnerName, string Title,
    /// <summary>UTC. For an all-day event: midnight UTC of the first day, and of the day after the last.</summary>
    DateTime StartUtc, DateTime EndUtc, bool AllDay, string? Location, string? Notes, string Color,
    List<EventPersonDto> People)
{
    public bool IsMine(Guid userId) => OwnerId == userId;
}

/// <summary>A new or changed event (only its owner can change it).</summary>
public sealed class EventSave
{
    public Guid? EventId { get; set; }
    public string Title { get; set; } = "";
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public bool AllDay { get; set; }
    public string? Location { get; set; }
    public string? Notes { get; set; }
    /// <summary>indigo | teal | saffron | rose | green | violet | slate</summary>
    public string Color { get; set; } = "indigo";
    /// <summary>People of the same company invited to it.</summary>
    public List<Guid> People { get; set; } = new();
}

public static class EventColors
{
    public static readonly IReadOnlyList<string> All = ["indigo", "teal", "saffron", "rose", "green", "violet", "slate"];
    public static string Clean(string? c) => All.Contains(c ?? "") ? c! : "indigo";
}

// ===================== Sudarshana Inbox (mail) =====================

public enum MailFolder : byte { Inbox = 1, Sent = 2, Drafts = 3, Trash = 4 }

/// <summary>A row of a mail folder (no body, so lists stay light).</summary>
public sealed record MailSummaryDto(
    Guid MessageId, MailFolder Folder, Guid FromUserId, string FromName, string FromEmail, string ToText,
    string Subject, string Preview, DateTime WhenUtc, bool IsRead, bool IsStarred, bool IsDraft);

/// <summary>One message, opened.</summary>
public sealed record MailMessageDto(
    Guid MessageId, MailFolder Folder, Guid FromUserId, string FromName, string FromEmail, string ToText, string CcText,
    string Subject, string BodyHtml, DateTime WhenUtc, bool IsRead, bool IsStarred, bool IsDraft);

/// <summary>Send a message, or keep it as a draft. Recipients are already split by the caller into the company's
/// own people (delivered to their Inbox) and other addresses (sent out by email).</summary>
public sealed class MailSend
{
    public Guid? DraftId { get; set; }
    public string ToText { get; set; } = "";
    public string CcText { get; set; } = "";
    public string Subject { get; set; } = "";
    public string BodyHtml { get; set; } = "";
    public bool IsDraft { get; set; }
    /// <summary>The company's people who get it in their Inbox (To and Cc together).</summary>
    public List<Guid> Recipients { get; set; } = new();
}

/// <summary>Unread and draft counts for the folder list.</summary>
public sealed record MailCountsDto(int UnreadInbox, int Drafts, int Starred, int Trash);
