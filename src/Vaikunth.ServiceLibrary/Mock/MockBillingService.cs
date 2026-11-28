using Vaikunth.DtoLibrary;

namespace Vaikunth.ServiceLibrary.Mock;

/// <summary>Billing in memory (sample-data mode): the rules, payments and hold / block flags of MockData.</summary>
public sealed class MockBillingService : IBillingService
{
    public Task<BillingSettingsDto> GetSettingsAsync() => Task.FromResult(new BillingSettingsDto
    {
        DueDay = MockData.Billing.DueDay, GraceDays = MockData.Billing.GraceDays, AutoBlock = MockData.Billing.AutoBlock
    });

    public Task<OperationResult> SaveSettingsAsync(BillingSettingsDto s)
    {
        if (BillingRules.Validate(s) is string problem) return Task.FromResult(OperationResult.Fail(problem));
        MockData.Billing.DueDay = s.DueDay; MockData.Billing.GraceDays = s.GraceDays; MockData.Billing.AutoBlock = s.AutoBlock;
        return Task.FromResult(OperationResult.Ok());
    }

    public Task<List<PaymentDto>> ListPaymentsAsync(Guid? tenantId = null)
    {
        lock (MockData.Payments)
            return Task.FromResult(MockData.Payments
                .Where(p => tenantId is null || p.TenantId == tenantId)
                .Select(p => p with { TenantName = MockData.Tenants.TryGetValue(p.TenantId, out var t) ? t.Name : p.TenantName })
                .OrderByDescending(p => p.PaidOn).ThenByDescending(p => p.RecordedUtc).ToList());
    }

    public Task<OperationResult> RecordAsync(RecordPaymentRequest r, decimal billAmount)
    {
        if (BillingRules.Validate(r) is string problem) return Task.FromResult(OperationResult.Fail(problem));
        if (!MockData.Tenants.TryGetValue(r.TenantId, out var t)) return Task.FromResult(OperationResult.Fail("Client not found."));
        var id = Guid.NewGuid();
        lock (MockData.Payments)
            MockData.Payments.Add(new PaymentDto(id, t.TenantId, t.Name, Money.FirstOfMonth(r.Period), r.Amount, billAmount, r.PaidOn, r.Method,
                BillingService.Clean(r.Reference, 100), BillingService.Clean(r.Note, 500), "Vaikunth Desk admin", DateTime.UtcNow));
        return Task.FromResult(OperationResult.Ok(id));
    }

    public Task DeletePaymentAsync(Guid paymentId)
    {
        lock (MockData.Payments) MockData.Payments.RemoveAll(p => p.PaymentId == paymentId);
        return Task.CompletedTask;
    }

    public Task SetHoldAsync(Guid tenantId, DateOnly? until)
    {
        if (MockData.Tenants.TryGetValue(tenantId, out var t))
        {
            t.BillingHoldUntil = until;
            if (until is not null) t.BillingBlocked = false;
        }
        return Task.CompletedTask;
    }

    public Task<OperationResult> SaveClientRulesAsync(ClientRulesRequest r)
    {
        if (BillingRules.Validate(r) is string problem) return Task.FromResult(OperationResult.Fail(problem));
        if (!MockData.Tenants.TryGetValue(r.TenantId, out var t)) return Task.FromResult(OperationResult.Fail("Client not found."));
        t.DueDay = r.UseDefault ? null : r.DueDay;
        t.GraceDays = r.UseDefault ? null : r.GraceDays;
        t.AutoBlock = r.UseDefault ? null : r.AutoBlock;
        if (r.BillingFrom is DateOnly f) t.BillingFrom = Money.FirstOfMonth(f);
        return Task.FromResult(OperationResult.Ok(t.TenantId));
    }

    public Task SetBlockedAsync(Guid tenantId, bool blocked)
    {
        if (MockData.Tenants.TryGetValue(tenantId, out var t)) t.BillingBlocked = blocked;
        return Task.CompletedTask;
    }

    public Task<List<Guid>> LockedTenantsAsync() =>
        Task.FromResult(MockData.Tenants.Values.Where(t => !t.IsActive || t.BillingBlocked).Select(t => t.TenantId).ToList());
}
