using Microsoft.AspNetCore.Razor.TagHelpers;
using static Vaikunth.UI.TagHelpers.VkHtml;

namespace Vaikunth.UI.TagHelpers;

/// <summary>
/// A modal &lt;dialog&gt; with a title, body and buttons. Open with document.getElementById(id).showModal()
/// or with any element that has data-open-dialog="id".
///   &lt;vk-dialog id="shareDialog" title="Share" icon="share-alt" ok-text="Send" ok-cmd="shareSend"&gt;...body...&lt;/vk-dialog&gt;
/// Set Action="/admin/create" to make the dialog a real form that posts there (put @Html.AntiForgeryToken() in the body);
/// the OK button then submits it, and Close / × just close it.
/// </summary>
[HtmlTargetElement("vk-dialog")]
public sealed class VkDialogTagHelper : TagHelper
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Icon { get; set; }
    public string? OkText { get; set; }
    public string? OkCmd { get; set; }
    public string CancelText { get; set; } = "Close";
    public string Width { get; set; } = "460px";
    public string? Action { get; set; }
    /// <summary>"multipart/form-data" when the dialog uploads a file.</summary>
    public string? Enctype { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var body = (await output.GetChildContentAsync()).GetContent();
        output.TagName = "dialog";
        output.Attributes.SetAttribute("id", Id);
        output.Attributes.SetAttribute("class", "vk-dialog");
        output.Attributes.SetAttribute("style", $"width:min({Width}, calc(100vw - 32px))");
        var posts = !string.IsNullOrEmpty(Action);
        var ok = string.IsNullOrEmpty(OkText) ? "" : posts
            ? $"<button type='submit' class='vk-btn vk-btn-primary'>{E(OkText)}</button>"
            : $"<button type='button' class='vk-btn vk-btn-primary' data-cmd='{E(OkCmd)}'>{E(OkText)}</button>";
        var close = posts ? " formmethod='dialog' formnovalidate" : "";
        output.Content.SetHtmlContent($"""
            <form method="{(posts ? "post" : "dialog")}"{(posts ? $" action='{E(Action)}'" : "")}{(Enctype is null ? "" : $" enctype='{E(Enctype)}'")} class="vk-dialog-inner">
                <div class="vk-dialog-head">
                    {(Icon is null ? "" : $"<span class='vk-dialog-ic'><i class='{Fa(Icon)}'></i></span>")}
                    <h3>{E(Title)}</h3>
                    <button class="vk-dialog-x" value="cancel" aria-label="Close"{close}><i class="fa fa-times"></i></button>
                </div>
                <div class="vk-dialog-body">{body}</div>
                <div class="vk-dialog-foot">
                    <button class="vk-btn vk-btn-light" value="cancel"{close}>{E(CancelText)}</button>
                    {ok}
                </div>
            </form>
            """);
    }
}
