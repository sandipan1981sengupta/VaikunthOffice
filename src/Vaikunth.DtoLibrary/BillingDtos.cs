using System.Globalization;

namespace Vaikunth.DtoLibrary;

/// <summary>The super admin's billing rules (Vaikunth_BillingSettings).</summary>
public sealed class BillingSettingsDto
{
    /// <summary>Payment is due on this day of every month (1 to 28).</summary>
    public int DueDay { get; set; } = 10;
    /// <summary>Days after the due day that an unpaid client keeps working, with a warning, before it is blocked.</summary>
    public int GraceDays { get; set; } = 30;
    /// <summary>Block a client automatically when the grace days are over.</summary>
    public bool AutoBlock { get; set; } = true;
}

/// <summary>One payment a client made (Vaikunth_Payments).</summary>
public sealed record PaymentDto(
    Guid PaymentId, Guid TenantId, string TenantName, DateOnly Period, decimal Amount, decimal BillAmount,
    DateOnly PaidOn, string Method, string? Reference, string? Note, string? RecordedByName, DateTime RecordedUtc);

/// <summary>The super admin records money received (Record payment window).</summary>
public sealed class RecordPaymentRequest
{
    public Guid TenantId { get; set; }
    /// <summary>The month it pays for (any day of it).</summary>
    public DateOnly Period { get; set; }
    public decimal Amount { get; set; }
    public DateOnly PaidOn { get; set; }
    public string Method { get; set; } = "Bank transfer";
    public string? Reference { get; set; }
    public string? Note { get; set; }
    public Guid? RecordedBy { get; set; }
}

/// <summary>Where a client stands with its bill today.</summary>
public enum BillState
{
    /// <summary>Plan price is 0: never billed, never blocked.</summary>
    Free,
    /// <summary>Billing starts in a later month.</summary>
    NotStarted,
    /// <summary>Every month up to this one is paid.</summary>
    Paid,
    /// <summary>This month is not paid yet, but the due day hasn't come.</summary>
    Due,
    /// <summary>A month is unpaid after its due day: the client is warned, and blocked when the grace days are over.</summary>
    Overdue,
    /// <summary>Unpaid and the super admin let it in again until a day.</summary>
    OnHold,
    /// <summary>Unpaid for longer than the grace days: nobody of this client can sign in.</summary>
    Blocked
}

/// <summary>A client's bill worked out for today (BillingCalculator).</summary>
public sealed record ClientBillDto(
    Guid TenantId, decimal Monthly, int Licenses, decimal PricePerUser, BillState State,
    /// <summary>Unpaid months up to this one, oldest first.</summary>
    IReadOnlyList<DateOnly> UnpaidMonths,
    /// <summary>Money still to come for those months.</summary>
    decimal Outstanding,
    /// <summary>Due day of the oldest unpaid month (or of next month when all is paid).</summary>
    DateOnly? DueOn,
    /// <summary>The day the client is (or was) blocked if nothing is paid. Null when nothing is late.</summary>
    DateOnly? BlocksOn,
    DateOnly? HoldUntil,
    decimal PaidThisMonth,
    DateOnly? LastPaidOn)
{
    public int DaysLate(DateOnly today) => DueOn is DateOnly d && today > d ? today.DayNumber - d.DayNumber : 0;
    public DateOnly? OldestUnpaid => UnpaidMonths.Count > 0 ? UnpaidMonths[0] : null;

    public string StateText => State switch
    {
        BillState.Free => "Free",
        BillState.NotStarted => "Not started",
        BillState.Paid => "Paid",
        BillState.Due => "Due",
        BillState.Overdue => "Overdue",
        BillState.OnHold => "On hold",
        _ => "Blocked"
    };
}

public static class Money
{
    private static readonly CultureInfo India = CultureInfo.GetCultureInfo("en-IN");
    /// <summary>1495 → "₹1,495", 1495.5 → "₹1,495.50" (Indian grouping: ₹1,49,500).</summary>
    public static string Rupees(decimal amount) =>
        "₹" + (amount == decimal.Truncate(amount) ? amount.ToString("#,##0", India) : amount.ToString("#,##0.00", India));
    /// <summary>December 2026 → "Dec 2026".</summary>
    public static string Month(DateOnly month) => month.ToString("MMM yyyy", CultureInfo.InvariantCulture);
    public static string Day(DateOnly day) => day.ToString("d MMM yyyy", CultureInfo.InvariantCulture);
    public static DateOnly FirstOfMonth(DateOnly d) => new(d.Year, d.Month, 1);
    /// <summary>The due day in a month (28 at most, so it exists in February too).</summary>
    public static DateOnly DueDate(DateOnly month, int dueDay) => new(month.Year, month.Month, Math.Clamp(dueDay, 1, 28));
    /// <summary>"10th", "1st", "22nd".</summary>
    public static string Ordinal(int n) => n + (n % 100 is 11 or 12 or 13 ? "th" : (n % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" });
}
