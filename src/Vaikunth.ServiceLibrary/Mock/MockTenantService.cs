using Vaikunth.DtoLibrary;

namespace Vaikunth.ServiceLibrary.Mock;

/// <summary>Clients and plans in memory, with five sample clients, so the super admin screen works without SQL Server.</summary>
public sealed class MockTenantService : ITenantService
{
    public Task<List<PlanDto>> GetPlansAsync() => Task.FromResult(MockData.Plans.ToList());

    public Task<List<TenantDto>> ListAsync() => Task.FromResult(MockData.Tenants.Values.OrderBy(t => t.Name).Select(ToDto).ToList());

    private static TenantDto ToDto(MockData.Tenant t)
    {
        var files = MockDocumentService.CountsFor(t.TenantId);
        var users = t.ExtraUsers + MockData.People.Values.Count(p => p.TenantId == t.TenantId && p.IsActive);
        var admin = t.AdminEmail is null ? null : MockData.People.GetValueOrDefault(t.AdminEmail);
        return new TenantDto(t.TenantId, t.Name, t.Slug, t.PlanId, MockData.Plans.First(p => p.PlanId == t.PlanId).Name, t.IsActive, t.CreatedUtc,
            MockData.MaxUsers(t), users, MockData.QuotaBytes(t), t.ExtraStorageBytes + files.Bytes, t.Apps,
            t.ExtraFiles[0] + files.Pages, t.ExtraFiles[1] + files.Workbooks, t.ExtraFiles[2] + files.Decks, t.AdminEmail,
            admin?.UserId, admin is not null && admin.Password is null, admin?.DisplayName, t.StorageQuotaMbOverride,
            t.ExtraFiles.ElementAtOrDefault(3) + files.Pdfs, t.ExtraFiles.ElementAtOrDefault(4) + files.Projects,
            MockData.Plans.First(p => p.PlanId == t.PlanId).PricePerUser, t.BillingFrom, t.BillingHoldUntil, t.BillingBlocked);
    }

    public async Task<SuiteStatsDto> StatsAsync()
    {
        var all = await ListAsync();
        return new SuiteStatsDto(all.Count, all.Count(t => t.IsActive), all.Sum(t => t.ActiveUsers),
            all.Sum(t => t.Documents), all.Sum(t => t.Workbooks), all.Sum(t => t.Decks), all.Sum(t => t.StorageUsedBytes),
            all.Sum(t => t.Pdfs), all.Sum(t => t.Projects));
    }

    public Task<TenantCreateResult> CreateAsync(CreateTenantRequest r)
    {
        if (TenantRules.Validate(r) is string problem) return Task.FromResult(new TenantCreateResult(false, problem));
        var slug = r.Slug.Trim().ToLowerInvariant();
        if (MockData.Tenants.Values.Any(t => t.Slug == slug)) return Task.FromResult(new TenantCreateResult(false, "That short name is already used by another client."));
        var email = r.AdminEmail.Trim().ToLowerInvariant();
        if (MockData.People.ContainsKey(email)) return Task.FromResult(new TenantCreateResult(false, "That admin email is already used."));
        var id = Guid.NewGuid();
        MockData.Tenants[id] = new MockData.Tenant { TenantId = id, Name = r.Name.Trim(), Slug = slug, PlanId = r.PlanId, Apps = r.Apps, AdminEmail = email, MaxUsersOverride = r.Licenses };
        var admin = new MockData.Person { TenantId = id, DisplayName = r.AdminName.Trim(), Email = email, Role = AppRoles.TenantAdmin, Password = r.SendInvite ? null : r.AdminPassword };
        MockData.People[email] = admin;
        return Task.FromResult(new TenantCreateResult(true, null, id, admin.UserId));
    }

    public Task<OperationResult> UpdateAsync(UpdateTenantRequest r)
    {
        if (!MockData.Tenants.TryGetValue(r.TenantId, out var t)) return Task.FromResult(OperationResult.Fail("Client not found."));
        if (string.IsNullOrWhiteSpace(r.Name)) return Task.FromResult(OperationResult.Fail("Enter the company name."));
        if (r.Apps == SuiteAppFlags.None) return Task.FromResult(OperationResult.Fail("Pick at least one app."));
        if (r.MaxUsersOverride is int n && (n < 1 || n > 100000)) return Task.FromResult(OperationResult.Fail("Licenses must be between 1 and 100,000."));
        t.Name = r.Name.Trim(); t.PlanId = r.PlanId; t.Apps = r.Apps; t.IsActive = r.IsActive;
        t.MaxUsersOverride = r.MaxUsersOverride; t.StorageQuotaMbOverride = r.StorageQuotaMbOverride;
        if (r.BillingFrom is DateOnly b) t.BillingFrom = Money.FirstOfMonth(b);
        return Task.FromResult(OperationResult.Ok(t.TenantId));
    }

    public Task SetActiveAsync(Guid tenantId, bool isActive)
    {
        if (MockData.Tenants.TryGetValue(tenantId, out var t)) t.IsActive = isActive;
        return Task.CompletedTask;
    }

    public Task SetPlanAsync(Guid tenantId, int planId)
    {
        if (MockData.Tenants.TryGetValue(tenantId, out var t) && MockData.Plans.Any(p => p.PlanId == planId)) { t.PlanId = planId; t.StorageQuotaMbOverride = null; }
        return Task.CompletedTask;
    }

    public Task<OperationResult> UpdatePlanAsync(UpdatePlanRequest r)
    {
        if (TenantRules.Validate(r) is string problem) return Task.FromResult(OperationResult.Fail(problem));
        var plan = MockData.Plans.FirstOrDefault(p => p.PlanId == r.PlanId);
        if (plan is null) return Task.FromResult(OperationResult.Fail("Plan not found."));
        plan.MaxUsers = r.MaxUsers; plan.StorageQuotaMb = r.StorageQuotaMb; plan.Apps = r.Apps; plan.PricePerUser = r.PricePerUser;
        return Task.FromResult(OperationResult.Ok());
    }

    public Task<OperationResult> DeleteAsync(Guid tenantId, string confirmSlug)
    {
        if (!MockData.Tenants.TryGetValue(tenantId, out var t)) return Task.FromResult(OperationResult.Fail("Client not found."));
        if (t.IsActive) return Task.FromResult(OperationResult.Fail("Pause the client before deleting it."));
        if (!string.Equals(t.Slug, (confirmSlug ?? "").Trim(), StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(OperationResult.Fail("The short name you typed doesn't match."));
        foreach (var p in MockData.People.Where(p => p.Value.TenantId == tenantId).ToList()) MockData.People.TryRemove(p.Key, out _);
        lock (MockData.Payments) MockData.Payments.RemoveAll(p => p.TenantId == tenantId);
        MockDocumentService.ForgetTenant(tenantId);
        MockData.Tenants.TryRemove(tenantId, out _);
        return Task.FromResult(OperationResult.Ok(tenantId));
    }
}
