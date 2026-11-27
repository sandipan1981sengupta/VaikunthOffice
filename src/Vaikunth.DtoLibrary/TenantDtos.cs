namespace Vaikunth.DtoLibrary;

/// <summary>One row of Vaikunth_Plans: the limits a client gets.</summary>
public sealed class PlanDto
{
    public int PlanId { get; set; }
    public string Name { get; set; } = "";
    public int MaxUsers { get; set; }
    public long StorageQuotaMb { get; set; }
    public SuiteAppFlags Apps { get; set; } = SuiteAppFlags.All;
    public decimal PricePerUser { get; set; }
}

/// <summary>A client company in the super admin list (Vaikunth_Tenant_List). Limits already include per-client overrides.</summary>
public sealed record TenantDto(
    Guid TenantId, string Name, string Slug, int PlanId, string PlanName, bool IsActive, DateTime CreatedUtc,
    int MaxUsers, int ActiveUsers, long StorageQuotaBytes, long StorageUsedBytes, SuiteAppFlags Apps,
    int Documents, int Workbooks, int Decks, string? AdminEmail, Guid? AdminUserId = null, bool AdminPending = false, string? AdminName = null,
    /// <summary>The client's own storage in MB, or null when it uses its plan's.</summary>
    long? StorageOverrideMb = null, int Pdfs = 0, int Projects = 0,
    /// <summary>The plan's price for one license a month. 0 = free plan, never billed.</summary>
    decimal PricePerUser = 0,
    /// <summary>First month the client pays for (first day of it). Null = not billed yet.</summary>
    DateOnly? BillingFrom = null,
    /// <summary>The super admin let the client in again until this day, even with unpaid months.</summary>
    DateOnly? BillingHoldUntil = null,
    /// <summary>Sign-in is stopped because a payment is too late.</summary>
    bool BillingBlocked = false)
{
    /// <summary>What the client pays a month: licenses × price per user.</summary>
    public decimal MonthlyBill => MaxUsers * PricePerUser;
    public bool IsFree => PricePerUser <= 0;
    public int Files => Documents + Workbooks + Decks + Pdfs + Projects;
    public int StoragePercent => StorageQuotaBytes <= 0 ? 0 : (int)Math.Min(100, StorageUsedBytes * 100 / StorageQuotaBytes);
    public int UserPercent => MaxUsers <= 0 ? 0 : Math.Min(100, ActiveUsers * 100 / MaxUsers);
}

/// <summary>The numbers at the top of the super admin page.</summary>
public sealed record SuiteStatsDto(int Clients, int ActiveClients, int Users, int Documents, int Workbooks, int Decks, long StorageUsedBytes, int Pdfs = 0, int Projects = 0);

/// <summary>Create a client with its first admin (Vaikunth_Tenant_Create).</summary>
public sealed class CreateTenantRequest
{
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public int PlanId { get; set; }
    public SuiteAppFlags Apps { get; set; } = SuiteAppFlags.All;
    /// <summary>How many people (licenses) this client may have, the admin included. Null = the plan's number.</summary>
    public int? Licenses { get; set; }
    public string AdminName { get; set; } = "";
    public string AdminEmail { get; set; } = "";
    /// <summary>True (default): the admin gets a welcome email with a link to set their own password.
    /// False: the super admin types a temporary password here and passes it on.</summary>
    public bool SendInvite { get; set; } = true;
    public string AdminPassword { get; set; } = "";
}

/// <summary>What creating a client returns: its id and its first admin's id (for the welcome email).</summary>
public sealed record TenantCreateResult(bool Succeeded, string? Error = null, Guid TenantId = default, Guid AdminUserId = default);

/// <summary>The super admin changes a plan on the Plans and limits tab (Vaikunth_Plan_Update). Every client on it gets the new limits.</summary>
public sealed class UpdatePlanRequest
{
    public int PlanId { get; set; }
    /// <summary>Licenses (users) a client on this plan gets, unless the client has its own number.</summary>
    public int MaxUsers { get; set; }
    public long StorageQuotaMb { get; set; }
    public SuiteAppFlags Apps { get; set; } = SuiteAppFlags.All;
    public decimal PricePerUser { get; set; }
}

/// <summary>Change a client's plan, limits, apps or status (Vaikunth_Tenant_Update).</summary>
public sealed class UpdateTenantRequest
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "";
    public int PlanId { get; set; }
    public SuiteAppFlags Apps { get; set; } = SuiteAppFlags.All;
    /// <summary>Licenses: how many people may use Vaikunth Office. Null = use the plan's number.</summary>
    public int? MaxUsersOverride { get; set; }
    public long? StorageQuotaMbOverride { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>First month the client pays for. Null = leave it as it is.</summary>
    public DateOnly? BillingFrom { get; set; }
}

public static class Sizes
{
    /// <summary>1536 → "1.5 KB", 3 GB → "3 GB".</summary>
    public static string Bytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double v = bytes;
        var i = 0;
        while (v >= 1024 && i < units.Length - 1) { v /= 1024; i++; }
        return i == 0 ? $"{bytes} B" : $"{v:0.#} {units[i]}";
    }
}
