using System.Net.Mail;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vaikunth.DtoLibrary;
using Vaikunth.ServiceLibrary;
using Vaikunth.UI.Infrastructure;

namespace Vaikunth.UI.Controllers;

/// <summary>
/// "My account" (profile menu): anyone, the super admin included, changes their own name, email and password.
/// Every change needs the current password.
/// </summary>
[Authorize]
[Route("account/me")]
public sealed class MyAccountController : Controller
{
    private readonly IUserService _users;
    public MyAccountController(IUserService users) => _users = users;

    [HttpPost(""), ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(string? displayName, string? email, string? currentPassword, string? newPassword, string? confirmPassword, string? returnUrl)
    {
        displayName = (displayName ?? "").Trim();
        email = (email ?? "").Trim();
        if (displayName.Length is 0 or > 120) return Back(returnUrl, "Enter your name (up to 120 letters).");
        if (!MailAddress.TryCreate(email, out _) || email.Length > 200) return Back(returnUrl, "That doesn't look like an email address.");
        if (!string.IsNullOrEmpty(newPassword))
        {
            if (Passwords.Validate(newPassword) is { } why) return Back(returnUrl, why);
            if (newPassword != confirmPassword) return Back(returnUrl, "The two new passwords don't match.");
        }

        // the current password proves it's really them
        var check = await _users.SignInAsync(new LoginRequest { Email = User.Identity?.Name ?? "", Password = currentPassword ?? "" });
        if (!check.Succeeded || check.User?.UserId != User.UserId()) return Back(returnUrl, "Your current password isn't right.");

        var result = await _users.UpdateAccountAsync(User.UserId(), displayName, email, string.IsNullOrEmpty(newPassword) ? null : newPassword);
        if (!result.Succeeded) return Back(returnUrl, result.Error);

        // show the new name and email straight away
        var auth = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (auth.Principal?.Identity is ClaimsIdentity old)
        {
            var claims = old.Claims.Where(c => c.Type is not (ClaimTypes.Name or AppClaims.DisplayName)).ToList();
            claims.Add(new Claim(ClaimTypes.Name, email.ToLowerInvariant()));
            claims.Add(new Claim(AppClaims.DisplayName, displayName));
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(new ClaimsIdentity(claims, old.AuthenticationType)), auth.Properties ?? new AuthenticationProperties());
        }
        TempData["AccountDone"] = string.IsNullOrEmpty(newPassword) ? "Your account is updated." : "Your account and password are updated.";
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
    }

    private IActionResult Back(string? returnUrl, string? error)
    {
        TempData["AccountError"] = error ?? "Couldn't save your account.";
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
    }
}
