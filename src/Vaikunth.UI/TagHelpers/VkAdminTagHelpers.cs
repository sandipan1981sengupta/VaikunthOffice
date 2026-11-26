using System.Text;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Vaikunth.DtoLibrary;
using Vaikunth.UI.Infrastructure;
using static Vaikunth.UI.TagHelpers.VkHtml;

namespace Vaikunth.UI.TagHelpers;

/// <summary>
/// A number tile for dashboards.
///   &lt;vk-stat-card icon="building" label="Clients" value="5" sub="4 active" tone="navy" /&gt;
/// tone = navy | green | saffron | peacock | gold
/// </summary>
[HtmlTargetElement("vk-stat-card", TagStructure = TagStructure.WithoutEndTag)]
public sealed class VkStatCardTagHelper : TagHelper
{
    public string Icon { get; set; } = "bar-chart";
    public string Label { get; set; } = "";
    public string Value { get; set; } = "";
    public string? Sub { get; set; }
    public string Tone { get; set; } = "navy";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", $"vk-stat tone-{E(Tone)}");
        output.Content.SetHtmlContent($"""
            <span class="vk-stat-ic"><i class="{Fa(Icon)}"></i></span>
            <span class="vk-stat-text"><small>{E(Label)}</small><b>{E(Value)}</b>{(Sub is null ? "" : $"<em>{E(Sub)}</em>")}</span>
            """);
    }
}

/// <summary>
/// A thin usage bar that turns amber at 75% and red at 90%.
///   &lt;vk-progress percent="42" text="4.2 GB of 10 GB" /&gt;
/// </summary>
[HtmlTargetElement("vk-progress", TagStructure = TagStructure.WithoutEndTag)]
public sealed class VkProgressTagHelper : TagHelper
{
    public int Percent { get; set; }
    public string? Text { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var p = Math.Clamp(Percent, 0, 100);
        var level = p >= 90 ? "high" : p >= 75 ? "mid" : "low";
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", $"vk-progress {level}");
        output.Content.SetHtmlContent($"<div class='vk-progress-track'><span style='width:{p}%'></span></div>{(Text is null ? "" : $"<small>{E(Text)}</small>")}");
    }
}

/// <summary>
/// A labelled drop-down that posts with the form.
///   &lt;vk-select id="PlanId" label="Plan" options="1|Starter,2|Business" selected="2" /&gt;
/// </summary>
[HtmlTargetElement("vk-select", TagStructure = TagStructure.WithoutEndTag)]
public sealed class VkSelectTagHelper : TagHelper
{
    public string Id { get; set; } = "";
    public string? Name { get; set; }
    public string? Label { get; set; }
    public string Options { get; set; } = "";
    public string? Selected { get; set; }
    public string? Icon { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", "vk-field");
        var opts = new StringBuilder();
        foreach (var raw in Options.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = raw.Split('|');
            opts.Append($"<option value='{E(parts[0])}'{(parts[0] == Selected ? " selected" : "")}>{E(parts.Length > 1 ? parts[1] : parts[0])}</option>");
        }
        output.Content.SetHtmlContent($"""
            {(Label is null ? "" : $"<label class='vk-label' for='{E(Id)}'>{E(Label)}</label>")}
            <div class="vk-input-box{(Icon is null ? "" : " has-icon")}">
                {(Icon is null ? "" : $"<i class='{Fa(Icon)} vk-input-ic'></i>")}
                <select id="{E(Id)}" name="{E(Name ?? Id)}" class="vk-input vk-select">{opts}</select>
                <i class="fa fa-angle-down vk-select-caret"></i>
            </div>
            """);
    }
}

/// <summary>
/// Pick which of the three apps a client gets: three toggle cards that post as one or more "name" values (1, 2, 4).
///   &lt;vk-app-picker name="Apps" value="7" label="Apps for this client" /&gt;
/// </summary>
[HtmlTargetElement("vk-app-picker", TagStructure = TagStructure.WithoutEndTag)]
public sealed class VkAppPickerTagHelper : TagHelper
{
    public string Name { get; set; } = "Apps";
    public int Value { get; set; } = (int)SuiteAppFlags.All;
    public string? Label { get; set; }
    public string IdPrefix { get; set; } = "app";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "fieldset";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", "vk-app-picker");
        var sb = new StringBuilder(Label is null ? "" : $"<legend class='vk-label'>{E(Label)}</legend>");
        sb.Append("<div class='vk-app-picker-row'>");
        foreach (var a in SuiteApps.All)
        {
            var flag = (int)a.App.Flag();
            var on = (Value & flag) != 0;
            sb.Append($"""
                <label class="vk-app-pick" style="--tile:{a.Color};--tile-tint:{a.Tint}">
                    <input type="checkbox" name="{E(Name)}" value="{flag}" id="{E(IdPrefix)}-{a.Key}"{(on ? " checked" : "")} />
                    <img src="{a.IconUrl}" alt="" width="30" height="30" />
                    <span><b>{E(a.Name)}</b><small>{E(a.Kind)}</small></span>
                    <i class="fa fa-check-circle vk-app-pick-tick"></i>
                </label>
                """);
        }
        output.Content.SetHtmlContent(sb.Append("</div>").ToString());
    }
}

/// <summary>Small icons for the apps a client has, greyed out for the ones it doesn't.  &lt;vk-app-badges value="5" /&gt;</summary>
[HtmlTargetElement("vk-app-badges", TagStructure = TagStructure.WithoutEndTag)]
public sealed class VkAppBadgesTagHelper : TagHelper
{
    public int Value { get; set; } = (int)SuiteAppFlags.All;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", "vk-app-badges");
        var sb = new StringBuilder();
        foreach (var a in SuiteApps.All)
        {
            var on = (Value & (int)a.App.Flag()) != 0;
            sb.Append($"<img src='{a.IconUrl}' alt='{E(a.Kind)}' title='{E(a.Name)}{(on ? "" : " (not included)")}' width='22' height='22' class='{(on ? "" : "off")}' />");
        }
        output.Content.SetHtmlContent(sb.ToString());
    }
}
