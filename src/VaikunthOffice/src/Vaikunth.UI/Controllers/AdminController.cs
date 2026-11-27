using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Vaikunth.UI.Infrastructure;
using Vaikunth.DtoLibrary;
using Vaikunth.ServiceLibrary;

namespace Vaikunth.UI.Controllers;

public sealed class AdminViewModel
{
    public SuiteStatsDto Stats { get; init; } = new(0, 0, 0, 0, 0, 0, 0);
    public List<TenantDto> Clients { get; init; } = new();
    public List<PlanDto> Plans { get; init; } = new();
    public string Search { get; init; } = "";
    public string Show { get; init; } = "all";
    public bool MockMode { get; init; }
}

/// <summary>
/// Super admin: the Clients page. Create a client company with its first admin, change its plan,
/// user and storage limits and which apps it gets, or switch it off. Available in every app at /admin.
/// </summary>
[Authorize(Roles = AppRoles.SuperAdmin)]
[Route("admin")]
public sealed class AdminController : Controller
{
    private readonly ITenantService _tenants;
    private readonly IConfiguration _config;
    private readonly IPasswordLinkService _links;
    private readonly IAccountEmailService _emails;
    private readonly SuiteOptions _suite;

    public AdminController(ITenantService tenants, IConfiguration config, IPasswordLinkService links, IAccountEmailService emails, IOptions<SuiteOptions> suite)
    {
        _tenants = tenants;
        _config = config;
        _links = links;
        _emails = emails;
        _suite = suite.Value;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? q = null, string show = "all")
    {
        var clients = await _tenants.ListAsync();
        if (!string.IsNullOrWhiteSpace(q))
            clients = clients.Where(c => c.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || c.Slug.Contains(q, StringComparison.OrdinalIgnoreCase)
                                       || (c.AdminEmail ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)).ToList();
        if (show == "active") clients = clients.Where(c => c.IsActive).ToList();
        if (show == "off") clients = clients.Where(c => !c.IsActive).ToList();
        return View(new AdminViewModel
        {
            Stats = await _tenants.StatsAsync(), Clients = clients, Plans = await _tenants.GetPlansAsync(),
            Search = q ?? "", Show = show, MockMode = ServiceRegistration.UsesMockData(_config)
        });
    }

    [HttpPost("create"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateTenantRequest request, int[]? apps)
    {
        request.Apps = Flags(apps);
        var result = await _tenants.CreateAsync(request);
        if (!result.Succeeded) { TempData["Error"] = result.Error; return RedirectToAction(nameof(Index)); }

        if (!request.SendInvite)
        {
            TempData["Ok"] = $"{request.Name} is set up. {request.AdminEmail} can sign in with the temporary password you chose.";
            return RedirectToAction(nameof(Index));
        }
        var sent = await SendWelcomeAsync(result.AdminUserId, request.AdminEmail.Trim(), request.AdminName.Trim(), request.Name.Trim());
        TempData[sent ? "Ok" : "Error"] = sent
            ? $"{request.Name} is set up. A welcome email with a link to set the password is on its way to {request.AdminEmail}."
            : $"{request.Name} is set up, but the welcome email couldn't be sent. Use Resend invite.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>A fresh welcome link for a client admin who hasn't set a password yet.</summary>
    [HttpPost("resend"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendInvite(Guid tenantId)
    {
        var client = (await _tenants.ListAsync()).FirstOrDefault(t => t.TenantId == tenantId);
        if (client?.AdminUserId is not Guid adminId || client.AdminEmail is null) return RedirectToAction(nameof(Index));
        var sent = await SendWelcomeAsync(adminId, client.AdminEmail, client.AdminEmail.Split('@')[0], client.Name);
        TempData[sent ? "Ok" : "Error"] = sent
            ? $"A new welcome email is on its way to {client.AdminEmail}."
            : "A link was sent less than a minute ago, or the email couldn't be sent. Try again shortly.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<bool> SendWelcomeAsync(Guid adminUserId, string email, string name, string company) =>
        await _links.CreateAsync(adminUserId, PasswordLinkPurpose.Invite) is string token
        && await _emails.SendClientWelcomeAsync(email, name, company, _suite.SetPasswordUrl(Request, token));

    [HttpPost("update"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(UpdateTenantRequest request, int[]? apps)
    {
        request.Apps = Flags(apps);
        var result = await _tenants.UpdateAsync(request);
        TempData[result.Succeeded ? "Ok" : "Error"] = result.Succeeded ? $"{request.Name} is updated." : result.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("active"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(Guid tenantId, bool isActive, string? name)
    {
        await _tenants.SetActiveAsync(tenantId, isActive);
        TempData["Ok"] = isActive ? $"{name} is switched on." : $"{name} is switched off. Its people can't sign in until you switch it back on.";
        return RedirectToAction(nameof(Index));
    }

    private static SuiteAppFlags Flags(int[]? apps) => (SuiteAppFlags)(apps ?? []).Aggregate(0, (all, a) => all | a);
}
