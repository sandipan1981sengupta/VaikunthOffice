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
    /// Registers every Vaikunth Desk service. Call once from each app's Program.cs:
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

        // billing: who is paid, late or blocked (BillingDesk), checked every hour by BillingSweepService
        services.AddSingleton<TenantGate>();
        services.AddScoped<BillingDesk>();
        services.AddHostedService<BillingSweepService>();

        if (UsesMockData(config))
        {
            services.AddSingleton<IBillingService, MockBillingService>();
            services.AddSingleton<IPasswordLinkService, MockPasswordLinkService>();
            services.AddSingleton<IUserService, MockUserService>();
            services.AddSingleton<IDocumentService, MockDocumentService>();
            services.AddSingleton<ITenantService, MockTenantService>();
            services.AddSingleton<IUserPhotoService, MockUserPhotoService>();
            services.AddSingleton<ICalendarService, MockCalendarService>();
            services.AddSingleton<IMailService, MockMailService>();
            return services;
        }

        services.AddSingleton<SqlDb>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<IPasswordLinkService, PasswordLinkService>();
        services.AddScoped<IUserPhotoService, UserPhotoService>();
        services.AddScoped<IBillingService, BillingService>();
        services.AddScoped<ICalendarService, CalendarService>();
        services.AddScoped<IMailService, MailService>();
        return services;
    }
}
