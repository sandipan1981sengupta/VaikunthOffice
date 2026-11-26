using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vaikunth.DtoLibrary;
using Vaikunth.ServiceLibrary;

namespace Vaikunth.UI.Infrastructure;

public static class WebRegistration
{
    /// <summary>The cookie is shared by the three apps, so signing in once works in all of them.</summary>
    public const string CookieName = ".Vaikunth.Auth";

    /// <summary>
    /// Everything an app of the suite needs: MVC, the shared UI, sign-in, services.
    /// Call from Program.cs: builder.AddVaikunthApp(SuiteApp.Pages).
    /// </summary>
    public static WebApplicationBuilder AddVaikunthApp(this WebApplicationBuilder builder, SuiteApp app)
    {
        var services = builder.Services;
        var suite = builder.Configuration.GetSection("Suite").Get<SuiteOptions>() ?? new SuiteOptions();
        services.Configure<SuiteOptions>(builder.Configuration.GetSection("Suite"));
        services.AddSingleton(new CurrentApp(SuiteApps.For(app)));

        services.AddVaikunthServices(builder.Configuration);
        services.AddControllersWithViews();

        // Word / Excel / PowerPoint files: empty Storage:RootPath = App_Data\VaikunthFiles in the folder the app is hosted in
        var contentRoot = builder.Environment.ContentRootPath;
        services.PostConfigure<StorageOptions>(o =>
        {
            if (string.IsNullOrWhiteSpace(o.RootPath)) o.RootPath = Path.Combine(contentRoot, "App_Data", "VaikunthFiles");
        });
        if (!ServiceRegistration.UsesMockData(builder.Configuration))
            services.AddHostedService(sp => new DocumentMoveService(sp.GetRequiredService<Vaikunth.ServiceLibrary.Data.SqlDb>(),
                sp.GetRequiredService<IDocumentFileStore>(), sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<DocumentMoveService>>(), app));

        // One key ring and one application name for all three apps, so each can read the others' sign-in cookie.
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
            });
        services.AddAuthorization();
        return builder;
    }

    /// <summary>The middleware and routes shared by every app of the suite.</summary>
    public static WebApplication UseVaikunthApp(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/home/error");
            app.UseHsts();
        }
        // a save sends the whole file, pictures included: let /editor/save take up to Storage:MaxFileMb
        // (twice that, as text in the request, plus room). Other requests keep the normal 30 MB limit.
        var maxMb = app.Configuration.GetValue("Storage:MaxFileMb", 200);
        var maxSave = (long)Math.Max(1, maxMb) * 1024 * 1024 * 2 + 1024 * 1024;
        app.Use((ctx, next) =>
        {
            if (ctx.Request.Path.StartsWithSegments("/editor/save")
                && ctx.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } limit)
                limit.MaxRequestBodySize = maxSave;
            return next(ctx);
        });
        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapStaticAssets();
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}").WithStaticAssets();
        return app;
    }
}
