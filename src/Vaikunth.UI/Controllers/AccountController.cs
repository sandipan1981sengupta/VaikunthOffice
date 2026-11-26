using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Vaikunth.DtoLibrary;
using Vaikunth.ServiceLibrary;

namespace Vaikunth.UI.Controllers;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Enter your work email."), EmailAddress(ErrorMessage = "That doesn't look like an email address.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Enter your password.")]
    public string Password { get; set; } = "";

    public bool RememberMe { get; set; } = true;
    public string? ReturnUrl { get; set; }
    public bool MockMode { get; set; }
}

/// <summary>Sign in and out. Shared by the three apps; the cookie works across all of them.</summary>
[AllowAnonymous]
[Route("account")]
public sealed class AccountController : Controller
{
    private readonly IUserService _users;
    private readonly IUserPhotoService _photos;
    private readonly IConfiguration _config;

    public AccountController(IUserService users, IUserPhotoService photos, IConfiguration config)
    {
        _users = users;
        _photos = photos;
        _config = config;
    }

    [HttpGet("login")]
    public async Task<IActionResult> Login(string? returnUrl = null, string? email = null)
    {
        if (User.Identity?.IsAuthenticated == true) return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
        if (await _users.SetupNeededAsync()) return Redirect("/setup");
        return View(new LoginViewModel { ReturnUrl = returnUrl, Email = email ?? "", MockMode = ServiceRegistration.UsesMockData(_config) });
    }

    [HttpPost("login"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        model.MockMode = ServiceRegistration.UsesMockData(_config);
        if (!ModelState.IsValid) return View(model);

        var result = await _users.SignInAsync(new LoginRequest { Email = model.Email, Password = model.Password });
        if (!result.Succeeded || result.User is null)
        {
            ModelState.AddModelError("", result.Error ?? "Couldn't sign you in.");
            model.Password = "";
            return View(model);
        }

        var user = result.User;
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new Claim(ClaimTypes.Name, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(AppClaims.DisplayName, user.DisplayName),
            new Claim(AppClaims.AvatarTone, user.AvatarTone.ToString()),
            new Claim(AppClaims.Apps, ((byte)user.Apps).ToString()),
        };
        if (user.TenantId is Guid tenantId)
        {
            claims.Add(new Claim(AppClaims.TenantId, tenantId.ToString()));
            claims.Add(new Claim(AppClaims.TenantName, user.TenantName ?? ""));
        }
        if (await _photos.VersionAsync(user.UserId) is > 0 and var photo)
            claims.Add(new Claim(AppClaims.PhotoVersion, photo.ToString()));
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = model.RememberMe });

        // the super admin has no files of their own: straight to the Clients page
        var home = user.Role == AppRoles.SuperAdmin ? "/admin" : "/";
        return LocalRedirect(Url.IsLocalUrl(model.ReturnUrl) && model.ReturnUrl != "/" ? model.ReturnUrl! : home);
    }

    [HttpPost("logout"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }
}
