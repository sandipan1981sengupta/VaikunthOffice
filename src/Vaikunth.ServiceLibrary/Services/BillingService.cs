using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vaikunth.DtoLibrary;
using Vaikunth.ServiceLibrary.Data;

namespace Vaikunth.ServiceLibrary;

/// <summary>
/// Money for the super admin: the billing rules, payments received, and the hold / block flags of a client.
/// The working-out (who is paid, late or blocked) is <see cref="BillingCalculator"/>; <see cref="BillingDesk"/> puts it together.
/// </summary>
public interface IBillingService
{
    Task<BillingSettingsDto> GetSettingsAsync();
    Task<OperationResult> SaveSettingsAsync(BillingSettingsDto settings);
    Task<List<PaymentDto>> ListPaymentsAsync(Guid? tenantId = null);
    /// <summary>Records money received. <paramref name="billAmount"/> is what that month costs today (licenses × price).</summary>
    Task<OperationResult> RecordAsync(RecordPaymentRequest request, decimal billAmount);
    Task DeletePaymentAsync(Guid paymentId);
    /// <summary>Let a client in again until a day (unblocks it now). Null takes the hold off.</summary>
    Task SetHoldAsync(Guid tenantId, DateOnly? until);
    Task SetBlockedAsync(Guid tenantId, bool blocked);
    /// <summary>One client's own payment rules (or back to the default rules) and its first billed month.</summary>
    Task<OperationResult> SaveClientRulesAsync(ClientRulesRequest request);
    /// <summary>Clients whose people can't sign in right now: paused, or blocked for an unpaid bill.</summary>
    Task<List<Guid>> LockedTenantsAsync();
}

public static class BillingRules
{
    public static readonly string[] Methods = ["Bank transfer", "UPI", "Cheque", "Cash", "Card"];

    public const string BlockedMessage =
        "Your company's Vaikunth Desk account is on hold because a payment is overdue. Ask your admin to contact us.";

    public static string? Validate(BillingSettingsDto s)
    {
        if (s.DueDay is < 1 or > 28) return "Pick a due day between the 1st and the 28th.";
        if (s.GraceDays is < 0 or > 365) return "Days before the block must be between 0 and 365.";
        return null;
    }

    public static string? Validate(ClientRulesRequest r)
    {
        if (r.TenantId == Guid.Empty) return "Pick the client.";
        if (r.UseDefault) return null;
        return Validate(new BillingSettingsDto { DueDay = r.DueDay, GraceDays = r.GraceDays, AutoBlock = r.AutoBlock });
    }

    public static string? Validate(RecordPaymentRequest r)
    {
        if (r.TenantId == Guid.Empty) return "Pick the client who paid.";
        if (r.Amount <= 0 || r.Amount > 100_000_000) return "Enter the amount received.";
        if (r.Period == default) return "Pick the month this payment is for.";
        if (r.PaidOn == default) return "Pick the day the money came in.";
        if (r.PaidOn > DateOnly.FromDateTime(DateTime.Now).AddDays(1)) return "The payment day can't be in the future.";
        if (!Methods.Contains(r.Method)) return "Pick how they paid.";
        return null;
    }
}

/// <summary>Works out a client's bill for a day. Same rules in SQL and sample-data mode.</summary>
public static class BillingCalculator
{
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    public static ClientBillDto For(TenantDto t, BillingSettingsDto defaults, IEnumerable<PaymentDto> payments, DateOnly today)
    {
        var s = defaults.For(t);   // the client's own rules where it has them
        var thisMonth = Money.FirstOfMonth(today);
        var mine = payments.Where(p => p.TenantId == t.TenantId).ToList();
        var paidThisMonth = mine.Where(p => p.Period == thisMonth).Sum(p => p.Amount);
        DateOnly? lastPaid = mine.Count == 0 ? null : mine.Max(p => p.PaidOn);
        var monthly = t.MonthlyBill;

        ClientBillDto Bill(BillState state, IReadOnlyList<DateOnly> unpaid, decimal outstanding, DateOnly? due, DateOnly? blocks) =>
            new(t.TenantId, monthly, t.MaxUsers, t.PricePerUser, state, unpaid, outstanding, due, blocks, t.BillingHoldUntil, paidThisMonth, lastPaid);

        if (t.IsFree) return Bill(BillState.Free, [], 0, null, null);

        var from = Money.FirstOfMonth(t.BillingFrom ?? thisMonth.AddMonths(1));
        if (from > thisMonth) return Bill(BillState.NotStarted, [], 0, Money.DueDate(from, s.DueDay), null);

        var unpaid = new List<DateOnly>();
        decimal outstanding = 0;
        for (var m = from; m <= thisMonth; m = m.AddMonths(1))
        {
            var rows = mine.Where(p => p.Period == m).ToList();
            var paid = rows.Sum(p => p.Amount);
            var bill = rows.Count > 0 ? rows.Max(p => p.BillAmount) : monthly;
            if (paid + 0.005m < bill) { unpaid.Add(m); outstanding += bill - paid; }
        }
        if (unpaid.Count == 0) return Bill(BillState.Paid, unpaid, 0, Money.DueDate(thisMonth.AddMonths(1), s.DueDay), null);

        var dueOn = Money.DueDate(unpaid[0], s.DueDay);
        var blocksOn = dueOn.AddDays(s.GraceDays);
        if (today <= dueOn) return Bill(BillState.Due, unpaid, outstanding, dueOn, s.AutoBlock ? blocksOn : null);
        if (t.BillingHoldUntil is DateOnly hold && hold >= today) return Bill(BillState.OnHold, unpaid, outstanding, dueOn, hold.AddDays(1));
        if (s.AutoBlock && today >= blocksOn) return Bill(BillState.Blocked, unpaid, outstanding, dueOn, blocksOn);
        return Bill(BillState.Overdue, unpaid, outstanding, dueOn, s.AutoBlock ? blocksOn : null);
    }

    /// <summary>"Due 10 Dec 2026", "17 days late · blocks on 9 Dec 2026", ...</summary>
    public static string Describe(ClientBillDto b, DateOnly today) => b.State switch
    {
        BillState.Free => "Free plan · never billed",
        BillState.NotStarted => $"Billing starts {Money.Month(Money.FirstOfMonth(b.DueOn ?? today))}",
        BillState.Paid => b.DueOn is DateOnly n ? $"All paid · next due {Money.Day(n)}" : "All paid",
        BillState.Due => $"Due {Money.Day(b.DueOn!.Value)}",
        BillState.OnHold => $"Unpaid · let in until {Money.Day(b.HoldUntil!.Value)}",
        BillState.Blocked => $"Blocked since {Money.Day(b.BlocksOn!.Value)} · {b.DaysLate(today)} days late",
        _ => b.BlocksOn is DateOnly x ? $"{b.DaysLate(today)} days late · blocks on {Money.Day(x)}" : $"{b.DaysLate(today)} days late"
    };
}

/// <summary>Everything the Payments tab needs, worked out once.</summary>
public sealed record BillingOverview(BillingSettingsDto Settings, List<TenantDto> Clients, List<PaymentDto> Payments,
    Dictionary<Guid, ClientBillDto> Bills, DateOnly Today)
{
    public IEnumerable<ClientBillDto> Billed => Bills.Values.Where(b => b.State is not (BillState.Free or BillState.NotStarted));
    /// <summary>This month: what billed clients should pay (from active, billed clients).</summary>
    public decimal ExpectedThisMonth => Billed.Sum(b => b.Monthly);
    public decimal ReceivedThisMonth => Payments.Where(p => p.Period == Money.FirstOfMonth(Today)).Sum(p => p.Amount);
    public decimal Outstanding => Bills.Values.Sum(b => b.Outstanding);
    public int Late => Bills.Values.Count(b => b.State is BillState.Overdue or BillState.Blocked or BillState.OnHold);
    public int Blocked => Bills.Values.Count(b => b.State == BillState.Blocked);
}

/// <summary>Clients whose people may not use Vaikunth Desk right now (paused or blocked). Signed-in people of these are signed out.</summary>
public sealed class TenantGate
{
    private volatile HashSet<Guid> _locked = [];
    public bool IsLocked(Guid tenantId) => tenantId != Guid.Empty && _locked.Contains(tenantId);
    public void Set(IEnumerable<Guid> locked) => _locked = [.. locked];
}

/// <summary>
/// Works out every client's bill, blocks clients that are too late (only when auto-block is on, never on a free plan),
/// unblocks the ones that paid or were let in again, and refreshes <see cref="TenantGate"/>.
/// Runs when the site starts, every hour, and each time the super admin opens the Clients page.
/// </summary>
public sealed class BillingDesk
{
    private readonly ITenantService _tenants;
    private readonly IBillingService _billing;
    private readonly TenantGate _gate;

    public BillingDesk(ITenantService tenants, IBillingService billing, TenantGate gate)
    {
        _tenants = tenants;
        _billing = billing;
        _gate = gate;
    }

    public async Task<BillingOverview> RefreshAsync()
    {
        var today = BillingCalculator.Today;
        var settings = await _billing.GetSettingsAsync();
        var clients = await _tenants.ListAsync();
        var payments = await _billing.ListPaymentsAsync();
        var bills = clients.ToDictionary(c => c.TenantId, c => BillingCalculator.For(c, settings, payments, today));

        var changed = false;
        foreach (var c in clients)
        {
            var block = bills[c.TenantId].State == BillState.Blocked;
            if (block == c.BillingBlocked) continue;
            await _billing.SetBlockedAsync(c.TenantId, block);
            changed = true;
        }
        if (changed) clients = await _tenants.ListAsync();
        _gate.Set(await _billing.LockedTenantsAsync());
        return new BillingOverview(settings, clients, payments, bills, today);
    }

    /// <summary>A client's own bill (for the banner its admin sees on the launcher).</summary>
    public async Task<ClientBillDto?> ForClientAsync(Guid tenantId)
    {
        var client = (await _tenants.ListAsync()).FirstOrDefault(c => c.TenantId == tenantId);
        if (client is null) return null;
        return BillingCalculator.For(client, await _billing.GetSettingsAsync(), await _billing.ListPaymentsAsync(tenantId), BillingCalculator.Today);
    }
}

/// <summary>Checks bills when the site starts and then every hour, so late clients are blocked on time.</summary>
public sealed class BillingSweepService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<BillingSweepService> _log;

    public BillingSweepService(IServiceScopeFactory scopes, ILogger<BillingSweepService> log)
    {
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var o = await scope.ServiceProvider.GetRequiredService<BillingDesk>().RefreshAsync();
                if (o.Blocked > 0) _log.LogInformation("Billing: {Blocked} client(s) blocked for an unpaid bill.", o.Blocked);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogWarning(ex, "Billing check failed. It runs again in an hour.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

/// <summary>Billing in SQL Server (Vaikunth_Billing_*, Vaikunth_Payment_*, Vaikunth_Tenant_SetHold / SetBlocked).</summary>
public sealed class BillingService : IBillingService
{
    private readonly SqlDb _db;
    public BillingService(SqlDb db) => _db = db;

    public async Task<BillingSettingsDto> GetSettingsAsync() =>
        (await _db.QueryAsync("dbo.Vaikunth_Billing_GetSettings", null, r => new BillingSettingsDto
        {
            DueDay = r.I("DueDay"), GraceDays = r.I("GraceDays"), AutoBlock = r.B("AutoBlock")
        })).FirstOrDefault() ?? new BillingSettingsDto();

    public async Task<OperationResult> SaveSettingsAsync(BillingSettingsDto s)
    {
        if (BillingRules.Validate(s) is string problem) return OperationResult.Fail(problem);
        await _db.ExecAsync("dbo.Vaikunth_Billing_SaveSettings", p => p
            .With("@DueDay", (byte)s.DueDay).With("@GraceDays", s.GraceDays).With("@AutoBlock", s.AutoBlock));
        return OperationResult.Ok();
    }

    public Task<List<PaymentDto>> ListPaymentsAsync(Guid? tenantId = null) =>
        _db.QueryAsync("dbo.Vaikunth_Payment_List", p => p.With("@TenantId", tenantId), r => new PaymentDto(
            r.G("PaymentId"), r.G("TenantId"), r.S("TenantName"), r.D("Period"), r.M("Amount"), r.M("BillAmount"),
            r.D("PaidOn"), r.S("Method"), r.SN("Reference"), r.SN("Note"), r.SN("RecordedByName"), r.Utc("RecordedUtc")));

    public async Task<OperationResult> RecordAsync(RecordPaymentRequest request, decimal billAmount)
    {
        if (BillingRules.Validate(request) is string problem) return OperationResult.Fail(problem);
        var id = Guid.NewGuid();
        var rows = await _db.QueryAsync("dbo.Vaikunth_Payment_Add", p => p
            .With("@PaymentId", id).With("@TenantId", request.TenantId).AddDate("@Period", Money.FirstOfMonth(request.Period))
            .With("@Amount", request.Amount).With("@BillAmount", billAmount).AddDate("@PaidOn", request.PaidOn)
            .AddText("@Method", request.Method, 30).AddText("@Reference", Clean(request.Reference, 100), 100)
            .AddText("@Note", Clean(request.Note, 500), 500).With("@RecordedBy", request.RecordedBy), r => r.I("Result"));
        return rows.FirstOrDefault() == 1 ? OperationResult.Ok(id) : OperationResult.Fail("Client not found.");
    }

    public Task DeletePaymentAsync(Guid paymentId) =>
        _db.ExecAsync("dbo.Vaikunth_Payment_Delete", p => p.With("@PaymentId", paymentId));

    public Task SetHoldAsync(Guid tenantId, DateOnly? until) =>
        _db.ExecAsync("dbo.Vaikunth_Tenant_SetHold", p => p.With("@TenantId", tenantId).AddDate("@HoldUntil", until));

    public async Task<OperationResult> SaveClientRulesAsync(ClientRulesRequest r)
    {
        if (BillingRules.Validate(r) is string problem) return OperationResult.Fail(problem);
        var rows = (await _db.QueryAsync("dbo.Vaikunth_Tenant_SetBillingRules", p => p
            .With("@TenantId", r.TenantId)
            .With("@DueDay", r.UseDefault ? null : (byte?)r.DueDay)
            .With("@GraceDays", r.UseDefault ? null : (short?)r.GraceDays)
            .With("@AutoBlock", r.UseDefault ? null : (bool?)r.AutoBlock)
            .AddDate("@BillingFrom", r.BillingFrom is DateOnly f ? Money.FirstOfMonth(f) : null), x => x.I("Result"))).FirstOrDefault();
        return rows > 0 ? OperationResult.Ok(r.TenantId) : OperationResult.Fail("Client not found.");
    }

    public Task SetBlockedAsync(Guid tenantId, bool blocked) =>
        _db.ExecAsync("dbo.Vaikunth_Tenant_SetBlocked", p => p.With("@TenantId", tenantId).With("@Blocked", blocked));

    public Task<List<Guid>> LockedTenantsAsync() =>
        _db.QueryAsync("dbo.Vaikunth_Tenant_ListLocked", null, r => r.G("TenantId"));

    internal static string? Clean(string? text, int max)
    {
        text = text?.Trim();
        return string.IsNullOrEmpty(text) ? null : text.Length > max ? text[..max] : text;
    }
}
