using Microsoft.AspNetCore.Razor.TagHelpers;
using Vaikunth.UI.Infrastructure;
using static Vaikunth.UI.TagHelpers.VkHtml;

namespace Vaikunth.UI.TagHelpers;

/// <summary>
/// A big launcher tile: icon, app name, what kind of app it is, and a short line.
///   &lt;vk-app-tile app="workbook" href="/workbook/editor" new-href="/workbook/editor/new" target="_blank" /&gt;
/// </summary>
[HtmlTargetElement("vk-app-tile", TagStructure = TagStructure.WithoutEndTag)]
public sealed class VkAppTileTagHelper : TagHelper
{
    public string App { get; set; } = "pages";
    public string Href { get; set; } = "#";
    public string? NewHref { get; set; }
    public bool IsCurrent { get; set; }
    /// <summary>"_blank" opens the app in its own browser tab.</summary>
    public string? Target { get; set; }
    /// <summary>The client's plan doesn't include this app: shown greyed out with a lock, no links.</summary>
    public bool IsLocked { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var a = SuiteApps.For(App);
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", Cls("vk-tile", $"vk-tile-{a.Key}", IsCurrent && !IsLocked ? "is-current" : null, IsLocked ? "is-locked" : null));
        output.Attributes.SetAttribute("style", $"--tile:{a.Color};--tile-tint:{a.Tint}");
        if (IsLocked)
        {
            output.Content.SetHtmlContent($"""
                <div class="vk-tile-main" title="Not in your company's plan">
                    <img src="{a.IconUrl}" alt="" width="96" height="96" class="vk-tile-icon" />
                    <span class="vk-tile-name">{E(a.Name)}</span>
                    <span class="vk-tile-kind">{E(a.Kind)}</span>
                    <span class="vk-tile-line"><i class="fa fa-lock"></i> Not in your plan</span>
                </div>
                """);
            return;
        }
        var target = string.IsNullOrEmpty(Target) ? "" : $" target='{E(Target)}'";
        var newBtn = string.IsNullOrEmpty(NewHref) ? "" :
            $"<a class='vk-tile-new' href='{E(NewHref)}'{target} title='New {E(a.Name)} file'><i class='fa fa-plus'></i> New</a>";
        output.Content.SetHtmlContent($"""
            <a class="vk-tile-main" href="{E(Href)}"{target}>
                <span class="vk-tile-glow" aria-hidden="true"></span>
                <img src="{a.IconUrl}" alt="" width="96" height="96" class="vk-tile-icon" />
                <span class="vk-tile-name">{E(a.Name)}</span>
                <span class="vk-tile-kind">{E(a.Kind)}</span>
                <span class="vk-tile-line">{E(a.Tagline)}</span>
            </a>
            {newBtn}
            """);
    }
}
