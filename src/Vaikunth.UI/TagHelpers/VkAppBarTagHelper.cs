using Microsoft.AspNetCore.Razor.TagHelpers;
using Vaikunth.UI.Infrastructure;
using static Vaikunth.UI.TagHelpers.VkHtml;

namespace Vaikunth.UI.TagHelpers;

/// <summary>
/// The coloured title bar of an editor: app launcher (waffle), app icon and name, the file title (editable),
/// save state, Share and the user's avatar.
///   &lt;vk-app-bar app="pages" doc-title="@Model.Title" user-name="@User.DisplayName()" home-url="/" /&gt;
/// </summary>
[HtmlTargetElement("vk-app-bar", TagStructure = TagStructure.WithoutEndTag)]
public sealed class VkAppBarTagHelper : TagHelper
{
    public string App { get; set; } = "pages";
    public string DocTitle { get; set; } = "Untitled";
    public string UserName { get; set; } = "";
    public int AvatarTone { get; set; }
    /// <summary>The person's photo (User.PhotoUrl()); null shows their initials.</summary>
    public string? PhotoUrl { get; set; }
    public string HomeUrl { get; set; } = "/";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (string.IsNullOrWhiteSpace(PhotoUrl)) PhotoUrl = null;   // a null @value arrives as ""
        var a = SuiteApps.For(App);
        output.TagName = "header";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", "vk-appbar");
        output.Content.SetHtmlContent($"""
            <a class="vk-appbar-waffle" href="{E(HomeUrl)}" title="Vaikunth Office: all apps"><i class="fa fa-th"></i></a>
            <a class="vk-appbar-app" href="{E(HomeUrl)}"><img src="{a.IconUrl}" alt="" width="26" height="26" /><span>{E(a.Name)}</span></a>
            <span class="vk-appbar-sep"></span>
            <input class="vk-doc-title" id="docTitle" value="{E(DocTitle)}" aria-label="File name" spellcheck="false" />
            <span class="vk-save-state" id="saveState"><i class="fa fa-cloud"></i> Saved</span>
            <span class="vk-appbar-fill"></span>
            <label class="vk-appbar-search"><i class="fa fa-search"></i><input placeholder="Search (Alt+Q)" /></label>
            <span class="vk-appbar-fill"></span>
            <button type="button" class="vk-appbar-btn" data-cmd="present" hidden><i class="fa fa-play"></i> Present</button>
            <button type="button" class="vk-appbar-btn vk-appbar-about" data-open-dialog="aboutDialog" title="About Vaikunth Office" aria-label="About Vaikunth Office"><i class="fa fa-info-circle"></i></button>
            <button type="button" class="vk-appbar-btn" data-cmd="fileExport" title="Download a copy ({E(SuiteApps.DownloadExt(a.App))})"><i class="fa fa-download"></i> <span class="vk-appbar-label">Download</span></button>
            <button type="button" class="vk-appbar-btn vk-share" data-cmd="share"><i class="fa fa-share-alt"></i> Share</button>
            <span class="vk-avatar tone-{AvatarTone}{(PhotoUrl is null ? "" : " has-photo")}" title="{E(UserName)}">{(PhotoUrl is null ? E(Vaikunth.DtoLibrary.Names.Initials(UserName)) : $"<img src=\"{E(PhotoUrl)}\" alt=\"\" />")}</span>
            """);
    }

}
