using System.Net;

namespace Vaikunth.ServiceLibrary;

/// <summary>The account emails: welcome for a new client admin, invite for a member, and password reset.</summary>
public interface IAccountEmailService
{
    Task<bool> SendClientWelcomeAsync(string toEmail, string personName, string companyName, string setPasswordUrl);
    Task<bool> SendMemberInviteAsync(string toEmail, string personName, string companyName, string invitedBy, string setPasswordUrl);
    Task<bool> SendPasswordResetAsync(string toEmail, string personName, string setPasswordUrl);
}

public sealed class AccountEmailService : IAccountEmailService
{
    private readonly IAppEmailSender _email;
    public AccountEmailService(IAppEmailSender email) => _email = email;

    private static string E(string s) => WebUtility.HtmlEncode(s);

    public Task<bool> SendClientWelcomeAsync(string toEmail, string personName, string companyName, string setPasswordUrl) =>
        _email.SendAsync(toEmail, $"Welcome to Vaikunth Office, {companyName}", Layout(
            $"Welcome, {E(personName)}",
            $"<p><b>{E(companyName)}</b> now has Vaikunth Office: Akshara Pages, Vishwaroopa Slides, Hrishikesha Workbook, Shashvata Reader and Dhruva Planner.</p>" +
            "<p>You are the admin for your company. Set your password, then add the people in your office from the <b>People</b> page. Each of them gets an email like this one.</p>",
            "Set my password", setPasswordUrl, "This link works once and is valid for 7 days."));

    public Task<bool> SendMemberInviteAsync(string toEmail, string personName, string companyName, string invitedBy, string setPasswordUrl) =>
        _email.SendAsync(toEmail, $"{invitedBy} added you to Vaikunth Office", Layout(
            $"Hello {E(personName)}",
            $"<p><b>{E(invitedBy)}</b> added you to <b>{E(companyName)}</b> on Vaikunth Office.</p>" +
            "<p>Set your password to start writing documents, presenting slides, building workbooks, reading PDFs and planning projects.</p>",
            "Set my password", setPasswordUrl, "This link works once and is valid for 7 days."));

    public Task<bool> SendPasswordResetAsync(string toEmail, string personName, string setPasswordUrl) =>
        _email.SendAsync(toEmail, "Reset your Vaikunth Office password", Layout(
            $"Hello {E(personName)}",
            "<p>Someone (hopefully you) asked to reset your Vaikunth Office password.</p><p>Choose a new password with the button below.</p>",
            "Choose a new password", setPasswordUrl, "This link works once and is valid for 2 hours. If you didn't ask for it, ignore this email: your password stays the same."));

    /// <summary>Simple table layout that looks right in Gmail and Outlook.</summary>
    private static string Layout(string title, string body, string button, string url, string footnote) => $"""
        <!doctype html><html><head><meta charset="utf-8"></head><body style="margin:0;background:#F4F5FB;font-family:Segoe UI,Arial,sans-serif;color:#2B2E4A">
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#F4F5FB;padding:28px 12px"><tr><td align="center">
          <table role="presentation" width="560" cellpadding="0" cellspacing="0" style="max-width:560px;background:#ffffff;border-radius:18px;overflow:hidden;border:1px solid #E3E5EE">
            <tr><td style="background:linear-gradient(110deg,#1E2060,#2B2F86 55%,#14A3A0);background-color:#1E2060;padding:22px 28px;color:#fff">
              <span style="font-size:18px;letter-spacing:3px;font-weight:800">VAIKUNTH</span> <span style="font-size:18px;letter-spacing:3px;opacity:.75">OFFICE</span>
              <div style="font-size:12px;opacity:.85;margin-top:4px">Akshara Pages · Vishwaroopa Slides · Hrishikesha Workbook · Shashvata Reader · Dhruva Planner</div>
            </td></tr>
            <tr><td style="padding:28px">
              <h1 style="margin:0 0 14px;font-size:22px;color:#141634">{title}</h1>
              <div style="font-size:15px;line-height:1.6">{body}</div>
              <p style="margin:24px 0"><a href="{E(url)}" style="display:inline-block;background:#1E2060;color:#ffffff;text-decoration:none;font-weight:600;padding:12px 22px;border-radius:12px">{E(button)}</a></p>
              <p style="font-size:12.5px;color:#5F6383;margin:0 0 6px">{E(footnote)}</p>
              <p style="font-size:12px;color:#8A8EA8;margin:0;word-break:break-all">Button not working? Paste this into your browser: {E(url)}</p>
            </td></tr>
            <tr><td style="padding:16px 28px;background:#FBF6EA;font-size:12.5px;color:#1E2060;border-top:1px solid #F1E6C8">
              <i>Yogah karmasu kaushalam: Excellence in every action.</i> · Bhagavad Gita 2.50
            </td></tr>
          </table>
        </td></tr></table></body></html>
        """;
}
