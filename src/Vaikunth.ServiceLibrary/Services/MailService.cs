using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Vaikunth.DtoLibrary;
using Vaikunth.ServiceLibrary.Data;

namespace Vaikunth.ServiceLibrary;

/// <summary>
/// Sudarshana Inbox: mail between the people of one company. Each person has their own copy of a message
/// (folder, read, starred); a message sent outside the company goes out by email as well.
/// </summary>
public interface IMailService
{
    /// <summary>A folder, or with <paramref name="starred"/> every starred message outside Trash.</summary>
    Task<List<MailSummaryDto>> ListAsync(Guid tenantId, Guid userId, MailFolder folder, bool starred = false);
    /// <summary>Opens a message; it becomes read.</summary>
    Task<MailMessageDto?> OpenAsync(Guid tenantId, Guid userId, Guid messageId);
    Task<OperationResult> SaveAsync(Guid tenantId, Guid userId, MailSend send);
    Task<bool> UpdateAsync(Guid tenantId, Guid userId, Guid messageId, MailFolder? folder = null, bool? isRead = null, bool? isStarred = null);
    /// <summary>0 = not found, 1 = moved to Trash, 2 = gone for good.</summary>
    Task<int> DeleteAsync(Guid tenantId, Guid userId, Guid messageId);
    Task<MailCountsDto> CountsAsync(Guid tenantId, Guid userId);
}

public static partial class MailText
{
    [GeneratedRegex("<(script|style)[^>]*>.*?</\\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)] private static partial Regex Blocks();
    [GeneratedRegex("<[^>]+>")] private static partial Regex Tags();
    [GeneratedRegex("\\s+")] private static partial Regex Spaces();
    [GeneratedRegex("<(script|iframe|object|embed|link|meta|style)[^>]*>(.*?</\\1>)?", RegexOptions.IgnoreCase | RegexOptions.Singleline)] private static partial Regex Unsafe();
    [GeneratedRegex("\\son\\w+\\s*=\\s*(\"[^\"]*\"|'[^']*'|[^\\s>]+)", RegexOptions.IgnoreCase)] private static partial Regex Handlers();
    [GeneratedRegex("(href|src)\\s*=\\s*([\"'])\\s*javascript:[^\"']*\\2", RegexOptions.IgnoreCase)] private static partial Regex JsLinks();

    /// <summary>The first words of a message, as plain text.</summary>
    public static string Preview(string? html)
    {
        var text = WebUtility.HtmlDecode(Tags().Replace(Blocks().Replace(html ?? "", " "), " "));
        text = Spaces().Replace(text, " ").Trim();
        return text.Length > 280 ? text[..280] : text;
    }

    /// <summary>Removes scripts, frames and event handlers from a message body before it is kept.</summary>
    public static string Clean(string? html) => JsLinks().Replace(Handlers().Replace(Unsafe().Replace(html ?? "", ""), ""), "$1=\"#\"");

    /// <summary>"Arjun <arjun@x.com>; meera@x.com" → the addresses, lower case, no repeats.</summary>
    public static List<string> Addresses(string? text)
    {
        var list = new List<string>();
        foreach (Match m in Regex.Matches(text ?? "", "[A-Za-z0-9._%+'-]+@[A-Za-z0-9.-]+\\.[A-Za-z]{2,}"))
        {
            var a = m.Value.ToLowerInvariant();
            if (!list.Contains(a)) list.Add(a);
        }
        return list;
    }
}

public sealed class MailService(SqlDb db) : IMailService
{
    private static MailSummaryDto Summary(SqlDataReader r) => new(
        r.G("MessageId"), (MailFolder)Convert.ToByte(r["Folder"]), r.G("FromUserId"), r.S("FromName"), r.S("FromEmail"), r.S("ToText"),
        r.S("Subject"), r.S("Preview"), r.Utc("WhenUtc"), r.B("IsRead"), r.B("IsStarred"), r.B("IsDraft"));

    public Task<List<MailSummaryDto>> ListAsync(Guid tenantId, Guid userId, MailFolder folder, bool starred = false) =>
        db.QueryAsync("dbo.Vaikunth_Mail_List", p => p.With("@TenantId", tenantId).With("@UserId", userId)
            .With("@Folder", (byte)folder).With("@Starred", starred).With("@Take", 300), Summary);

    public Task<MailMessageDto?> OpenAsync(Guid tenantId, Guid userId, Guid messageId) =>
        db.QueryFirstAsync("dbo.Vaikunth_Mail_Get", p => p.With("@TenantId", tenantId).With("@UserId", userId).With("@MessageId", messageId),
            r => new MailMessageDto(r.G("MessageId"), (MailFolder)Convert.ToByte(r["Folder"]), r.G("FromUserId"), r.S("FromName"), r.S("FromEmail"),
                r.S("ToText"), r.S("CcText"), r.S("Subject"), r.S("BodyHtml"), r.Utc("WhenUtc"), r.B("IsRead"), r.B("IsStarred"), r.B("IsDraft")));

    public async Task<OperationResult> SaveAsync(Guid tenantId, Guid userId, MailSend send)
    {
        var id = send.DraftId ?? Guid.NewGuid();
        var body = MailText.Clean(send.BodyHtml);
        var rows = await db.QueryAsync("dbo.Vaikunth_Mail_Save", p => p
            .With("@MessageId", id).With("@TenantId", tenantId).With("@UserId", userId)
            .AddText("@ToText", Cut(send.ToText, 2000), 2000).AddText("@CcText", Cut(send.CcText, 2000), 2000)
            .AddText("@Subject", Cut(send.Subject, 300), 300).AddText("@BodyHtml", body, -1).AddText("@Preview", Cut(MailText.Preview(body), 300), 300)
            .With("@IsDraft", send.IsDraft).AddText("@Recipients", string.Join(",", send.Recipients.Distinct()), -1), r => r.I("Result"));
        return rows.FirstOrDefault() == 1 ? OperationResult.Ok(id) : OperationResult.Fail("This draft isn't yours, or it was already sent.");
    }

    private static string Cut(string? s, int n) { s = (s ?? "").Trim(); return s.Length > n ? s[..n] : s; }

    public async Task<bool> UpdateAsync(Guid tenantId, Guid userId, Guid messageId, MailFolder? folder = null, bool? isRead = null, bool? isStarred = null) =>
        (await db.QueryAsync("dbo.Vaikunth_Mail_Update", p => p.With("@TenantId", tenantId).With("@UserId", userId).With("@MessageId", messageId)
            .With("@Folder", folder is null ? null : (byte)folder).With("@IsRead", isRead).With("@IsStarred", isStarred), r => r.I("Result"))).FirstOrDefault() > 0;

    public async Task<int> DeleteAsync(Guid tenantId, Guid userId, Guid messageId) =>
        (await db.QueryAsync("dbo.Vaikunth_Mail_Delete", p => p.With("@TenantId", tenantId).With("@UserId", userId).With("@MessageId", messageId),
            r => r.I("Result"))).FirstOrDefault();

    public async Task<MailCountsDto> CountsAsync(Guid tenantId, Guid userId) =>
        await db.QueryFirstAsync("dbo.Vaikunth_Mail_Counts", p => p.With("@TenantId", tenantId).With("@UserId", userId),
            r => new MailCountsDto(r.I("UnreadInbox"), r.I("Drafts"), r.I("Starred"), r.I("Trash"))) ?? new MailCountsDto(0, 0, 0, 0);
}
