using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vaikunth.DtoLibrary;
using Vaikunth.ServiceLibrary;

namespace Vaikunth.UI.Infrastructure;

public static class WebRegistration
{
    /// <summary>The sign-in cookie of Vaikunth Office.</summary>
    public const string CookieName = ".Vaikunth.Auth";

    /// <summary>
    /// Everything Vaikunth Office needs: MVC, the shared UI, the three apps (as areas /pages, /workbook, /slides),
    /// sign-in and services. Call from Program.cs: builder.AddVaikunthOffice().
    /// </summary>
    public static WebApplicationBuilder AddVaikunthOffice(this WebApplicationBuilder builder)
    {
        var services = builder.Services;
        var suite = builder.Configuration.GetSection("Suite").Get<SuiteOptions>() ?? new SuiteOptions();
        services.Configure<SuiteOptions>(builder.Configuration.GetSection("Suite"));
        services.AddHttpContextAccessor();
        services.AddScoped(sp => CurrentApp.ForPath(sp.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>().HttpContext?.Request.Path.Value));

        services.AddVaikunthServices(builder.Configuration);
        services.AddControllersWithViews();

        // Word / Excel / PowerPoint files: empty Storage:RootPath = App_Data\VaikunthFiles in the folder the app is hosted in
        var contentRoot = builder.Environment.ContentRootPath;
        services.PostConfigure<StorageOptions>(o =>
        {
            if (string.IsNullOrWhiteSpace(o.RootPath)) o.RootPath = Path.Combine(contentRoot, "App_Data", "VaikunthFiles");
            else if (!Path.IsPathRooted(o.RootPath)) o.RootPath = Path.Combine(contentRoot, o.RootPath);   // e.g. "App_Data/files"
        });
        if (!ServiceRegistration.UsesMockData(builder.Configuration))
            foreach (var app in SuiteApps.All.Select(a => a.App))
                services.AddSingleton<Microsoft.Extensions.Hosting.IHostedService>(sp => new DocumentMoveService(sp.GetRequiredService<Vaikunth.ServiceLibrary.Data.SqlDb>(),
                    sp.GetRequiredService<IDocumentFileStore>(), sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<DocumentMoveService>>(), app));

        // Sign-in keys: set Suite:DataProtectionKeysPath on a server (or a web farm) so sign-ins survive restarts.
        var keys = string.IsNullOrWhiteSpace(suite.DataProtectionKeysPath)
            ? Path.Combine(Path.GetTempPath(), "vaikunth-office-keys")
            : suite.DataProtectionKeysPath;
        services.AddDataProtection().SetApplicationName("VaikunthOffice").PersistKeysToFileSystem(new DirectoryInfo(keys));

        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(o =>
            {
                o.Cookie.Name = CookieName;
                o.LoginPath = "/account/login";
                o.LogoutPath = "/account/logout";
                o.AccessDeniedPath = "/";   // e.g. a member opening /team or /admin goes back to the launcher
                o.SlidingExpiration = true;
                o.ExpireTimeSpan = TimeSpan.FromDays(7);
                // a client that is paused, or blocked for an unpaid bill, can't keep working in a tab that was already signed in
                o.Events.OnValidatePrincipal = async ctx =>
                {
                    var gate = ctx.HttpContext.RequestServices.GetRequiredService<TenantGate>();
                    if (ctx.Principal is { } user && gate.IsLocked(user.TenantId()))
                    {
                        ctx.RejectPrincipal();
                        await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    }
                };
            });
        services.AddAuthorization();
        return builder;
    }

    /// <summary>The middleware and routes of Vaikunth Office.</summary>
    public static WebApplication UseVaikunthOffice(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/home/error");
            app.UseHsts();
        }
        // a save sends the whole file, pictures included: let /{app}/editor/save take up to Storage:MaxFileMb
        // (twice that, as text in the request, plus room). Other requests keep the normal 30 MB limit.
        var maxMb = app.Configuration.GetValue("Storage:MaxFileMb", 200);
        var maxSave = (long)Math.Max(1, maxMb) * 1024 * 1024 * 2 + 1024 * 1024;
        app.Use((ctx, next) =>
        {
            if (HttpMethods.IsPost(ctx.Request.Method)
                && ((ctx.Request.Path.Value ?? "").EndsWith("/editor/save", StringComparison.OrdinalIgnoreCase)
                    || (ctx.Request.Path.Value ?? "").EndsWith("/editor/export", StringComparison.OrdinalIgnoreCase))
                && ctx.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
                limit.MaxRequestBodySize = maxSave;
            return next(ctx);
        });
        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapStaticAssets();
        // /pages/editor, /workbook/editor, /slides/editor: each app's editor lives in its own area
        app.MapControllerRoute("apps", "{area:exists}/{controller=Editor}/{action=Index}/{id?}").WithStaticAssets();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}").WithStaticAssets();
        return app;
    }
}
