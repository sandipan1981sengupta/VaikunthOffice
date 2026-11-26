using Microsoft.AspNetCore.Razor.TagHelpers;
using static Vaikunth.UI.TagHelpers.VkHtml;

namespace Vaikunth.UI.TagHelpers;

/// <summary>
/// A person's round avatar: their photo when they have one, otherwise their initials on a colour.
///   &lt;vk-avatar name="Priya Sharma" tone="2" photo-url="@User.PhotoUrl()" size="34" /&gt;
/// </summary>
[HtmlTargetElement("vk-avatar", TagStructure = TagStructure.WithoutEndTag)]
public sealed class VkAvatarTagHelper : TagHelper
{
    public string Name { get; set; } = "";
    public int Tone { get; set; }
    public string? PhotoUrl { get; set; }
    public int? Size { get; set; }
    public string? Class { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        // Razor passes a null @value to a string attribute as "", so treat empty as "no photo"
        if (string.IsNullOrWhiteSpace(PhotoUrl)) PhotoUrl = null;
        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", $"vk-avatar tone-{Tone}{(PhotoUrl is null ? "" : " has-photo")} {Class}".TrimEnd());
        output.Attributes.SetAttribute("title", Name);
        if (Size is int s) output.Attributes.SetAttribute("style", $"width:{s}px;height:{s}px;font-size:{Math.Max(10, s * 0.38):0}px");
        output.Content.SetHtmlContent(PhotoUrl is null
            ? E(Vaikunth.DtoLibrary.Names.Initials(Name))
            : $"<img src=\"{E(PhotoUrl)}\" alt=\"\" loading=\"lazy\" />");
    }
}
