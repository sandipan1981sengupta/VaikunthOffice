using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Vaikunth.DtoLibrary;
using Vaikunth.ServiceLibrary;
using Vaikunth.UI.Infrastructure;

namespace Vaikunth.UI.Controllers;

public sealed class TeamViewModel
{
    public List<TeamMemberDto> People { get; init; } = new();
    public TenantDto? Company { get; init; }
    public Guid Me { get; init; }
    public string Show { get; init; } = "all";
    public bool EmailConfigured { get; init; }
}

/// <summary>
/// The client admin's People page (/team): invite the people in the office by email, resend links,
/// send a password reset, make someone an admin, or switch them off.
/// </summary>
[Authorize(Roles = AppRoles.TenantAdmin)]
[Route("team")]
public sealed class TeamController : Controller
{
    private readonly IUserService _users;
    private readonly ITenantService _tenants;
    private readonly IPasswordLinkService _links;
    private readonly IAccountEmailService _emails;
    private readonly IAppEmailSender _sender;
    private readonly SuiteOptions _suite;

    public TeamController(IUserService users, ITenantService tenants, IPasswordLinkService links, IAccountEmailService emails,
        IAppEmailSender sender, IOptions<SuiteOptions> suite)
    {
        _users = users;
        _tenants = tenants;
        _links = links;
        _emails = emails;
        _sender = sender;
        _suite = suite.Value;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string show = "all")
    {
        var people = await _users.ListForTenantAsync(User.TenantId());
        var shown = show switch
        {
            "invited" => people.Where(p => p.Status == "Invited").ToList(),
            "off" => people.Where(p => p.Status == "Off").ToList(),
            _ => people
        };
        var company = (await _tenants.ListAsync()).FirstOrDefault(t => t.TenantId == User.TenantId());
        ViewData["AllCount"] = people.Count;
        ViewData["InvitedCount"] = people.Count(p => p.Status == "Invited");
        ViewData["OffCount"] = people.Count(p => p.Status == "Off");
        return View(new TeamViewModel { People = shown, Company = company, Me = User.UserId(), Show = show, EmailConfigured = _sender.IsConfigured });
    }

    [HttpPost("invite"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Invite(string displayName, string email, string role, string? department, string? jobTitle, DateOnly? joiningDate)
    {
        var result = await _users.InviteAsync(new InviteRequest
        {
            TenantId = User.TenantId(), InvitedBy = User.UserId(), DisplayName = displayName ?? "", Email = email ?? "",
            Role = role == AppRoles.TenantAdmin ? AppRoles.TenantAdmin : AppRoles.Member,
            Department = department, JobTitle = jobTitle, JoiningDate = joiningDate
        });
        if (!result.Succeeded) { TempData["Error"] = result.Error; return RedirectToAction(nameof(Index)); }

        var sent = await _links.CreateAsync(result.Id!.Value, PasswordLinkPurpose.Invite) is string token
                   && await _emails.SendMemberInviteAsync(email!.Trim(), displayName!.Trim(), User.TenantName(), User.DisplayName(), _suite.SetPasswordUrl(Request, token));
        TempData[sent ? "Ok" : "Error"] = sent
            ? $"{displayName} is added. An email with a link to set their password is on its way to {email}."
            : $"{displayName} is added, but the email couldn't be sent. Try Resend in a minute.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Invited people get a fresh welcome link; people with a password get a reset link.</summary>
    [HttpPost("resend"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Resend(Guid userId)
    {
        var person = (await _users.ListForTenantAsync(User.TenantId())).FirstOrDefault(p => p.UserId == userId);
        if (person is null || !person.IsActive) return RedirectToAction(nameof(Index));
        var purpose = person.IsInvited ? PasswordLinkPurpose.Invite : PasswordLinkPurpose.Reset;
        if (await _links.CreateAsync(userId, purpose) is not string token)
        {
            TempData["Error"] = $"A link was sent to {person.Email} less than a minute ago. Please wait a moment.";
            return RedirectToAction(nameof(Index));
        }
        var url = _suite.SetPasswordUrl(Request, token);
        var sent = person.IsInvited
            ? await _emails.SendMemberInviteAsync(person.Email, person.DisplayName, User.TenantName(), User.DisplayName(), url)
            : await _emails.SendPasswordResetAsync(person.Email, person.DisplayName, url);
        TempData[sent ? "Ok" : "Error"] = sent
            ? (person.IsInvited ? $"A new invite is on its way to {person.Email}." : $"A password reset link is on its way to {person.Email}.")
            : "The email couldn't be sent. Check the email settings.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Correct someone's name, department, job title or joining date.</summary>
    [HttpPost("details"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Details(Guid userId, string displayName, string? department, string? jobTitle, DateOnly? joiningDate)
    {
        var result = await _users.UpdateMemberAsync(new UpdateMemberRequest
        {
            TenantId = User.TenantId(), UserId = userId, DisplayName = displayName ?? "",
            Department = department, JobTitle = jobTitle, JoiningDate = joiningDate
        });
        TempData[result.Succeeded ? "Ok" : "Error"] = result.Succeeded ? $"{displayName?.Trim()}'s details are saved." : result.Error;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("active"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(Guid userId, bool isActive, string? name)
    {
        if (userId == User.UserId()) { TempData["Error"] = "You can't switch yourself off."; return RedirectToAction(nameof(Index)); }
        await _users.SetActiveAsync(User.TenantId(), userId, isActive);
        TempData["Ok"] = isActive ? $"{name} can sign in again." : $"{name} is switched off and can't sign in. Their files stay.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("role"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SetRole(Guid userId, string role, string? name)
    {
        if (userId == User.UserId()) { TempData["Error"] = "Ask another admin to change your own role."; return RedirectToAction(nameof(Index)); }
        await _users.SetRoleAsync(User.TenantId(), userId, role);
        TempData["Ok"] = role == AppRoles.TenantAdmin ? $"{name} is now an admin." : $"{name} is now a member.";
        return RedirectToAction(nameof(Index));
    }
}
