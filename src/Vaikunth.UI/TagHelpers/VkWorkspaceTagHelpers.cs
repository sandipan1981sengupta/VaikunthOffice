using System.Globalization;
using Microsoft.AspNetCore.Razor.TagHelpers;
using static Vaikunth.UI.TagHelpers.VkHtml;

namespace Vaikunth.UI.TagHelpers;

/// <summary>
/// The title bar of a work page: icon, heading, a short line, and a toolbar of big icon buttons on the right.
///   &lt;vk-action-header id="AdminHeader" heading="Client workspaces" subtitle="Super admin · 3 clients" icon="shield"&gt;
///       &lt;vk-action-btn text="Refresh" icon="refresh" cmd="refresh" tone="blue" /&gt;
///   &lt;/vk-action-header&gt;
/// </summary>
[HtmlTargetElement("vk-action-header")]
public sealed class VkActionHeaderTagHelper : TagHelper
{
    public string? Id { get; set; }
    public string Heading { get; set; } = "";
    public string? Subtitle { get; set; }
    public string Icon { get; set; } = "shield";

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var buttons = (await output.GetChildContentAsync()).GetContent();
        output.TagName = "header";
        output.TagMode = TagMode.StartTagAndEndTag;
        if (!string.IsNullOrEmpty(Id)) output.Attributes.SetAttribute("id", Id);
        output.Attributes.SetAttribute("class", "vk-ahead vk-glass-card");
        output.Content.SetHtmlContent($"""
            <span class="vk-ahead-ic" aria-hidden="true"><i class="{Fa(Icon)}"></i></span>
            <div class="vk-ahead-text"><h1>{E(Heading)}</h1>{(Subtitle is null ? "" : $"<p>{E(Subtitle)}</p>")}</div>
            <div class="vk-ahead-tools" role="toolbar" aria-label="{E(Heading)}">{buttons}</div>
            """);
    }
}

/// <summary>
/// One toolbar button of &lt;vk-action-header&gt;: icon above a short label. The click goes to VK.on(cmd).
///   tone = blue | green | red | amber | pink | slate · scope="client" hides it on tabs that have no selection.
///   separator="true" draws a thin line before it.
/// </summary>
[HtmlTargetElement("vk-action-btn", TagStructure = TagStructure.WithoutEndTag)]
public sealed class VkActionBtnTagHelper : TagHelper
{
    public string Text { get; set; } = "";
    public string Icon { get; set; } = "circle";
    public string Cmd { get; set; } = "";
    public string Tone { get; set; } = "slate";
    public string? Tooltip { get; set; }
    public string? Scope { get; set; }
    public bool Separator { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "button";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("type", "button");
        output.Attributes.SetAttribute("class", Cls("vk-abtn", $"tone-{Tone}", Separator ? "sep" : null));
        output.Attributes.SetAttribute("data-cmd", Cmd);
        output.Attributes.SetAttribute("title", Tooltip ?? Text);
        if (!string.IsNullOrEmpty(Scope)) output.Attributes.SetAttribute("data-scope", Scope);
        output.Content.SetHtmlContent($"""<i class="{Fa(Icon)}" aria-hidden="true"></i><span>{E(Text)}</span>""");
    }
}

/// <summary>
/// One figure in a key-figures strip (put several inside &lt;section class="vk-kpis vk-glass-card"&gt;).
///   &lt;vk-kpi icon="hdd-o" tone="pink" label="Storage" value="1 MB" sub="of 5 GB" percent="0.4" /&gt;
/// percent (optional) draws a usage bar; it turns amber at 75% and red at 90%.
/// </summary>
[HtmlTargetElement("vk-kpi", TagStructure = TagStructure.WithoutEndTag)]
public sealed class VkKpiTagHelper : TagHelper
{
    public string Icon { get; set; } = "bar-chart";
    public string Tone { get; set; } = "blue";
    public string Label { get; set; } = "";
    public string Value { get; set; } = "";
    public string? Sub { get; set; }
    /// <summary>Shown before Sub as a green dot (e.g. "3 active").</summary>
    public string? Dot { get; set; }
    public double? Percent { get; set; }
    /// <summary>Text of the coloured percent badge, e.g. "less than 0.1%".</summary>
    public string? PercentText { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", Cls("vk-kpi", Percent is null ? null : "grow"));
        string bar = "", badge = "";
        if (Percent is double p)
        {
            var level = p >= 90 ? "full" : p >= 75 ? "warn" : "";
            var width = p <= 0 ? 0 : Math.Max(2.5, Math.Min(100, p));
            bar = $"""<div class="vk-kpi-bar" role="progressbar" aria-valuemin="0" aria-valuemax="100" aria-valuenow="{p.ToString("0.#", CultureInfo.InvariantCulture)}"><i class="{level}" style="width:{width.ToString("0.##", CultureInfo.InvariantCulture)}%"></i></div>""";
            if (!string.IsNullOrEmpty(PercentText)) badge = $"""<span class="vk-kpi-pct {level}">{E(PercentText)}</span> """;
        }
        var dot = string.IsNullOrEmpty(Dot) ? "" : $"""<span class="vk-kpi-dot"></span> {E(Dot)}{(Sub is null ? "" : " · ")}""";
        output.Content.SetHtmlContent($"""
            <span class="vk-kpi-ic tone-{E(Tone)}" aria-hidden="true"><i class="{Fa(Icon)}"></i></span>
            <div class="vk-kpi-body"><div class="vk-kpi-label">{E(Label)}</div><div class="vk-kpi-val">{E(Value)}</div>{bar}
                <div class="vk-kpi-sub">{dot}{badge}{E(Sub)}</div></div>
            """);
    }
}

/// <summary>
/// Pill tabs that switch panes on the page: each &lt;vk-tab key="plans"&gt; shows the element with data-pane="plans".
///   &lt;vk-tabs id="adminTabs" active="clients"&gt;&lt;vk-tab key="clients" text="Clients" icon="building-o" badge="3" hint="..." /&gt;&lt;/vk-tabs&gt;
/// The open tab is kept in the address (?tab=plans), so a page that reloads after Save comes back to it.
/// </summary>
[HtmlTargetElement("vk-tabs")]
public sealed class VkTabsTagHelper : TagHelper
{
    public string? Id { get; set; }
    public string? Active { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        context.Items["vk-tabs-active"] = Active;
        var tabs = (await output.GetChildContentAsync()).GetContent();
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        if (!string.IsNullOrEmpty(Id)) output.Attributes.SetAttribute("id", Id);
        output.Attributes.SetAttribute("class", "vk-tabbar");
        output.Content.SetHtmlContent($"""<nav class="vk-tabs vk-glass-card" role="tablist" data-vk-tabs>{tabs}</nav><span class="vk-tab-hint" data-tab-hint></span>""");
    }
}

[HtmlTargetElement("vk-tab", ParentTag = "vk-tabs", TagStructure = TagStructure.WithoutEndTag)]
public sealed class VkTabTagHelper : TagHelper
{
    public string Key { get; set; } = "";
    public string Text { get; set; } = "";
    public string? Icon { get; set; }
    public string? Badge { get; set; }
    /// <summary>The line next to the tabs while this tab is open.</summary>
    public string? Hint { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var on = string.Equals(context.Items.TryGetValue("vk-tabs-active", out var a) ? a as string : null, Key, StringComparison.OrdinalIgnoreCase);
        output.TagName = "button";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("type", "button");
        output.Attributes.SetAttribute("role", "tab");
        output.Attributes.SetAttribute("class", Cls("vk-tab", on ? "on" : null));
        output.Attributes.SetAttribute("aria-selected", on ? "true" : "false");
        output.Attributes.SetAttribute("data-tab", Key);
        if (Hint is not null) output.Attributes.SetAttribute("data-hint", Hint);
        output.Content.SetHtmlContent($"""{(Icon is null ? "" : $"<i class=\"{Fa(Icon)}\" aria-hidden=\"true\"></i>")}<span>{E(Text)}</span>{(Badge is null ? "" : $"<b class=\"vk-tab-badge\">{E(Badge)}</b>")}""");
    }
}

/// <summary>
/// A group of fields with a two-tone title, inside windows and cards: &lt;vk-group title="Company|details"&gt;...&lt;/vk-group&gt;
/// The part after | is shown in the second colour.
/// </summary>
[HtmlTargetElement("vk-group")]
public sealed class VkGroupTagHelper : TagHelper
{
    public string Title { get; set; } = "";
    public string? CssClass { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var body = (await output.GetChildContentAsync()).GetContent();
        var parts = Title.Split('|', 2);
        output.TagName = "section";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", Cls("vk-group", CssClass));
        output.Content.SetHtmlContent($"""<h4 class="vk-group-title">{E(parts[0].Trim())}{(parts.Length > 1 ? $" <span>{E(parts[1].Trim())}</span>" : "")}</h4><div class="vk-group-body">{body}</div>""");
    }
}
