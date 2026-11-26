using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vaikunth.DtoLibrary;
using Vaikunth.ServiceLibrary;
using Vaikunth.UI.Infrastructure;

namespace Vaikunth.UI.Controllers;

/// <summary>
/// Profile photos. They are stored in the database (Vaikunth_Users.Photo) with the rest of the person;
/// the browser shrinks a photo to 256 × 256 before it is sent, so each is only a few KB.
/// </summary>
[Authorize]
[Route("account/photo")]
public sealed class PhotoController : Controller
{
    private readonly IUserPhotoService _photos;
    public PhotoController(IUserPhotoService photos) => _photos = photos;

    /// <summary>Anyone's photo, for people of the same client. Cached by the browser until it changes (?v=).</summary>
    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> Get(Guid userId)
    {
        Guid? tenant = User.IsSuperAdmin() ? null : User.TenantId();
        var photo = await _photos.GetAsync(userId, tenant, User.IsSuperAdmin());
        if (photo is null) return NotFound();
        Response.Headers.CacheControl = "private, max-age=31536000, immutable";
        return File(photo.Data, photo.ContentType);
    }

    [HttpPost(""), ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload(IFormFile? photo, string? returnUrl)
    {
        if (photo is null || photo.Length == 0) return Back(returnUrl, "Choose a picture first.");
        if (photo.Length > UserPhotos.MaxBytes) return Back(returnUrl, "That picture is bigger than 2 MB. Choose a smaller one.");
        using var ms = new MemoryStream();
        await photo.CopyToAsync(ms);
        var bytes = ms.ToArray();
        var type = UserPhotos.Sniff(bytes);
        if (type is null) return Back(returnUrl, "That file isn't a picture. Use a PNG, JPEG or WebP.");
        var version = await _photos.SetAsync(User.UserId(), bytes, type);
        await RefreshSignInAsync(version);
        return Back(returnUrl, null, "Your photo is updated.");
    }

    [HttpPost("remove"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(string? returnUrl)
    {
        await _photos.SetAsync(User.UserId(), null, null);
        await RefreshSignInAsync(0);
        return Back(returnUrl, null, "Your photo is removed.");
    }

    /// <summary>Puts the new photo version in the sign-in cookie, so every page shows the new photo straight away.</summary>
    private async Task RefreshSignInAsync(long version)
    {
        var auth = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (auth.Principal?.Identity is not ClaimsIdentity old) return;
        var claims = old.Claims.Where(c => c.Type != AppClaims.PhotoVersion).ToList();
        if (version > 0) claims.Add(new Claim(AppClaims.PhotoVersion, version.ToString()));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, old.AuthenticationType)), auth.Properties ?? new AuthenticationProperties());
    }

    private IActionResult Back(string? returnUrl, string? error, string? done = null)
    {
        if (error is not null) TempData["PhotoError"] = error;
        if (done is not null) TempData["PhotoDone"] = done;
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/");
    }
}
