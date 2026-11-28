using System.Text.RegularExpressions;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Vaikunth.ServiceLibrary;

/// <summary>appsettings "Smtp". Leave Host empty to keep emails in the local outbox instead of sending them.</summary>
public sealed class SmtpOptions
{
    public string Host { get; set; } = "";
    /// <summary>465 = SSL from the first byte (Plesk / Windows hosting). 587 = STARTTLS (Gmail, Brevo, Office 365).</summary>
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string UserName { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromAddress { get; set; } = "";
    public string FromName { get; set; } = "Vaikunth Desk";
    /// <summary>Where unsent emails are written while Host is empty (shared by the three apps). Empty = temp folder.</summary>
    public string OutboxPath { get; set; } = "";

    public string Outbox => string.IsNullOrWhiteSpace(OutboxPath) ? Path.Combine(Path.GetTempPath(), "vaikunth-office-outbox") : OutboxPath;
}

public interface IAppEmailSender
{
    /// <summary>True when a real mail server is set up (Smtp:Host).</summary>
    bool IsConfigured { get; }
    /// <summary>True when the mail server accepted the message (or it was saved to the outbox). Never throws: failures are logged.</summary>
    Task<bool> SendAsync(string to, string subject, string html);
    /// <summary>Why the last email to this address failed, in plain words (null when it went out).</summary>
    string? LastErrorFor(string to);
}

/// <summary>
/// SMTP through MailKit. When Smtp:Host is empty the email is saved as an .html file in the outbox folder,
/// so invites and password links can be tried on a developer machine (see /dev/outbox).
/// </summary>
public sealed class SmtpEmailSender : IAppEmailSender
{
    private readonly SmtpOptions _o;
    private readonly ILogger<SmtpEmailSender> _log;

    public SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> log)
    {
        _o = options.Value;
        _log = log;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_o.Host);

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _errors = new(StringComparer.OrdinalIgnoreCase);
    public string? LastErrorFor(string to) => _errors.TryGetValue(to.Trim(), out var e) ? e : null;

    /// <summary>What went wrong, said so the super admin can fix the Smtp settings.</summary>
    private string Explain(Exception ex) => ex switch
    {
        MailKit.Security.AuthenticationException => $"the mail server refused the user name or password ({_o.UserName}). For Gmail or Office 365 use an app password.",
        MailKit.Security.SslHandshakeException => $"the secure connection failed. Port 465 needs SSL from the start, port 587 needs STARTTLS (EnableSsl true for both); check Port {_o.Port}.",
        System.Net.Sockets.SocketException or OperationCanceledException or TimeoutException => $"the mail server {_o.Host}:{_o.Port} can't be reached from this server (wrong host or port, or a firewall).",
        SmtpCommandException c when c.ErrorCode == SmtpErrorCode.SenderNotAccepted => $"the mail server won't send from {(string.IsNullOrWhiteSpace(_o.FromAddress) ? _o.UserName : _o.FromAddress)}. FromAddress must be an address that account may send from. ({c.Message})",
        SmtpCommandException c when c.ErrorCode == SmtpErrorCode.RecipientNotAccepted => $"the mail server refused the address. ({c.Message})",
        SmtpCommandException c => $"the mail server said: {c.Message} (code {(int)c.StatusCode}).",
        ParseException => "the email address isn't valid.",
        _ => ex.Message
    };

    public async Task<bool> SendAsync(string to, string subject, string html)
    {
        if (!IsConfigured) return await SaveToOutboxAsync(to, subject, html);
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_o.FromName, string.IsNullOrWhiteSpace(_o.FromAddress) ? _o.UserName : _o.FromAddress));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;
            message.Body = new BodyBuilder { HtmlBody = html, TextBody = Regex.Replace(html, "<[^>]+>", " ") }.ToMessageBody();

            using var client = new SmtpClient();
            var socket = !_o.EnableSsl ? SecureSocketOptions.None : _o.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
            await client.ConnectAsync(_o.Host, _o.Port, socket);
            if (!string.IsNullOrWhiteSpace(_o.UserName)) await client.AuthenticateAsync(_o.UserName, _o.Password);
            await client.SendAsync(new MailKit.SendRequest(message));   // MailKit 5: messages are sent as a SendRequest
            await client.DisconnectAsync(true);
            _log.LogInformation("Email \"{Subject}\" sent to {To}.", subject, to);
            _errors.TryRemove(to.Trim(), out _);
            return true;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Email \"{Subject}\" to {To} could not be sent. Check the Smtp settings.", subject, to);
            _errors[to.Trim()] = Explain(ex);
            return false;
        }
    }

    private async Task<bool> SaveToOutboxAsync(string to, string subject, string html)
    {
        try
        {
            Directory.CreateDirectory(_o.Outbox);
            var name = $"{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{Regex.Replace(to, "[^a-zA-Z0-9@.-]", "_")}.html";
            var head = $"<!-- to: {System.Net.WebUtility.HtmlEncode(to)} -->\n<!-- subject: {System.Net.WebUtility.HtmlEncode(subject)} -->\n";
            await File.WriteAllTextAsync(Path.Combine(_o.Outbox, name), head + html);
            _log.LogWarning("Smtp:Host is empty, so the email \"{Subject}\" to {To} was saved to {Folder} instead of being sent.", subject, to, _o.Outbox);
            return true;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Couldn't save the email to the outbox.");
            return false;
        }
    }
}

/// <summary>One saved email in the outbox (shown on /dev/outbox while SMTP isn't set up).</summary>
public sealed record OutboxEmail(string File, string To, string Subject, DateTime SavedUtc);

public static class Outbox
{
    public static List<OutboxEmail> List(SmtpOptions o)
    {
        if (!Directory.Exists(o.Outbox)) return new();
        return Directory.GetFiles(o.Outbox, "*.html").OrderByDescending(f => f).Take(100).Select(f =>
        {
            var head = File.ReadLines(f).Take(2).ToList();
            string Pick(int i, string key) => head.Count > i ? System.Net.WebUtility.HtmlDecode(head[i].Replace($"<!-- {key}: ", "").Replace(" -->", "")) : "";
            return new OutboxEmail(Path.GetFileName(f), Pick(0, "to"), Pick(1, "subject"), File.GetLastWriteTimeUtc(f));
        }).ToList();
    }

    public static string? Read(SmtpOptions o, string file)
    {
        var path = Path.Combine(o.Outbox, Path.GetFileName(file));   // no folders: only files in the outbox
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }
}
