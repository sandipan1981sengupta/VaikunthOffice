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
    /// <summary>Moves a client to another plan. Its licenses stay; storage goes back to the new plan's.</summary>
    Task SetPlanAsync(Guid tenantId, int planId);
    Task<OperationResult> UpdatePlanAsync(UpdatePlanRequest request);
    /// <summary>
    /// Deletes a paused client for good: its people, their sign-in links, the file list and (by the caller) the files on disk.
    /// <paramref name="confirmSlug"/> must be the client's short name.
    /// </summary>
    Task<OperationResult> DeleteAsync(Guid tenantId, string confirmSlug);
}

public static class TenantRules
{
    /// <summary>Checks a new client before it is saved. Returns the problem, or null.</summary>
    public static string? Validate(UpdatePlanRequest r)
    {
        if (r.MaxUsers < 1 || r.MaxUsers > 100000) return "Licenses must be between 1 and 100,000.";
        if (r.StorageQuotaMb < 1 || r.StorageQuotaMb > 100L * 1024 * 1024) return "Storage must be between 1 MB and 100 TB.";
        if (r.Apps == SuiteAppFlags.None) return "Pick at least one app.";
        if (r.PricePerUser < 0 || r.PricePerUser > 1_000_000) return "Check the price per user.";
        return null;
    }

    /// <summary>Checks the admin's name and email on Edit client (both or neither).</summary>
    public static string? ValidateAdmin(UpdateTenantRequest r)
    {
        if (r.AdminEmail is null && r.AdminName is null) return null;
        if (string.IsNullOrWhiteSpace(r.AdminName)) return "Enter the admin's name.";
        var e = r.AdminEmail?.Trim() ?? "";
        if (!System.Text.RegularExpressions.Regex.IsMatch(e, @"^[^@\s]+@[^@\s]+\.[^@\s]+$")) return "Check the admin's email address.";
        return null;
    }

    public static string? Validate(CreateTenantRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name)) return "Enter the company name.";
        if (string.IsNullOrWhiteSpace(r.Slug) || !System.Text.RegularExpressions.Regex.IsMatch(r.Slug.Trim(), "^[a-z0-9][a-z0-9-]{1,38}$"))
            return "The short name can only have small letters, numbers and dashes (2 to 39 characters).";
        if (r.Apps == SuiteAppFlags.None) return "Pick at least one app.";
        if (r.Licenses is int n && (n < 1 || n > 100000)) return "Licenses must be between 1 and 100,000 (the admin uses one).";
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
            r.I("Documents"), r.I("Workbooks"), r.I("Decks"), r.SN("AdminEmail"), r.GN("AdminUserId"), r.B("AdminPending"), r.SN("AdminName"),
            r["StorageQuotaMbOverride"] is DBNull ? null : Convert.ToInt64(r["StorageQuotaMbOverride"]), r.I("Pdfs"), r.I("Projects"),
            r.M("PricePerUser"), r.DN("BillingFrom"), r.DN("BillingHoldUntil"), r.B("BillingBlocked"),
            // the rule columns arrive with Vaikunth_Update.sql; before it runs, every client follows the defaults
            r.Has("DueDay") && r["DueDay"] is not DBNull ? Convert.ToInt32(r["DueDay"]) : null,
            r.Has("GraceDays") && r["GraceDays"] is not DBNull ? Convert.ToInt32(r["GraceDays"]) : null,
            r.Has("AutoBlock") && r["AutoBlock"] is not DBNull ? Convert.ToBoolean(r["AutoBlock"]) : null));

    public async Task<SuiteStatsDto> StatsAsync() =>
        (await _db.QueryAsync("dbo.Vaikunth_Suite_Stats", null, r => new SuiteStatsDto(
            r.I("Clients"), r.I("ActiveClients"), r.I("Users"), r.I("Documents"), r.I("Workbooks"), r.I("Decks"), r.L("StorageUsedBytes"), r.I("Pdfs"), r.I("Projects")))).First();

    public async Task<TenantCreateResult> CreateAsync(CreateTenantRequest request)
    {
        if (TenantRules.Validate(request) is string problem) return new TenantCreateResult(false, problem);
        var id = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var rows = await _db.QueryAsync("dbo.Vaikunth_Tenant_Create", p => p
            .With("@TenantId", id).AddText("@Name", request.Name.Trim(), 120).AddText("@Slug", request.Slug.Trim(), 40)
            .With("@PlanId", request.PlanId).With("@AppsEnabled", (byte)request.Apps).With("@MaxUsersOverride", request.Licenses)
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
        if (request.MaxUsersOverride is int n && (n < 1 || n > 100000)) return OperationResult.Fail("Licenses must be between 1 and 100,000.");
        if (TenantRules.ValidateAdmin(request) is string adminProblem) return OperationResult.Fail(adminProblem);
        if (request.AdminEmail is not null)
        {
            var res = (await _db.QueryAsync("dbo.Vaikunth_Tenant_UpdateAdmin", p => p
                .With("@TenantId", request.TenantId).AddText("@Name", request.AdminName!.Trim(), 120)
                .AddText("@Email", request.AdminEmail.Trim().ToLowerInvariant(), 200), r => r.I("Result"))).FirstOrDefault();
            if (res == 0) return OperationResult.Fail($"{request.AdminEmail.Trim()} is already used by someone else.");
        }
        await _db.ExecAsync("dbo.Vaikunth_Tenant_Update", p => p
            .With("@TenantId", request.TenantId).AddText("@Name", request.Name.Trim(), 120).With("@PlanId", request.PlanId)
            .With("@AppsEnabled", (byte)request.Apps).With("@MaxUsersOverride", request.MaxUsersOverride)
            .With("@StorageQuotaMbOverride", request.StorageQuotaMbOverride).With("@IsActive", request.IsActive)
            .AddDate("@BillingFrom", request.BillingFrom is DateOnly b ? Money.FirstOfMonth(b) : null));
        return OperationResult.Ok(request.TenantId);
    }

    public Task SetActiveAsync(Guid tenantId, bool isActive) =>
        _db.ExecAsync("dbo.Vaikunth_Tenant_SetActive", p => p.With("@TenantId", tenantId).With("@IsActive", isActive));
    public Task SetPlanAsync(Guid tenantId, int planId) =>
        _db.ExecAsync("dbo.Vaikunth_Tenant_SetPlan", p => p.With("@TenantId", tenantId).With("@PlanId", planId));

    public async Task<OperationResult> UpdatePlanAsync(UpdatePlanRequest request)
    {
        if (TenantRules.Validate(request) is string problem) return OperationResult.Fail(problem);
        await _db.ExecAsync("dbo.Vaikunth_Plan_Update", p => p
            .With("@PlanId", request.PlanId).With("@MaxUsers", request.MaxUsers).With("@StorageQuotaMb", request.StorageQuotaMb)
            .With("@AppsEnabled", (byte)request.Apps).With("@PricePerUser", request.PricePerUser));
        return OperationResult.Ok();
    }

    public async Task<OperationResult> DeleteAsync(Guid tenantId, string confirmSlug)
    {
        var rows = await _db.QueryAsync("dbo.Vaikunth_Tenant_Delete", p => p
            .With("@TenantId", tenantId).AddText("@ConfirmSlug", (confirmSlug ?? "").Trim().ToLowerInvariant(), 40), r => r.I("Result"));
        return rows.FirstOrDefault() switch
        {
            1 => OperationResult.Ok(tenantId),
            2 => OperationResult.Fail("Pause the client before deleting it."),
            3 => OperationResult.Fail("The short name you typed doesn't match."),
            _ => OperationResult.Fail("Client not found.")
        };
    }
}
