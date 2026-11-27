// Ported from ShankhNaad (Template 1 controls). Same markup, CSS and JS; Vaikunth colours come from css/Theme/vaikunth-controls.css.
#nullable disable
using Microsoft.AspNetCore.Razor.TagHelpers;
using System.Text.Json;

namespace Vaikunth.UI.TagHelpers.Modern
{
    public class TabItem
    {
        public string Id { get; set; }                       // required — this is what gets passed to the "tabcontrol:select" JS event, so host code can branch on which tab was clicked
        public string Text { get; set; }                      // label shown on the tab, e.g. "CA/DA Login"
        public bool Active { get; set; } = false;             // true = this tab starts selected. If no tab has Active="true", the first tab is used.
        public bool Disabled { get; set; } = false;           // true = shown but not clickable
        public string TextColor { get; set; }                  // optional per-tab override of the inactive text color
        public string ActiveTextColor { get; set; }             // optional per-tab override of the active-state text color
        public string ActiveBackgroundColor { get; set; }        // optional per-tab override of the active-state pill color (e.g. make one tab red when selected)
        public string ActiveBorderWidth { get; set; }             // optional per-tab override of the active-state border width
        public string ActiveBorderColor { get; set; }              // optional per-tab override of the active-state border color
        public string Icon { get; set; }                          // optional icon class shown before the text, e.g. "bi bi-telephone-x"
        public string Badge { get; set; }                         // optional small count after the text, e.g. "5"
        public string BadgeKind { get; set; }                     // optional badge look: "warn" (amber) — default is a quiet grey
    }

    /// <summary>
    /// Rounded pill-style tab strip (role switcher, section nav, etc.) —
    /// see tab-control.css for the look this ships with by default.
    ///
    /// This is a purely CLIENT-SIDE control: it just tracks which tab is
    /// active, restyles it, and fires a "tabcontrol:select" event (and
    /// updates a hidden input, if Name is set) with the clicked tab's Id.
    /// It does not show/hide any content itself — wire that up in your own
    /// handler:
    ///
    ///   $("#roleTabs").on("tabcontrol:select", function (e, tabId, tab) {
    ///       if (tabId === "admin") { ... }
    ///   });
    /// </summary>
    [HtmlTargetElement("tab-control")]
    [RestrictChildren("tab")]
    public class TabControl : TagHelper
    {
        public string Id { get; set; }
        public string Name { get; set; }                // optional hidden input name — posts the active tab's Id on form submit; omit if you only need the JS event

        // Style knobs — all optional. Unset ones fall back to the dark
        // pill theme defined in tab-control.css, which matches the
        // reference design out of the box.
        public string Background { get; set; }               // any valid CSS "background" value (color, gradient...) for the outer rail
        public string ActiveBackgroundColor { get; set; }      // background of the active tab's pill (global default; a <tab> can override this itself)
        public string TextColor { get; set; }                   // default (inactive) tab text color
        public string ActiveTextColor { get; set; }              // active tab text color
        public string FontSize { get; set; }
        public string FontWeight { get; set; }                     // inactive tab font weight
        public string ActiveFontWeight { get; set; }
        public string Height { get; set; }                           // e.g. "52px"
        public string Width { get; set; }                             // e.g. "100%" — default is auto (hugs its content)
        public string BorderRadius { get; set; }                       // default is a full pill; set e.g. "8px" for a rounded-rectangle look instead
        public string Gap { get; set; }                                  // space between tabs
        public string TransitionDuration { get; set; }                    // e.g. "0.25s" — how long the sliding indicator takes to move between tabs; default 0.25s
        public string ActiveBorderWidth { get; set; }                      // e.g. "2px" — border around the active tab's pill; default 1px
        public string ActiveBorderColor { get; set; }                       // e.g. "#ffffff" or "rgba(255,255,255,0.4)" — default a subtle translucent white; a <tab> can override this itself
        public string BorderWidth { get; set; }                              // border around the WHOLE control (the outer rail), not the active pill; default 1px, invisible until BorderColor is set
        public string BorderColor { get; set; }                               // e.g. "#334155" — set this (BorderWidth is optional) to give the whole control an outline

        // Fallback: still supports setting tabs from code-behind if no <tab> children used
        public IEnumerable<TabItem> Tabs { get; set; }

        public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
        {
            // Collect tabs declared as <tab> children (same trick ModernTable uses for <column>)
            var declaredTabs = new List<TabItem>();
            context.Items[typeof(TabControl)] = declaredTabs;

            await output.GetChildContentAsync();

            var tabs = declaredTabs.Count > 0
                ? declaredTabs
                : (Tabs ?? Enumerable.Empty<TabItem>()).ToList();

            // If nobody flagged a tab Active="true", the first tab is the default
            if (tabs.Count > 0 && !tabs.Any(t => t.Active))
            {
                tabs[0].Active = true;
            }

            output.TagName = "div";
            output.Attributes.SetAttribute("class", "tab-control-wrapper");
            output.Attributes.SetAttribute("id", Id);

            var styleParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Background))
                styleParts.Add($"--tc-bg: {Background};");
            if (!string.IsNullOrWhiteSpace(ActiveBackgroundColor))
                styleParts.Add($"--tc-active-bg: {ActiveBackgroundColor};");
            if (!string.IsNullOrWhiteSpace(TextColor))
                styleParts.Add($"--tc-text-color: {TextColor};");
            if (!string.IsNullOrWhiteSpace(ActiveTextColor))
                styleParts.Add($"--tc-active-text-color: {ActiveTextColor};");
            if (!string.IsNullOrWhiteSpace(FontSize))
                styleParts.Add($"--tc-font-size: {FontSize};");
            if (!string.IsNullOrWhiteSpace(FontWeight))
                styleParts.Add($"--tc-font-weight: {FontWeight};");
            if (!string.IsNullOrWhiteSpace(ActiveFontWeight))
                styleParts.Add($"--tc-active-font-weight: {ActiveFontWeight};");
            if (!string.IsNullOrWhiteSpace(Height))
                styleParts.Add($"--tc-height: {Height};");
            if (!string.IsNullOrWhiteSpace(Width))
                styleParts.Add($"--tc-width: {Width};");
            if (!string.IsNullOrWhiteSpace(BorderRadius))
                styleParts.Add($"--tc-radius: {BorderRadius};");
            if (!string.IsNullOrWhiteSpace(Gap))
                styleParts.Add($"--tc-gap: {Gap};");
            if (!string.IsNullOrWhiteSpace(TransitionDuration))
                styleParts.Add($"--tc-transition-duration: {TransitionDuration};");
            if (!string.IsNullOrWhiteSpace(ActiveBorderWidth))
                styleParts.Add($"--tc-active-border-width: {ActiveBorderWidth};");
            if (!string.IsNullOrWhiteSpace(ActiveBorderColor))
                styleParts.Add($"--tc-active-border-color: {ActiveBorderColor};");
            if (!string.IsNullOrWhiteSpace(BorderWidth))
                styleParts.Add($"--tc-border-width: {BorderWidth};");
            if (!string.IsNullOrWhiteSpace(BorderColor))
                styleParts.Add($"--tc-border-color: {BorderColor};");

            if (styleParts.Count > 0)
            {
                output.Attributes.SetAttribute("style", string.Join(" ", styleParts));
            }

            // Same reasoning as ModernTable: JS reads camelCase (tab.id,
            // tab.text, tab.activeBackgroundColor, ...), so this must match.
            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var tabsJson = JsonSerializer.Serialize(tabs, jsonOptions);
            var activeTab = tabs.FirstOrDefault(t => t.Active);

            output.Content.SetHtmlContent($@"
                <div class='tc-tabs'></div>
                <input type='hidden' id='{Id}_selected' name='{Name}' value='{activeTab?.Id}' />
                <script type='application/json' class='tc-tabs-data'>{tabsJson}</script>
            ");
        }
    }

    // Child element: <tab id="..." text="..." active="true" text-color="..." active-text-color="..." active-background-color="..." active-border-width="..." active-border-color="..." disabled="false"></tab>
    [HtmlTargetElement("tab", ParentTag = "tab-control")]
    public class TabControlTab : TagHelper
    {
        public string Id { get; set; }
        public string Text { get; set; }
        public bool Active { get; set; } = false;
        public bool Disabled { get; set; } = false;
        public string TextColor { get; set; }
        public string ActiveTextColor { get; set; }
        public string ActiveBackgroundColor { get; set; }
        public string ActiveBorderWidth { get; set; }
        public string ActiveBorderColor { get; set; }
        public string Icon { get; set; }
        public string Badge { get; set; }
        public string BadgeKind { get; set; }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            var tabs = (List<TabItem>)context.Items[typeof(TabControl)];

            tabs.Add(new TabItem
            {
                Id = Id,
                Text = Text,
                Active = Active,
                Disabled = Disabled,
                TextColor = TextColor,
                ActiveTextColor = ActiveTextColor,
                ActiveBackgroundColor = ActiveBackgroundColor,
                ActiveBorderWidth = ActiveBorderWidth,
                ActiveBorderColor = ActiveBorderColor,
                Icon = Icon,
                Badge = Badge,
                BadgeKind = BadgeKind
            });

            output.SuppressOutput();   // <tab> never renders any actual HTML
        }
    }
}