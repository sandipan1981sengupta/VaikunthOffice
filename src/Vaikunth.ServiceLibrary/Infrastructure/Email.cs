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
    public string FromName { get; set; } = "Vaikunth Office";
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
            return true;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Email \"{Subject}\" to {To} could not be sent. Check the Smtp settings.", subject, to);
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
