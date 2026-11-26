using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vaikunth.DtoLibrary;
using Vaikunth.ServiceLibrary;

namespace Vaikunth.UI.Controllers;

public sealed class SetupViewModel
{
    [Required(ErrorMessage = "Enter your name.")]
    public string DisplayName { get; set; } = "";

    [Required(ErrorMessage = "Enter your email."), EmailAddress(ErrorMessage = "That doesn't look like an email address.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Choose a password."), MinLength(8, ErrorMessage = "At least 8 characters.")]
    public string Password { get; set; } = "";

    [Compare(nameof(Password), ErrorMessage = "The two passwords don't match.")]
    public string ConfirmPassword { get; set; } = "";

    public bool MockMode { get; set; }
}

/// <summary>
/// First run on a new database: creates the one super admin (Vaikunth_Users with TenantId NULL).
/// Once the super admin exists this page only says so.
/// </summary>
[AllowAnonymous]
[Route("setup")]
public sealed class SetupController : Controller
{
    private readonly IUserService _users;
    private readonly Microsoft.Extensions.Configuration.IConfiguration _config;

    public SetupController(IUserService users, Microsoft.Extensions.Configuration.IConfiguration config)
    {
        _users = users;
        _config = config;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var mock = ServiceRegistration.UsesMockData(_config);
        ViewData["Done"] = !mock && !await _users.SetupNeededAsync();
        return View(new SetupViewModel { MockMode = mock });
    }

    [HttpPost(""), ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(SetupViewModel model)
    {
        model.MockMode = ServiceRegistration.UsesMockData(_config);
        if (model.MockMode || !await _users.SetupNeededAsync()) return RedirectToAction(nameof(Index));
        if (!ModelState.IsValid) { ViewData["Done"] = false; return View(model); }

        var result = await _users.CreateAsync(new CreateUserRequest
        {
            TenantId = null, DisplayName = model.DisplayName, Email = model.Email, Password = model.Password, Role = AppRoles.SuperAdmin
        });
        if (!result.Succeeded)
        {
            ModelState.AddModelError("", result.Error ?? "Couldn't create the super admin.");
            ViewData["Done"] = false;
            return View(model);
        }
        TempData["SetupMessage"] = "Super admin created. Sign in to add your first client.";
        return Redirect("/account/login");
    }
}
