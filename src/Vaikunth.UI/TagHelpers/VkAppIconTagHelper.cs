using Microsoft.AspNetCore.Razor.TagHelpers;
using Vaikunth.UI.Infrastructure;
using static Vaikunth.UI.TagHelpers.VkHtml;

namespace Vaikunth.UI.TagHelpers;

/// <summary>
/// The icon of one app of the suite, or of the suite itself.
///   &lt;vk-app-icon app="pages" size="48" /&gt;   app = pages | workbook | slides | suite
/// </summary>
[HtmlTargetElement("vk-app-icon", TagStructure = TagStructure.WithoutEndTag)]
public sealed class VkAppIconTagHelper : TagHelper
{
    public string App { get; set; } = "pages";
    public int Size { get; set; } = 40;
    public string? CssClass { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "img";
        output.TagMode = TagMode.SelfClosing;
        var isSuite = string.Equals(App, "suite", StringComparison.OrdinalIgnoreCase);
        var info = isSuite ? null : SuiteApps.For(App);
        output.Attributes.SetAttribute("src", isSuite ? "/_content/Vaikunth.UI/images/vaikunth-logo.svg" : info!.IconUrl);
        output.Attributes.SetAttribute("alt", isSuite ? "Vaikunth Desk" : info!.Name);
        output.Attributes.SetAttribute("width", Size);
        output.Attributes.SetAttribute("height", Size);
        output.Attributes.SetAttribute("class", Cls("vk-app-icon", CssClass));
    }
}
