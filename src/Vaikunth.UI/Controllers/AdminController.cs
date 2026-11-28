using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Vaikunth.UI.Infrastructure;
using Vaikunth.DtoLibrary;
using Vaikunth.ServiceLibrary;

namespace Vaikunth.UI.Controllers;

public sealed class AdminViewModel
{
    public SuiteStatsDto Stats { get; init; } = new(0, 0, 0, 0, 0, 0, 0);
    public List<TenantDto> Clients { get; init; } = new();
    public List<PlanDto> Plans { get; init; } = new();
    public string Search { get; init; } = "";
    public string Show { get; init; } = "all";
    public bool MockMode { get; init; }
    /// <summary>"clients", "payments" or "plans": the tab that is open.</summary>
    public string Tab { get; init; } = "clients";
    /// <summary>The client to select again after an action (its details pane stays open).</summary>
    public Guid? Selected { get; init; }
    public bool EmailConfigured { get; init; }
    /// <summary>Money: every client's bill, the payments and the billing rules.</summary>
    public required BillingOverview Billing { get; init; }
}

/// <summary>
/// Super admin: the Clients page. Create a client company with its first admin, change its plan,
/// user and storage limits and which apps it gets, or switch it off. Available in every app at /admin.
/// </summary>
[Authorize(Roles = AppRoles.SuperAdmin)]
[Route("admin")]
public sealed class AdminController : Controller
{
    private readonly ITenantService _tenants;
    private readonly IConfiguration _config;
    private readonly IPasswordLinkService _links;
    private readonly IAccountEmailService _emails;
    private readonly SuiteOptions _suite;
    private readonly IDocumentFileStore _files;
    private readonly IAppEmailSender _sender;
    private readonly IBillingService _billing;
    private readonly BillingDesk _desk;

    public AdminController(ITenantService tenants, IConfiguration config, IPasswordLinkService links, IAccountEmailService emails,
        IOptions<SuiteOptions> suite, IDocumentFileStore files, IAppEmailSender sender, IBillingService billing, BillingDesk desk)
    {
        _billing = billing;
        _desk = desk;
        _files = files;
        _sender = sender;
        _tenants = tenants;
        _config = config;
        _links = links;
        _emails = emails;
        _suite = suite.Value;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? q = null, string show = "all", string tab = "clients", Guid? sel = null)
    {
        // search, filters, sorting and paging happen in the page itself (the list of clients is small).
        // Opening the page also works out every bill and blocks / unblocks clients straight away.
        var billing = await _desk.RefreshAsync();
        // X-Partial: the page asks for its content only (after a save, Refresh or a tab change), without the frame around it
        ViewData["Partial"] = Request.Headers["X-Partial"] == "1";
        return View(new AdminViewModel
        {
            Stats = await _tenants.StatsAsync(), Clients = billing.Clients, Plans = await _tenants.GetPlansAsync(),
            Search = q ?? "", Show = show, MockMode = ServiceRegistration.UsesMockData(_config),
            Tab = tab is "plans" or "payments" ? tab : "clients", Selected = sel, EmailConfigured = _sender.IsConfigured,
            Billing = billing
        });
    }

    /// <summary>
    /// After an action: a normal form post goes back to the page (the message shows there); a post from the page's own
    /// script (fetch) gets the message as JSON plus the address to load the page content from, so nothing reloads.
    /// </summary>
    private IActionResult Back(Guid? sel = null, string? tab = null)
    {
        if (Request.Headers.XRequestedWith != "XMLHttpRequest") return RedirectToAction(nameof(Index), new { sel, tab });
        var ok = TempData["Ok"] as string;
        var error = TempData["Error"] as string;
        TempData.Remove("ReopenNew");
        return Json(new { ok = error is null, message = error ?? ok, url = Url.Action(nameof(Index), new { sel, tab }) });
    }

    [HttpPost("create"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateTenantRequest request, int[]? apps)
    {
        request.Apps = Flags(apps);
        var result = await _tenants.CreateAsync(request);
        if (!result.Succeeded) { TempData["Error"] = result.Error; TempData["ReopenNew"] = true; return Back(); }

        if (!request.SendInvite)
        {
            TempData["Ok"] = $"{request.Name} is set up. {request.AdminEmail} can sign in with the temporary password you chose.";
            return Back(result.TenantId);
        }
        var problem = await SendWelcomeAsync(result.AdminUserId, request.AdminEmail.Trim(), request.AdminName.Trim(), request.Name.Trim());
        TempData[problem is null ? "Ok" : "Error"] = problem is null
            ? $"{request.Name} is set up. A welcome email with a link to set the password is on its way to {request.AdminEmail}."
            : $"{request.Name} is set up, but {problem} Fix it, then use Email admin link.";
        return Back(result.TenantId);
    }

    /// <summary>
    /// Email admin link: a client admin who hasn't set a password yet gets a fresh welcome link,
    /// one who has gets a link to choose a new password.
    /// </summary>
    [HttpPost("resend"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendInvite(Guid tenantId)
    {
        var client = (await _tenants.ListAsync()).FirstOrDefault(t => t.TenantId == tenantId);
        if (client?.AdminUserId is not Guid adminId || client.AdminEmail is null)
        {
            TempData["Error"] = "This client has no admin to email.";
            return Back(tenantId);
        }
        if (!client.IsActive)
        {
            TempData["Error"] = $"{client.Name} is paused. Resume it first, so its admin can sign in.";
            return Back(tenantId);
        }
        var name = client.AdminName ?? client.AdminEmail.Split('@')[0];
        string? problem;
        if (client.AdminPending) problem = await SendWelcomeAsync(adminId, client.AdminEmail, name, client.Name);
        else problem = await _links.CreateAsync(adminId, PasswordLinkPurpose.Reset) is not string token
                ? $"a link was already sent to {client.AdminEmail} less than a minute ago. Wait a minute and try again."
                : await _emails.SendPasswordResetAsync(client.AdminEmail, name, _suite.SetPasswordUrl(Request, token)) ? null : EmailProblem(client.AdminEmail);
        TempData[problem is null ? "Ok" : "Error"] = problem is null
            ? (client.AdminPending ? $"A new welcome email is on its way to {client.AdminEmail}." : $"A link to choose a new password is on its way to {client.AdminEmail}.")
            : char.ToUpper(problem[0]) + problem[1..];
        return Back(tenantId);
    }

    /// <summary>Sends the welcome email with a set-password link. Returns null when it went out, else what went wrong.</summary>
    private async Task<string?> SendWelcomeAsync(Guid adminUserId, string email, string name, string company)
    {
        if (await _links.CreateAsync(adminUserId, PasswordLinkPurpose.Invite) is not string token)
            return $"a link was already sent to {email} less than a minute ago. Wait a minute and try again.";
        return await _emails.SendClientWelcomeAsync(email, name, company, _suite.SetPasswordUrl(Request, token)) ? null : EmailProblem(email);
    }

    private string EmailProblem(string email) =>
        $"the email to {email} couldn't be sent: {_sender.LastErrorFor(email) ?? "the mail server didn't accept it. Check the Smtp settings."}";

    [HttpPost("update"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(UpdateTenantRequest request, int[]? apps, string? billingMonth)
    {
        request.Apps = Flags(apps);
        request.BillingFrom = ParseMonth(billingMonth);
        var before = (await _tenants.ListAsync()).FirstOrDefault(t => t.TenantId == request.TenantId);
        if (before?.AdminUserId is null) { request.AdminEmail = null; request.AdminName = null; }
        var result = await _tenants.UpdateAsync(request);
        if (!result.Succeeded) { TempData["Error"] = result.Error; return Back(request.TenantId); }
        var newEmail = request.AdminEmail?.Trim().ToLowerInvariant();
        var emailChanged = newEmail is not null && before?.AdminEmail is string old && !string.Equals(old, newEmail, StringComparison.OrdinalIgnoreCase);
        if (emailChanged && before!.AdminPending && request.IsActive)
        {
            // the welcome went to the wrong address: send a fresh one to the corrected address (old links no longer work)
            var problem = await SendWelcomeAsync(before.AdminUserId!.Value, newEmail!, request.AdminName!.Trim(), request.Name.Trim());
            TempData[problem is null ? "Ok" : "Error"] = problem is null
                ? $"{request.Name} is updated. A new welcome email is on its way to {newEmail}; the old link no longer works."
                : $"{request.Name} is updated, but {problem}";
        }
        else TempData["Ok"] = emailChanged
            ? $"{request.Name} is updated. Its admin now signs in with {newEmail}."
            : $"{request.Name} is updated.";
        return Back(request.TenantId);
    }

    /// <summary>Change plan, from the details pane.</summary>
    [HttpPost("plan"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePlan(Guid tenantId, int planId)
    {
        var plan = (await _tenants.GetPlansAsync()).FirstOrDefault(p => p.PlanId == planId);
        var client = (await _tenants.ListAsync()).FirstOrDefault(t => t.TenantId == tenantId);
        if (plan is null || client is null) { TempData["Error"] = "Pick a plan."; return Back(tenantId); }
        await _tenants.SetPlanAsync(tenantId, planId);
        TempData["Ok"] = $"{client.Name} is now on the {plan.Name} plan. Its {client.MaxUsers} licenses stay; storage is now {Sizes.Bytes(plan.StorageQuotaMb * 1048576)}.";
        return Back(tenantId);
    }

    /// <summary>Plans and limits tab: save one plan.</summary>
    [HttpPost("plans/{planId:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePlan(int planId, UpdatePlanRequest request, int[]? apps)
    {
        request.PlanId = planId;
        request.Apps = Flags(apps);
        var result = await _tenants.UpdatePlanAsync(request);
        var name = (await _tenants.GetPlansAsync()).FirstOrDefault(p => p.PlanId == planId)?.Name ?? "The";
        TempData[result.Succeeded ? "Ok" : "Error"] = result.Succeeded ? $"{name} plan is saved. Its clients have the new limits now." : result.Error;
        return Back(tab: "plans");
    }

    /// <summary>Delete a paused client for good, with its people and all its files.</summary>
    [HttpPost("delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid tenantId, string? confirmSlug)
    {
        var client = (await _tenants.ListAsync()).FirstOrDefault(t => t.TenantId == tenantId);
        var result = await _tenants.DeleteAsync(tenantId, confirmSlug ?? "");
        if (!result.Succeeded) { TempData["Error"] = result.Error; return Back(tenantId); }
        var filesGone = _files.DeleteClientFolder(tenantId);
        TempData[filesGone ? "Ok" : "Error"] = filesGone
            ? $"{client?.Name ?? "The client"} is deleted, with its people and files."
            : $"{client?.Name ?? "The client"} is deleted, but some files couldn't be removed from the storage folder. See the log.";
        return Back();
    }

    [HttpPost("active"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(Guid tenantId, bool isActive, string? name)
    {
        await _tenants.SetActiveAsync(tenantId, isActive);
        TempData["Ok"] = isActive ? $"{name} is active again. Its people can sign in." : $"{name} is paused. Its people can't sign in until you resume it. Nothing is deleted.";
        return Back(tenantId);
    }

    // ======================= money =======================

    /// <summary>Record payment: money received from a client for one month.</summary>
    [HttpPost("payments"), ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordPayment(Guid tenantId, string? period, string? amount, string? paidOn, string? method,
        string? reference, string? note, string? tab)
    {
        var client = (await _tenants.ListAsync()).FirstOrDefault(t => t.TenantId == tenantId);
        if (client is null) { TempData["Error"] = "Pick the client who paid."; return Back(tab: tab); }
        var wasBlocked = client.BillingBlocked;
        var request = new RecordPaymentRequest
        {
            TenantId = tenantId, Period = ParseMonth(period) ?? default, Amount = ParseAmount(amount), PaidOn = ParseDay(paidOn) ?? default,
            Method = method ?? "", Reference = reference, Note = note, RecordedBy = User.UserId()
        };
        var result = await _billing.RecordAsync(request, client.MonthlyBill);
        if (!result.Succeeded) { TempData["Error"] = result.Error; return Back(tenantId, tab); }
        var overview = await _desk.RefreshAsync();
        var bill = overview.Bills.GetValueOrDefault(tenantId);
        var msg = $"{Money.Rupees(request.Amount)} from {client.Name} for {Money.Month(request.Period)} is recorded.";
        if (bill is not null && bill.Outstanding > 0) msg += $" {Money.Rupees(bill.Outstanding)} is still to come.";
        else msg += " Everything is paid.";
        if (wasBlocked && bill?.State != BillState.Blocked) msg += " Its people can sign in again.";
        TempData["Ok"] = msg;
        return Back(tenantId, tab);
    }

    [HttpPost("payments/delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePayment(Guid paymentId)
    {
        var payment = (await _billing.ListPaymentsAsync()).FirstOrDefault(p => p.PaymentId == paymentId);
        if (payment is null) { TempData["Error"] = "That payment is already gone."; return Back(tab: "payments"); }
        await _billing.DeletePaymentAsync(paymentId);
        await _desk.RefreshAsync();
        TempData["Ok"] = $"The payment of {Money.Rupees(payment.Amount)} from {payment.TenantName} for {Money.Month(payment.Period)} is deleted.";
        return Back(tab: "payments");
    }

    /// <summary>Billing rules: the due day of every month, days before the block, and whether to block automatically.</summary>
    [HttpPost("billing"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveBilling(int dueDay, int graceDays, bool autoBlock)
    {
        var result = await _billing.SaveSettingsAsync(new BillingSettingsDto { DueDay = dueDay, GraceDays = graceDays, AutoBlock = autoBlock });
        if (!result.Succeeded) { TempData["Error"] = result.Error; return Back(tab: "payments"); }
        var o = await _desk.RefreshAsync();
        TempData["Ok"] = $"Saved. Payment is due on the {Money.Ordinal(dueDay)} of every month" +
            (autoBlock ? $"; a client that hasn't paid {graceDays} days after that is blocked." : "; nobody is blocked automatically.") +
            (o.Blocked > 0 ? $" {o.Blocked} {(o.Blocked == 1 ? "client is" : "clients are")} blocked now." : "");
        return Back(tab: "payments");
    }

    /// <summary>One client's own payment rules: due day, days before the block, auto-block and the first month it pays for.</summary>
    [HttpPost("rules/client"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveClientRules(Guid tenantId, bool useDefault, int dueDay, int graceDays, bool autoBlock, string? billingMonth)
    {
        var client = (await _tenants.ListAsync()).FirstOrDefault(t => t.TenantId == tenantId);
        if (client is null) { TempData["Error"] = "Pick the client."; return Back(tab: "payments"); }
        OperationResult result;
        try
        {
            result = await _billing.SaveClientRulesAsync(new ClientRulesRequest
            {
                TenantId = tenantId, UseDefault = useDefault, DueDay = dueDay, GraceDays = graceDays, AutoBlock = autoBlock, BillingFrom = ParseMonth(billingMonth)
            });
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 2812)   // stored procedure not found
        {
            TempData["Error"] = "The database is older than this version. Run database/Vaikunth_Update.sql once, then save the rules again.";
            return Back(tenantId, "payments");
        }
        if (!result.Succeeded) { TempData["Error"] = result.Error; return Back(tenantId, "payments"); }
        var o = await _desk.RefreshAsync();
        var bill = o.Bills.GetValueOrDefault(tenantId);
        TempData["Ok"] = (useDefault
                ? $"{client.Name} follows the default rules again."
                : $"{client.Name}: payment due on the {Money.Ordinal(dueDay)} of every month" +
                  (autoBlock ? $", blocked {graceDays} days after that if unpaid." : ", never blocked automatically."))
            + (bill is not null ? $" Now: {bill.StateText}." : "");
        return Back(tenantId, "payments");
    }

    /// <summary>Let a blocked or late client in again until a day (or take that off with clear=true).</summary>
    [HttpPost("hold"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Hold(Guid tenantId, string? until, bool clear, string? tab)
    {
        var client = (await _tenants.ListAsync()).FirstOrDefault(t => t.TenantId == tenantId);
        if (client is null) { TempData["Error"] = "Client not found."; return Back(tab: tab); }
        if (clear)
        {
            await _billing.SetHoldAsync(tenantId, null);
            await _desk.RefreshAsync();
            TempData["Ok"] = $"{client.Name} follows the normal payment rules again.";
            return Back(tenantId, tab);
        }
        var day = ParseDay(until);
        var today = BillingCalculator.Today;
        if (day is null || day < today) { TempData["Error"] = "Pick a day from today on."; return Back(tenantId, tab); }
        await _billing.SetHoldAsync(tenantId, day);
        await _desk.RefreshAsync();
        TempData["Ok"] = $"{client.Name} can sign in again until {Money.Day(day.Value)}" +
            (client.IsActive ? "." : ". It is still paused: use Resume to let its people in.");
        return Back(tenantId, tab);
    }

    /// <summary>"12-2026" (month picker) or "2026-12" → 1 Dec 2026.</summary>
    private static DateOnly? ParseMonth(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        string[] formats = ["MM-yyyy", "M-yyyy", "yyyy-MM", "MM/yyyy"];
        return DateOnly.TryParseExact(text.Trim(), formats, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var d) ? new DateOnly(d.Year, d.Month, 1) : null;
    }

    /// <summary>"27-11-2026" (date picker) or "2026-11-27".</summary>
    private static DateOnly? ParseDay(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        string[] formats = ["dd-MM-yyyy", "d-M-yyyy", "yyyy-MM-dd", "dd/MM/yyyy"];
        return DateOnly.TryParseExact(text.Trim(), formats, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var d) ? d : null;
    }

    /// <summary>"1,49,500.00" or "₹ 1495" → 1495.</summary>
    private static decimal ParseAmount(string? text) =>
        decimal.TryParse(new string((text ?? "").Where(c => char.IsDigit(c) || c == '.').ToArray()),
            System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;

    private static SuiteAppFlags Flags(int[]? apps) => (SuiteAppFlags)(apps ?? []).Aggregate(0, (all, a) => all | a);
}
