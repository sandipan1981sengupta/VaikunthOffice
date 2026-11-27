using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Vaikunth.ServiceLibrary;

namespace Vaikunth.UI.Controllers;

/// <summary>
/// Developer outbox: while Smtp:Host is empty, emails are saved to a folder instead of sent.
/// This page lists them so invites and password links can be tried. It only exists in Development
/// and only while SMTP isn't set up.
/// </summary>
[AllowAnonymous]
[Route("dev/outbox")]
public sealed class DevController : Controller
{
    private readonly IAppEmailSender _sender;
    private readonly SmtpOptions _smtp;
    private readonly IHostEnvironment _env;

    public DevController(IAppEmailSender sender, IOptions<SmtpOptions> smtp, IHostEnvironment env)
    {
        _sender = sender;
        _smtp = smtp.Value;
        _env = env;
    }

    private bool Allowed => _env.IsDevelopment() && !_sender.IsConfigured;

    [HttpGet("")]
    public IActionResult Index() => Allowed ? View("~/Views/Dev/Outbox.cshtml", Outbox.List(_smtp)) : NotFound();

    [HttpGet("{file}")]
    public IActionResult Email(string file)
    {
        if (!Allowed) return NotFound();
        var html = Outbox.Read(_smtp, file);
        return html is null ? NotFound() : Content(html, "text/html");
    }
}
