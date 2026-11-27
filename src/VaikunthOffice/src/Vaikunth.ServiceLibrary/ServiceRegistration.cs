using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vaikunth.ServiceLibrary.Data;
using Vaikunth.ServiceLibrary.Mock;

namespace Vaikunth.ServiceLibrary;

public static class ServiceRegistration
{
    /// <summary>True when there is no connection string, so the in-memory mock services are used.</summary>
    public static bool UsesMockData(IConfiguration config) => string.IsNullOrWhiteSpace(config.GetConnectionString("Default"));

    /// <summary>
    /// Registers every Vaikunth Office service. Call once from each app's Program.cs:
    /// builder.Services.AddVaikunthServices(builder.Configuration).
    /// With ConnectionStrings:Default set, services call the stored procedures in /database;
    /// without it, they use in-memory sample data (handy for the mock screens).
    /// </summary>
    public static IServiceCollection AddVaikunthServices(this IServiceCollection services, IConfiguration config)
    {
        // email works the same in mock and SQL mode: SMTP when Smtp:Host is set, otherwise the local outbox
        services.Configure<SmtpOptions>(config.GetSection("Smtp"));
        services.AddSingleton<IAppEmailSender, SmtpEmailSender>();
        services.AddSingleton<IAccountEmailService, AccountEmailService>();

        // Word / Excel / PowerPoint content lives in files on the server (appsettings "Storage"), in both modes
        services.Configure<StorageOptions>(config.GetSection("Storage"));
        services.AddSingleton<IDocumentFileStore, DocumentFileStore>();

        if (UsesMockData(config))
        {
            services.AddSingleton<IPasswordLinkService, MockPasswordLinkService>();
            services.AddSingleton<IUserService, MockUserService>();
            services.AddSingleton<IDocumentService, MockDocumentService>();
            services.AddSingleton<ITenantService, MockTenantService>();
            services.AddSingleton<IUserPhotoService, MockUserPhotoService>();
            return services;
        }

        services.AddSingleton<SqlDb>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<IPasswordLinkService, PasswordLinkService>();
        services.AddScoped<IUserPhotoService, UserPhotoService>();
        return services;
    }
}
