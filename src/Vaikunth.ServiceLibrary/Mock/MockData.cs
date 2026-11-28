using System.Collections.Concurrent;
using Vaikunth.DtoLibrary;

namespace Vaikunth.ServiceLibrary.Mock;

/// <summary>The in-memory "database" behind the mock services: plans, clients and their people. Lost on restart.</summary>
internal static class MockData
{
    public static readonly Guid SuperAdminId = Guid.Parse("00000000-0000-0000-0000-00000000a001");
    public static readonly Guid AcmeId = Guid.Parse("acacacac-0000-0000-0000-000000000001");

    public static readonly List<PlanDto> Plans =
    [
        new() { PlanId = 1, Name = "Starter", MaxUsers = 10, StorageQuotaMb = 1024, Apps = SuiteAppFlags.All, PricePerUser = 0 },
        new() { PlanId = 2, Name = "Business", MaxUsers = 50, StorageQuotaMb = 10240, Apps = SuiteAppFlags.All, PricePerUser = 149 },
        new() { PlanId = 3, Name = "Enterprise", MaxUsers = 500, StorageQuotaMb = 102400, Apps = SuiteAppFlags.All, PricePerUser = 299 },
    ];

    public sealed class Tenant
    {
        public Guid TenantId { get; init; }
        public string Name { get; set; } = "";
        public string Slug { get; init; } = "";
        public int PlanId { get; set; }
        public SuiteAppFlags Apps { get; set; } = SuiteAppFlags.All;
        public bool IsActive { get; set; } = true;
        public int? MaxUsersOverride { get; set; }
        public long? StorageQuotaMbOverride { get; set; }
        public long ExtraStorageBytes { get; set; }
        public int ExtraUsers { get; set; }
        public int[] ExtraFiles { get; set; } = [0, 0, 0];
        public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
        public string? AdminEmail { get; set; }
        /// <summary>First month the client pays for. New clients: next month.</summary>
        public DateOnly? BillingFrom { get; set; } = Money.FirstOfMonth(DateOnly.FromDateTime(DateTime.Now)).AddMonths(1);
        public DateOnly? BillingHoldUntil { get; set; }
        public bool BillingBlocked { get; set; }
        public int? DueDay { get; set; }
        public int? GraceDays { get; set; }
        public bool? AutoBlock { get; set; }
    }

    public static readonly BillingSettingsDto Billing = new();
    public static readonly List<PaymentDto> Payments = [];

    public sealed class Person
    {
        public Guid UserId { get; init; } = Guid.NewGuid();
        public Guid? TenantId { get; init; }
        public string DisplayName { get; set; } = "";
        public string Email { get; set; } = "";
        public string Role { get; set; } = AppRoles.Member;
        /// <summary>Plain text is fine here: this is only the in-memory mock. Null = invited, no password yet.</summary>
        public string? Password { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
        public DateTime? LastLoginUtc { get; set; }
        public string? InvitedByName { get; init; }
        public string? Department { get; set; }
        public string? JobTitle { get; set; }
        public DateOnly? JoiningDate { get; set; }
    }

    /// <summary>Password links: token hash (hex) → link.</summary>
    public sealed record Link(Guid UserId, PasswordLinkPurpose Purpose, DateTime CreatedUtc, DateTime ExpiresUtc)
    {
        public DateTime? UsedUtc { get; set; }
    }
    public static readonly ConcurrentDictionary<string, Link> Links = new();

    public static readonly ConcurrentDictionary<Guid, Tenant> Tenants = new();
    public static readonly ConcurrentDictionary<string, Person> People = new(StringComparer.OrdinalIgnoreCase);

    static MockData()
    {
        void T(Guid id, string name, string slug, int plan, SuiteAppFlags apps, bool active, int users, long mb, int[] files, int daysAgo, string admin)
        {
            Tenants[id] = new Tenant
            {
                TenantId = id, Name = name, Slug = slug, PlanId = plan, Apps = apps, IsActive = active, ExtraUsers = users,
                ExtraStorageBytes = mb * 1048576, ExtraFiles = files, CreatedUtc = DateTime.UtcNow.AddDays(-daysAgo), AdminEmail = admin
            };
        }
        T(AcmeId, "Acme Corp", "acme", 2, SuiteAppFlags.All, true, 0, 2310, [148, 62, 37], 120, "priya.sharma@acme.com");
        void P(string name, string email, string role, string? password, bool active, int days, int? lastLoginHours, string? by,
               string dept, string job)
        {
            People[email] = new Person
            {
                TenantId = AcmeId, DisplayName = name, Email = email, Role = role, Password = password, IsActive = active,
                CreatedUtc = DateTime.UtcNow.AddDays(-days), LastLoginUtc = lastLoginHours is int h ? DateTime.UtcNow.AddHours(-h) : null, InvitedByName = by,
                Department = dept, JobTitle = job, JoiningDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-days - 3))
            };
        }
        P("Priya Sharma", "priya.sharma@acme.com", AppRoles.TenantAdmin, "vaikunth123", true, 120, 1, null, "Administration", "Office manager");
        P("Arjun Nair", "arjun.nair@acme.com", AppRoles.Member, "vaikunth123", true, 90, 5, "Priya Sharma", "Sales", "Sales executive");
        P("Fatima Khan", "fatima.khan@acme.com", AppRoles.TenantAdmin, "vaikunth123", true, 80, 30, "Priya Sharma", "Finance", "Accounts lead");
        P("Rohan Das", "rohan.das@acme.com", AppRoles.Member, "vaikunth123", true, 45, 52, "Priya Sharma", "Engineering", "Developer");
        P("Meera Iyer", "meera.iyer@acme.com", AppRoles.Member, null, true, 2, null, "Priya Sharma", "Human resources", "HR associate");
        P("Kabir Singh", "kabir.singh@acme.com", AppRoles.Member, "vaikunth123", false, 200, 900, "Priya Sharma", "Sales", "Field sales");
        T(Guid.NewGuid(), "Shree Textiles", "shree-textiles", 1, SuiteAppFlags.Pages | SuiteAppFlags.Workbook, true, 7, 380, [41, 88, 0], 64, "owner@shreetextiles.in");
        T(Guid.NewGuid(), "Nirmaan Infra", "nirmaan", 3, SuiteAppFlags.All, true, 212, 38400, [1290, 840, 315, 460, 38], 210, "it@nirmaaninfra.com");
        T(Guid.NewGuid(), "Gurukul Public School", "gurukul", 1, SuiteAppFlags.Pages | SuiteAppFlags.Slides, true, 9, 910, [76, 4, 52], 30, "principal@gurukul.edu.in");
        T(Guid.NewGuid(), "Sagar Logistics", "sagar", 2, SuiteAppFlags.All, false, 18, 1620, [90, 133, 12, 25, 6], 400, "ops@sagarlogistics.com");
        SeedBilling();
    }

    /// <summary>Sample money: Acme has paid every month, Nirmaan hasn't paid this month (late), Sagar stopped paying (blocked).</summary>
    private static void SeedBilling()
    {
        var thisMonth = Money.FirstOfMonth(DateOnly.FromDateTime(DateTime.Now));
        Tenant BySlug(string slug) => Tenants.Values.First(t => t.Slug == slug);
        void Pay(Tenant t, int monthsAgo, string method, string? reference, int paidDay)
        {
            var month = thisMonth.AddMonths(-monthsAgo);
            var bill = MaxUsers(t) * Plans.First(p => p.PlanId == t.PlanId).PricePerUser;
            var paidOn = new DateOnly(month.Year, month.Month, Math.Min(paidDay, 28));
            if (paidOn > DateOnly.FromDateTime(DateTime.Now)) paidOn = DateOnly.FromDateTime(DateTime.Now);
            Payments.Add(new PaymentDto(Guid.NewGuid(), t.TenantId, t.Name, month, bill, bill, paidOn, method, reference, null, "Vaikunth Desk admin",
                paidOn.ToDateTime(new TimeOnly(11, 0)).ToUniversalTime()));
        }
        foreach (var t in Tenants.Values) t.BillingFrom = thisMonth.AddMonths(-5);
        var acme = BySlug("acme");
        for (var m = 5; m >= 0; m--) Pay(acme, m, m % 2 == 0 ? "Bank transfer" : "UPI", $"UTR{40213 + m * 7}", 6 + m);
        var nirmaan = BySlug("nirmaan");
        for (var m = 5; m >= 1; m--) Pay(nirmaan, m, "Cheque", $"CHQ-{118300 + m}", 9);
        var sagar = BySlug("sagar");
        for (var m = 5; m >= 3; m--) Pay(sagar, m, "Bank transfer", $"UTR{77120 + m}", 12);
    }

    public static int MaxUsers(Tenant t) => t.MaxUsersOverride ?? Plans.First(p => p.PlanId == t.PlanId).MaxUsers;
    public static long QuotaBytes(Tenant t) => (t.StorageQuotaMbOverride ?? Plans.First(p => p.PlanId == t.PlanId).StorageQuotaMb) * 1048576;
}
