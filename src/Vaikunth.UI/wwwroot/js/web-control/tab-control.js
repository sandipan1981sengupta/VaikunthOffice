// ===== Tab Control =====
// Purely a client-side control: renders a pill-style tab strip from the
// embedded JSON, tracks which tab is active, slides a single indicator
// element to it, and fires a "tabcontrol:select" event with the clicked
// tab's id (and the full tab object) so host page JS can decide what
// happens next. It does not show/hide any content itself — wire that up
// in your own handler:
//
//   $("#roleTabs").on("tabcontrol:select", function (e, tabId, tab) {
//       if (tabId === "admin") { ... }
//   });
//
// Auto-initializes every .tab-control-wrapper on the page.

var requestFrame = (typeof window !== "undefined" && window.requestAnimationFrame)
    ? window.requestAnimationFrame.bind(window)
    : function (cb) { return setTimeout(cb, 16); };

$(document).ready(function () {

    // window.TabControl.init(context): set up tab strips in a part of the page loaded later
    // (ShankhNaad loads pages partially); each strip is set up only once.
    window.TabControl = window.TabControl || {};
    window.TabControl.init = function (context) {
        $(context || document).find(".tab-control-wrapper").addBack(".tab-control-wrapper").each(function () {
            var $w = $(this);
            if ($w.data("tc-inited")) return;
            $w.data("tc-inited", true);
            initTabControl($w);
        });
    };
    window.TabControl.init();

    function initTabControl($wrapper) {
        var $tabsContainer = $wrapper.find(".tc-tabs");
        var $hiddenInput = $wrapper.find("input[type='hidden']");
        var $indicator = $("<div class='tc-indicator'></div>");

        var dataEl = $wrapper.find("script.tc-tabs-data")[0];
        var tabs = [];
        try {
            tabs = dataEl ? JSON.parse(dataEl.textContent || "[]") : [];
        } catch (e) {
            tabs = [];
        }

        // Builds the tab buttons ONCE. Re-selecting a tab afterwards only
        // toggles classes/styles on these same elements (see
        // updateActiveState) rather than tearing them down — the sliding
        // indicator is one persistent element, and it can only animate
        // between positions if it isn't destroyed and recreated on every
        // click.
        function buildTabs() {
            $tabsContainer.empty().append($indicator);

            tabs.forEach(function (tab) {
                var $tab = $("<button>")
                    .attr("type", "button")
                    .addClass("tc-tab")
                    .attr("data-tab-id", tab.id);
                // optional icon before the text and a small count after it
                if (tab.icon) $tab.append($("<i>").addClass("tc-ic " + tab.icon).attr("aria-hidden", "true"));
                $tab.append($("<span>").addClass("tc-label").text(tab.text != null ? tab.text : ""));
                if (tab.badge != null && tab.badge !== "") $tab.append($("<span>").addClass("tc-badge" + (tab.badgeKind ? " " + tab.badgeKind : "")).text(tab.badge));

                if (tab.disabled) {
                    $tab.addClass("disabled").attr("aria-disabled", "true");
                }

                $tab.on("click", function () {
                    if (tab.disabled) return;
                    selectTab(tab.id);
                });

                $tabsContainer.append($tab);
            });
        }

        // Moves the (already-existing) indicator to sit behind the given
        // tab button. Reads the button's actual rendered position/width so
        // it lines up regardless of each tab's text length.
        function moveIndicatorTo($tab, tabData) {
            if (!$tab || !$tab.length) {
                $indicator.css("width", 0);
                return;
            }
            $indicator.css({
                left: $tab[0].offsetLeft + "px",
                width: $tab[0].offsetWidth + "px",
                background: (tabData && tabData.activeBackgroundColor) || "",     // "" clears any previous override, falling back to the CSS default
                borderWidth: (tabData && tabData.activeBorderWidth) || "",
                borderColor: (tabData && tabData.activeBorderColor) || ""
            });
        }

        // Applies active/inactive classes and per-tab color overrides to
        // the existing buttons, then slides the indicator to the active
        // one. Called on init and after every selection change.
        function updateActiveState() {
            var $activeTab = null;
            var activeTabData = null;

            $tabsContainer.find(".tc-tab").each(function () {
                var $t = $(this);
                var tabData = tabs.filter(function (t) { return t.id === $t.attr("data-tab-id"); })[0];
                if (!tabData) return;

                $t.toggleClass("active", !!tabData.active);
                $t.css("color", tabData.active ? (tabData.activeTextColor || "") : (tabData.textColor || ""));

                if (tabData.active) {
                    $activeTab = $t;
                    activeTabData = tabData;
                }
            });

            moveIndicatorTo($activeTab, activeTabData);
        }

        function selectTab(tabId) {
            var selected = null;
            tabs.forEach(function (t) {
                t.active = (t.id === tabId);
                if (t.active) selected = t;
            });
            if (!selected) return;   // unknown id — ignore rather than clobber the current selection

            if ($hiddenInput.length) $hiddenInput.val(tabId).trigger("change");

            updateActiveState();

            $wrapper.trigger("tabcontrol:select", [tabId, selected]);
        }

        buildTabs();
        updateActiveState();   // places the indicator instantly — no .tc-animated class yet

        // Turn on the sliding transition only AFTER the first placement, so
        // the indicator doesn't slide in from the top-left corner on page
        // load — only actual tab changes after this point animate.
        requestFrame(function () {
            $indicator.addClass("tc-animated");
        });

        // Keep the indicator aligned if tab widths change (e.g. responsive
        // reflow, or text wrapping at a narrow viewport).
        $(window).on("resize", function () {
            moveIndicatorTo($tabsContainer.find(".tc-tab.active").first(), tabs.filter(function (t) { return t.active; })[0]);
        });

        // Expose a small per-instance API, same pattern as modern-table's mt-api
        $wrapper.data("tc-api", {
            select: selectTab,
            getActive: function () {
                var active = tabs.filter(function (t) { return t.active; });
                return active.length ? active[0] : null;
            },
            getTabs: function () {
                return tabs;
            }
        });
    }

});