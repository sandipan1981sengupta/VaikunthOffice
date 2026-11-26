using Microsoft.Data.SqlClient;
using Vaikunth.DtoLibrary;
using Vaikunth.ServiceLibrary.Data;

namespace Vaikunth.ServiceLibrary;

/// <summary>Clients (tenants) and plans. Only the super admin uses this (the Clients page).</summary>
public interface ITenantService
{
    Task<List<PlanDto>> GetPlansAsync();
    Task<List<TenantDto>> ListAsync();
    Task<SuiteStatsDto> StatsAsync();
    Task<TenantCreateResult> CreateAsync(CreateTenantRequest request);
    Task<OperationResult> UpdateAsync(UpdateTenantRequest request);
    Task SetActiveAsync(Guid tenantId, bool isActive);
}

public static class TenantRules
{
    /// <summary>Checks a new client before it is saved. Returns the problem, or null.</summary>
    public static string? Validate(CreateTenantRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) return "Enter the company name.";
        if (string.IsNullOrWhiteSpace(r.Slug) || !System.Text.RegularExpressions.Regex.IsMatch(r.Slug.Trim(), "^[a-z0-9][a-z0-9-]{1,38}$"))
            return "The short name can only have small letters, numbers and dashes (2 to 39 characters).";
        if (r.Apps == SuiteAppFlags.None) return "Pick at least one app.";
        if (string.IsNullOrWhiteSpace(r.AdminName) || string.IsNullOrWhiteSpace(r.AdminEmail) || !r.AdminEmail.Contains('@'))
            return "Enter the client admin's name and email.";
        return r.SendInvite ? null : Passwords.Validate(r.AdminPassword);
    }
}

/// <summary>Clients and plans in SQL Server (Vaikunth_Tenant_*, Vaikunth_Plan_List, Vaikunth_Suite_Stats).</summary>
public sealed class TenantService : ITenantService
{
    private readonly SqlDb _db;
    public TenantService(SqlDb db) => _db = db;

    public Task<List<PlanDto>> GetPlansAsync() =>
        _db.QueryAsync("dbo.Vaikunth_Plan_List", null, r => new PlanDto
        {
            PlanId = r.I("PlanId"), Name = r.S("Name"), MaxUsers = r.I("MaxUsers"), StorageQuotaMb = r.L("StorageQuotaMb"),
            Apps = (SuiteAppFlags)Convert.ToByte(r["AppsEnabled"]), PricePerUser = Convert.ToDecimal(r["PricePerUser"])
        });

    public Task<List<TenantDto>> ListAsync() =>
        _db.QueryAsync("dbo.Vaikunth_Tenant_List", null, r => new TenantDto(
            r.G("TenantId"), r.S("Name"), r.S("Slug"), r.I("PlanId"), r.S("PlanName"), r.B("IsActive"), r.Utc("CreatedUtc"),
            r.I("MaxUsers"), r.I("ActiveUsers"), r.L("StorageQuotaBytes"), r.L("StorageUsedBytes"), (SuiteAppFlags)Convert.ToByte(r["AppsEnabled"]),
            r.I("Documents"), r.I("Workbooks"), r.I("Decks"), r.SN("AdminEmail"), r.GN("AdminUserId"), r.B("AdminPending")));

    public async Task<SuiteStatsDto> StatsAsync() =>
        (await _db.QueryAsync("dbo.Vaikunth_Suite_Stats", null, r => new SuiteStatsDto(
            r.I("Clients"), r.I("ActiveClients"), r.I("Users"), r.I("Documents"), r.I("Workbooks"), r.I("Decks"), r.L("StorageUsedBytes")))).First();

    public async Task<TenantCreateResult> CreateAsync(CreateTenantRequest request)
    {
        if (TenantRules.Validate(request) is string problem) return new TenantCreateResult(false, problem);
        var id = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var rows = await _db.QueryAsync("dbo.Vaikunth_Tenant_Create", p => p
            .With("@TenantId", id).AddText("@Name", request.Name.Trim(), 120).AddText("@Slug", request.Slug.Trim(), 40)
            .With("@PlanId", request.PlanId).With("@AppsEnabled", (byte)request.Apps)
            .With("@AdminUserId", adminId).AddText("@AdminName", request.AdminName.Trim(), 120)
            .AddText("@AdminEmail", request.AdminEmail.Trim(), 200)
            .AddText("@AdminPasswordHash", request.SendInvite ? null : Passwords.Hash(request.AdminPassword), 400)
            .With("@AdminTone", Passwords.ToneFor(request.AdminEmail)), r => r.I("Result"));
        return rows.FirstOrDefault() switch
        {
            1 => new TenantCreateResult(true, null, id, adminId),
            0 => new TenantCreateResult(false, "That short name is already used by another client."),
            _ => new TenantCreateResult(false, "That admin email is already used.")
        };
    }

    public async Task<OperationResult> UpdateAsync(UpdateTenantRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return OperationResult.Fail("Enter the company name.");
        if (request.Apps == SuiteAppFlags.None) return OperationResult.Fail("Pick at least one app.");
        await _db.ExecAsync("dbo.Vaikunth_Tenant_Update", p => p
            .With("@TenantId", request.TenantId).AddText("@Name", request.Name.Trim(), 120).With("@PlanId", request.PlanId)
            .With("@AppsEnabled", (byte)request.Apps).With("@MaxUsersOverride", request.MaxUsersOverride)
            .With("@StorageQuotaMbOverride", request.StorageQuotaMbOverride).With("@IsActive", request.IsActive));
        return OperationResult.Ok(request.TenantId);
    }

    public Task SetActiveAsync(Guid tenantId, bool isActive) =>
        _db.ExecAsync("dbo.Vaikunth_Tenant_SetActive", p => p.With("@TenantId", tenantId).With("@IsActive", isActive));
}
