using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vaikunth.DtoLibrary;
using Vaikunth.UI.Infrastructure;

namespace Vaikunth.UI.Controllers;

/// <summary>
/// Base for the apps that live inside the workspace and keep no files (Vaijayanti Days, Sudarshana Inbox):
/// signed-in people only, super admins go to their own page, and a client only gets the apps on its plan.
/// </summary>
[Authorize]
public abstract class SuiteAppControllerBase : Controller
{
    protected readonly CurrentApp Current;

    protected SuiteAppControllerBase(CurrentApp current) => Current = current;

    public override void OnActionExecuting(Microsoft.AspNetCore.Mvc.Filters.ActionExecutingContext context)
    {
        if (User.IsSuperAdmin()) context.Result = Redirect("/admin");
        else if (!User.Apps().Has(Current.Info.App))
            context.Result = IsAjax ? StatusCode(403, new { ok = false, error = $"{Current.Info.Name} isn't part of your company's plan." }) : Redirect($"/?blocked={Current.Info.Key}");
        base.OnActionExecuting(context);
    }

    protected bool IsAjax => Request.Headers.XRequestedWith == "XMLHttpRequest" || Request.Headers.Accept.ToString().Contains("application/json");

    protected IActionResult Fail(string error) => Json(new { ok = false, error });

    /// <summary>The database hasn't had database/Vaikunth_Update.sql yet (the new tables or procedures are missing).</summary>
    protected static bool NeedsUpdate(Microsoft.Data.SqlClient.SqlException ex) => ex.Number is 208 or 2812;

    protected const string UpdateMessage = "This app needs the latest database: run database/Vaikunth_Update.sql once on your database, then try again.";
}
