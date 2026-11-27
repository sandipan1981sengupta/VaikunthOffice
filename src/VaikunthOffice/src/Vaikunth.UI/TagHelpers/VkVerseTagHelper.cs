using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Vaikunth.UI.TagHelpers;

/// <summary>
/// The suite's verse, shown where ShankhNaad shows its Rig Veda line:
/// on the sign-in page (variant="hero"), in About (variant="box") and on the launcher (variant="inline").
///   &lt;vk-verse variant="hero" /&gt;
/// </summary>
[HtmlTargetElement("vk-verse", TagStructure = TagStructure.WithoutEndTag)]
public sealed class VkVerseTagHelper : TagHelper
{
    public const string Sanskrit = "Yogah karmasu kaushalam.";
    public const string Meaning = "Excellence in every action.";
    /// <summary>The line as the user worded it, used where only one line fits.</summary>
    public const string FullLine = "Yogah karmasu kaushalam: Excellence in every action.";
    public const string Source = "Bhagavad Gita 2.50";

    public string Variant { get; set; } = "hero";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagMode = TagMode.StartTagAndEndTag;
        switch (Variant)
        {
            case "box":
                output.TagName = "div";
                output.Attributes.SetAttribute("class", "vk-verse-box");
                output.Content.SetHtmlContent($"<em>{Sanskrit}</em><span>“{Meaning}” — {Source}</span>");
                break;
            case "inline":
                output.TagName = "p";
                output.Attributes.SetAttribute("class", "vk-verse-inline");
                output.Content.SetHtmlContent($"<i class='fa fa-sun-o' aria-hidden='true'></i> <em>{FullLine}</em> <span>· {Source}</span>");
                break;
            default:
                output.TagName = "blockquote";
                output.Attributes.SetAttribute("class", "vk-verse");
                output.Content.SetHtmlContent($"<em>{Sanskrit}</em><span>“{Meaning}” · {Source}</span>");
                break;
        }
    }
}
