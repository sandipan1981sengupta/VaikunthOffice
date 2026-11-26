using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Vaikunth.UI.TagHelpers;

/// <summary>
/// The bar at the bottom of an editor: whatever you put inside on the left, a zoom control on the right.
///   &lt;vk-status-bar zoom="100"&gt;&lt;span id="wordCount"&gt;0 words&lt;/span&gt;&lt;/vk-status-bar&gt;
/// </summary>
[HtmlTargetElement("vk-status-bar")]
public sealed class VkStatusBarTagHelper : TagHelper
{
    public int Zoom { get; set; } = 100;
    public bool ShowZoom { get; set; } = true;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var left = (await output.GetChildContentAsync()).GetContent();
        output.TagName = "footer";
        output.Attributes.SetAttribute("class", "vk-status");
        var zoom = !ShowZoom ? "" : $"""
            <div class="vk-zoom">
                <button type="button" data-cmd="zoomOut" title="Zoom out"><i class="fa fa-minus"></i></button>
                <input type="range" min="50" max="200" step="10" value="{Zoom}" data-cmd="zoom" aria-label="Zoom" />
                <button type="button" data-cmd="zoomIn" title="Zoom in"><i class="fa fa-plus"></i></button>
                <span class="vk-zoom-val" id="zoomVal">{Zoom}%</span>
            </div>
            """;
        output.Content.SetHtmlContent($"<div class='vk-status-left'>{left}</div>{zoom}");
    }
}
