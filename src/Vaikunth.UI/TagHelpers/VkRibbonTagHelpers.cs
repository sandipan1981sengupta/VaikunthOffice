using System.Text;
using Microsoft.AspNetCore.Razor.TagHelpers;
using static Vaikunth.UI.TagHelpers.VkHtml;

namespace Vaikunth.UI.TagHelpers;

/// <summary>
/// The command ribbon at the top of each editor. Tabs register themselves, so the tab strip is built for you:
/// <code>
/// &lt;vk-ribbon&gt;
///   &lt;vk-ribbon-tab title="Home" active="true"&gt;
///     &lt;vk-ribbon-group label="Font"&gt;
///       &lt;vk-ribbon-select cmd="fontName" options="Inter,Georgia,Courier New" width="130" /&gt;
///       &lt;vk-ribbon-btn cmd="bold" icon="bold" tooltip="Bold (Ctrl+B)" toggle="true" /&gt;
///       &lt;vk-ribbon-color cmd="foreColor" icon="font" value="#C24A1C" tooltip="Font colour" /&gt;
///     &lt;/vk-ribbon-group&gt;
///   &lt;/vk-ribbon-tab&gt;
/// &lt;/vk-ribbon&gt;
/// </code>
/// Buttons render data-cmd / data-arg; the app's script decides what each command does.
/// </summary>
[HtmlTargetElement("vk-ribbon")]
public sealed class VkRibbonTagHelper : TagHelper
{
    internal const string TabsKey = "vk-ribbon-tabs";

    /// <summary>Optional: extra buttons in the "file" slot before the tabs (e.g. a File menu).</summary>
    public string? FileLabel { get; set; } = "File";

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var tabs = new List<(string Title, string Key, bool Active)>();
        context.Items[TabsKey] = tabs;
        var body = (await output.GetChildContentAsync()).GetContent();

        output.TagName = "div";
        output.Attributes.SetAttribute("class", "vk-ribbon");
        output.Attributes.SetAttribute("role", "toolbar");
        if (tabs.Count > 0 && !tabs.Any(t => t.Active)) tabs[0] = tabs[0] with { Active = true };

        var strip = new StringBuilder("<div class='vk-ribbon-tabs' role='tablist'>");
        if (!string.IsNullOrEmpty(FileLabel))
            strip.Append($"<button type='button' class='vk-ribbon-file' data-cmd='fileMenu'>{E(FileLabel)}</button>");
        foreach (var t in tabs)
            strip.Append($"<button type='button' role='tab' class='vk-ribbon-tab{(t.Active ? " active" : "")}' data-tab='{E(t.Key)}'>{E(t.Title)}</button>");
        strip.Append("</div>");

        // the tab that was marked active is shown; the others are hidden until clicked (vaikunth.js)
        var activeKey = tabs.FirstOrDefault(t => t.Active).Key;
        if (activeKey is not null) body = body.Replace($"data-panel='{activeKey}' hidden", $"data-panel='{activeKey}'");
        output.Content.SetHtmlContent(strip + "<div class='vk-ribbon-body'>" + body + "</div>");
    }
}

[HtmlTargetElement("vk-ribbon-tab", ParentTag = "vk-ribbon")]
public sealed class VkRibbonTabTagHelper : TagHelper
{
    public string Title { get; set; } = "";
    public bool Active { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var key = Title.ToLowerInvariant().Replace(' ', '-');
        if (context.Items[VkRibbonTagHelper.TabsKey] is List<(string, string, bool)> tabs) tabs.Add((Title, key, Active));
        var inner = (await output.GetChildContentAsync()).GetContent();
        output.TagName = null;
        output.Content.SetHtmlContent($"<div class='vk-ribbon-panel' role='tabpanel' data-panel='{E(key)}' hidden>{inner}</div>");
    }
}

[HtmlTargetElement("vk-ribbon-group", ParentTag = "vk-ribbon-tab")]
public sealed class VkRibbonGroupTagHelper : TagHelper
{
    public string Label { get; set; } = "";

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var inner = (await output.GetChildContentAsync()).GetContent();
        output.TagName = "div";
        output.Attributes.SetAttribute("class", "vk-ribbon-group");
        output.Content.SetHtmlContent($"<div class='vk-ribbon-group-items'>{inner}</div><div class='vk-ribbon-group-label'>{E(Label)}</div>");
    }
}

/// <summary>One ribbon command. Size="large" shows the icon above the text (like Paste or New slide).</summary>
[HtmlTargetElement("vk-ribbon-btn", TagStructure = TagStructure.WithoutEndTag)]
public sealed class VkRibbonBtnTagHelper : TagHelper
{
    public string Cmd { get; set; } = "";
    public string? Arg { get; set; }
    public string? Icon { get; set; }
    public string? Text { get; set; }
    public string? Tooltip { get; set; }
    public string Size { get; set; } = "small";
    public bool Toggle { get; set; }
    public string? CssClass { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "button";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("type", "button");
        output.Attributes.SetAttribute("class", Cls("vk-rb", Size == "large" ? "vk-rb-lg" : null, Toggle ? "vk-rb-toggle" : null, string.IsNullOrEmpty(Text) ? "vk-rb-icon" : null, CssClass));
        output.Attributes.SetAttribute("data-cmd", Cmd);
        if (!string.IsNullOrEmpty(Arg)) output.Attributes.SetAttribute("data-arg", Arg);
        var tip = Tooltip ?? Text ?? Cmd;
        output.Attributes.SetAttribute("title", tip);
        output.Attributes.SetAttribute("aria-label", tip);
        output.Content.SetHtmlContent(
            (string.IsNullOrEmpty(Icon) ? "" : $"<i class='{Fa(Icon)}' aria-hidden='true'></i>") +
            (string.IsNullOrEmpty(Text) ? "" : $"<span>{E(Text)}</span>"));
    }
}

/// <summary>A drop-down in the ribbon (font, size, number format, theme...). Options: "a,b,c" or "value|Text,value|Text".</summary>
[HtmlTargetElement("vk-ribbon-select", TagStructure = TagStructure.WithoutEndTag)]
public sealed class VkRibbonSelectTagHelper : TagHelper
{
    public string Cmd { get; set; } = "";
    public string Options { get; set; } = "";
    public string? Selected { get; set; }
    public int Width { get; set; } = 110;
    public string? Tooltip { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "select";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", "vk-rsel");
        output.Attributes.SetAttribute("data-cmd", Cmd);
        output.Attributes.SetAttribute("style", $"width:{Width}px");
        output.Attributes.SetAttribute("title", Tooltip ?? Cmd);
        var sb = new StringBuilder();
        foreach (var raw in Options.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = raw.Split('|');
            var value = parts[0];
            var text = parts.Length > 1 ? parts[1] : parts[0];
            sb.Append($"<option value='{E(value)}'{(value == Selected ? " selected" : "")}>{E(text)}</option>");
        }
        output.Content.SetHtmlContent(sb.ToString());
    }
}

/// <summary>A colour button: the icon with a colour bar under it, and a small picker arrow.</summary>
[HtmlTargetElement("vk-ribbon-color", TagStructure = TagStructure.WithoutEndTag)]
public sealed class VkRibbonColorTagHelper : TagHelper
{
    public string Cmd { get; set; } = "";
    public string Icon { get; set; } = "font";
    public string Value { get; set; } = "#C24A1C";
    public string? Tooltip { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", "vk-rcolor");
        output.Attributes.SetAttribute("style", $"--swatch:{Value}");
        output.Content.SetHtmlContent($"""
            <button type="button" class="vk-rb vk-rb-icon" data-cmd="{E(Cmd)}" data-arg="{E(Value)}" title="{E(Tooltip ?? Cmd)}"><i class="{Fa(Icon)}"></i><b class="vk-swatch-bar"></b></button>
            <label class="vk-rcolor-pick" title="Pick a colour"><i class="fa fa-caret-down"></i><input type="color" value="{E(Value)}" data-color-for="{E(Cmd)}" /></label>
            """);
    }
}

/// <summary>A thin line between buttons in a group.</summary>
[HtmlTargetElement("vk-ribbon-sep", TagStructure = TagStructure.WithoutEndTag)]
public sealed class VkRibbonSepTagHelper : TagHelper
{
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", "vk-rsep");
    }
}
