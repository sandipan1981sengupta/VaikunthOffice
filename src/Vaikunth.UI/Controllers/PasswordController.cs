using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Vaikunth.DtoLibrary;
using Vaikunth.ServiceLibrary;
using Vaikunth.UI.Infrastructure;

namespace Vaikunth.UI.Controllers;

public sealed class ForgotViewModel
{
    [Required(ErrorMessage = "Enter your work email."), EmailAddress(ErrorMessage = "That doesn't look like an email address.")]
    public string Email { get; set; } = "";
    public bool Sent { get; set; }
}

public sealed class SetPasswordViewModel
{
    public string Token { get; set; } = "";

    [Required(ErrorMessage = "Choose a password."), MinLength(8, ErrorMessage = "At least 8 characters.")]
    public string Password { get; set; } = "";

    [Compare(nameof(Password), ErrorMessage = "The two passwords don't match.")]
    public string ConfirmPassword { get; set; } = "";

    public PasswordLinkInfo? Link { get; set; }
}

/// <summary>"Forgot password?" and the page an emailed link opens (welcome, invite or reset).</summary>
[AllowAnonymous]
[Route("account")]
public sealed class PasswordController : Controller
{
    private readonly IUserService _users;
    private readonly IPasswordLinkService _links;
    private readonly IAccountEmailService _emails;
    private readonly SuiteOptions _suite;

    public PasswordController(IUserService users, IPasswordLinkService links, IAccountEmailService emails, IOptions<SuiteOptions> suite)
    {
        _users = users;
        _links = links;
        _emails = emails;
        _suite = suite.Value;
    }

    [HttpGet("forgot")]
    public IActionResult Forgot(string? email = null) => View(new ForgotViewModel { Email = email ?? "" });

    /// <summary>Always answers the same way, so the page can't be used to find out who has an account.</summary>
    [HttpPost("forgot"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Forgot(ForgotViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await _users.FindByEmailAsync(model.Email);
        if (user is { IsActive: true, TenantActive: true } && await _links.CreateAsync(user.UserId, PasswordLinkPurpose.Reset) is string token)
            await _emails.SendPasswordResetAsync(user.Email, user.DisplayName, _suite.SetPasswordUrl(Request, token));
        return View(new ForgotViewModel { Email = model.Email, Sent = true });
    }

    [HttpGet("set-password")]
    public async Task<IActionResult> SetPassword(string? token)
    {
        var link = string.IsNullOrEmpty(token) ? null : await _links.GetAsync(token);
        return View(new SetPasswordViewModel { Token = token ?? "", Link = link });
    }

    [HttpPost("set-password"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPassword(SetPasswordViewModel model)
    {
        model.Link = await _links.GetAsync(model.Token);
        if (!ModelState.IsValid || model.Link is not { IsUsable: true }) return View(model);

        switch (await _links.UseAsync(model.Token, model.Password))
        {
            case PasswordLinkUseResult.Done:
                TempData["SetupMessage"] = "Your password is set. Sign in with it now.";
                return Redirect($"/account/login?email={Uri.EscapeDataString(model.Link.Email)}");
            case PasswordLinkUseResult.Inactive:
                ModelState.AddModelError("", "This account is switched off. Ask your admin.");
                return View(model);
            default:
                ModelState.AddModelError("", "This link has already been used or has run out. Ask for a new one.");
                return View(model);
        }
    }
}
