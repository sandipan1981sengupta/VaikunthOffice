using Microsoft.AspNetCore.Razor.TagHelpers;
using static Vaikunth.UI.TagHelpers.VkHtml;

namespace Vaikunth.UI.TagHelpers;

/// <summary>
/// Labelled text box with an icon on the left (like ShankhNaad's modern-input).
/// IsPassword="true" masks it and adds a show/hide eye.
///   &lt;vk-input id="Email" name="Email" label="Work email" icon="envelope" mandatory="true" /&gt;
/// </summary>
[HtmlTargetElement("vk-input", TagStructure = TagStructure.WithoutEndTag)]
public sealed class VkInputTagHelper : TagHelper
{
    public string Id { get; set; } = "";
    public string? Name { get; set; }
    public string? Label { get; set; }
    public string? Placeholder { get; set; }
    public string? Value { get; set; }
    public string? Icon { get; set; }
    public string InputType { get; set; } = "text";
    public bool IsPassword { get; set; }
    public bool Mandatory { get; set; }
    public bool Autofocus { get; set; }
    public int? MaxLength { get; set; }
    public string? Autocomplete { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", "vk-field");
        var icon = Icon ?? (IsPassword ? "key" : null);
        var type = IsPassword ? "password" : InputType;
        var label = string.IsNullOrWhiteSpace(Label) ? "" :
            $"<label class='vk-label' for='{E(Id)}'>{E(Label)}{(Mandatory ? " <span class='vk-req'>*</span>" : "")}</label>";
        var eye = IsPassword
            ? "<button type='button' class='vk-input-eye' data-vk-eye tabindex='-1' aria-label='Show password'><i class='fa fa-eye'></i></button>"
            : "";
        output.Content.SetHtmlContent($"""
            {label}
            <div class="vk-input-box{(icon is null ? "" : " has-icon")}">
                {(icon is null ? "" : $"<i class='{Fa(icon)} vk-input-ic' aria-hidden='true'></i>")}
                <input id="{E(Id)}" name="{E(Name ?? Id)}" type="{E(type)}" class="vk-input" value="{E(Value)}"
                       placeholder="{E(Placeholder)}"{(Mandatory ? " required" : "")}{(Autofocus ? " autofocus" : "")}
                       {(MaxLength is int m ? $"maxlength='{m}'" : "")} {(Autocomplete is null ? "" : $"autocomplete='{E(Autocomplete)}'")} />
                {eye}
            </div>
            """);
    }
}
