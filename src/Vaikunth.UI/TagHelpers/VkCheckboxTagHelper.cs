using Microsoft.AspNetCore.Razor.TagHelpers;
using static Vaikunth.UI.TagHelpers.VkHtml;

namespace Vaikunth.UI.TagHelpers;

/// <summary>
/// A checkbox, or a toggle switch with IsToggle="true". Posts "true" when on (with a hidden "false" like asp-for does).
///   &lt;vk-checkbox id="RememberMe" label="Keep me signed in" is-toggle="true" checked="true" /&gt;
/// </summary>
[HtmlTargetElement("vk-checkbox", TagStructure = TagStructure.WithoutEndTag)]
public sealed class VkCheckboxTagHelper : TagHelper
{
    public string Id { get; set; } = "";
    public string? Name { get; set; }
    public string? Label { get; set; }
    public bool Checked { get; set; }
    public bool IsToggle { get; set; }
    /// <summary>A second, smaller line under the label.</summary>
    public string? Description { get; set; }
    public bool Disabled { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "label";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", IsToggle ? "vk-check vk-toggle" : "vk-check");
        output.Attributes.SetAttribute("for", Id);
        var name = E(Name ?? Id);
        output.Content.SetHtmlContent($"""
            <input type="checkbox" id="{E(Id)}" name="{name}" value="true"{(Checked ? " checked" : "")}{(Disabled ? " disabled" : "")} />
            <span class="vk-check-box" aria-hidden="true"></span>
            <span class="vk-check-text">{E(Label)}{(Description is null ? "" : $"<small class=\"vk-check-desc\">{E(Description)}</small>")}</span>
            <input type="hidden" name="{name}" value="false" />
            """);
    }
}
