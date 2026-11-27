// ===== Modern Bar Chart (horizontal bars) =====
// Auto-initializes every .modern-bar-chart-canvas on the page.
// Requires Chart.js: <script src="https://cdn.jsdelivr.net/npm/chart.js@4"></script>

$(document).ready(function () {

    if (typeof Chart === "undefined") {
        console.warn("Modern Bar Chart: Chart.js is not loaded.");
        return;
    }

    var chartInstances = {};

    // Custom plugin — draws a dashed HORIZONTAL guide line that tracks the
    // mouse cursor's y-position. Horizontal (not vertical, unlike the other
    // charts) because this chart's categories run top-to-bottom along the
    // y-axis, not left-to-right — the guide line needs to match that.
    Chart.register({
        id: "modernBarCrosshair",
        afterDraw: function (chart) {
            var pluginOpts = chart.options.plugins && chart.options.plugins.modernBarCrosshair;
            if (!pluginOpts || !pluginOpts.enabled) return;

            var activeElements = chart.getActiveElements();
            if (!activeElements || activeElements.length === 0) return;

            var y = activeElements[0].element.y;
            var chartArea = chart.chartArea;

            var ctx = chart.ctx;
            ctx.save();
            ctx.beginPath();
            ctx.moveTo(chartArea.left, y);
            ctx.lineTo(chartArea.right, y);
            ctx.lineWidth = 1;
            ctx.strokeStyle = "rgba(100, 116, 139, 0.6)";
            ctx.setLineDash([4, 4]);
            ctx.stroke();
            ctx.restore();
        }
    });

    // Custom plugin — draws each bar's value as text inside the bar, near
    // its right (end) edge, matching this chart's horizontal orientation.
    Chart.register({
        id: "modernBarDataLabels",
        afterDatasetsDraw: function (chart) {
            var pluginOpts = chart.options.plugins && chart.options.plugins.modernBarDataLabels;
            if (!pluginOpts || !pluginOpts.enabled) return;

            var ctx = chart.ctx;
            ctx.save();
            ctx.font = "bold 12px sans-serif";
            ctx.fillStyle = "#ffffff";
            ctx.textAlign = "right";
            ctx.textBaseline = "middle";

            chart.data.datasets.forEach(function (dataset, datasetIndex) {
                var meta = chart.getDatasetMeta(datasetIndex);
                if (meta.hidden) return;

                meta.data.forEach(function (bar, index) {
                    var value = dataset.data[index];
                    if (value === null || value === undefined) return;

                    var pos = bar.tooltipPosition();
                    // 6px padding from the bar's right edge, staying inside it
                    ctx.fillText(value, pos.x - 6, pos.y);
                });
            });

            ctx.restore();
        }
    });

    function getAccentColor() {
        var value = getComputedStyle(document.documentElement).getPropertyValue("--ctrl-accent");
        return value ? value.trim() : "#5b7a9d";
    }

    function generatePalette(count) {
        var basePalette = [
            "#5b7a9d", "#0e93a8", "#2f8f5b", "#7451a8", "#d97706",
            "#dc2626", "#7a97b8", "#46617f", "#94a3b8", "#334155"
        ];
        var result = [];
        for (var i = 0; i < count; i++) {
            result.push(basePalette[i % basePalette.length]);
        }
        return result;
    }

    function darkenColor(hex, percent) {
        hex = hex.replace("#", "");
        if (hex.length === 3) {
            hex = hex.split("").map(function (c) { return c + c; }).join("");
        }
        var num = parseInt(hex, 16);
        var r = Math.max(0, Math.floor((num >> 16) * (1 - percent)));
        var g = Math.max(0, Math.floor(((num >> 8) & 0x00FF) * (1 - percent)));
        var b = Math.max(0, Math.floor((num & 0x0000FF) * (1 - percent)));
        return "rgb(" + r + "," + g + "," + b + ")";
    }

    function lightenColor(hex, percent) {
        hex = hex.replace("#", "");
        if (hex.length === 3) {
            hex = hex.split("").map(function (c) { return c + c; }).join("");
        }
        var num = parseInt(hex, 16);
        var r0 = num >> 16, g0 = (num >> 8) & 0x00FF, b0 = num & 0x0000FF;
        var r = Math.min(255, Math.floor(r0 + (255 - r0) * percent));
        var g = Math.min(255, Math.floor(g0 + (255 - g0) * percent));
        var b = Math.min(255, Math.floor(b0 + (255 - b0) * percent));
        return "rgb(" + r + "," + g + "," + b + ")";
    }

    // Returns a Chart.js "scriptable" background function. false = horizontal
    // gradient (left = lighter, right = full color), matching how bars extend
    // left-to-right in this horizontal chart.
    function makeGradientBackground(baseColorOrArray, vertical) {
        return function (context) {
            var chart = context.chart;
            var chartArea = chart.chartArea;
            if (!chartArea) return "transparent";

            var baseColor = Array.isArray(baseColorOrArray)
                ? baseColorOrArray[context.dataIndex % baseColorOrArray.length]
                : baseColorOrArray;

            var gradient = vertical
                ? chart.ctx.createLinearGradient(0, chartArea.top, 0, chartArea.bottom)
                : chart.ctx.createLinearGradient(chartArea.left, 0, chartArea.right, 0);

            gradient.addColorStop(0, lightenColor(baseColor, 0.18));   // CHANGED — was 0.4; darker overall gradient
            gradient.addColorStop(1, baseColor);
            return gradient;
        };
    }

    // Single-series config (backward compatible with the original version)
    function buildConfig(labels, values, datasetLabel, customColors, showDataLabels, showCrosshair) {
        var hasCustomColors = customColors && customColors.length > 0;
        var baseColor = hasCustomColors ? customColors : getAccentColor();
        var hoverColor = hasCustomColors
            ? customColors.map(function (c) { return darkenColor(c, 0.18); })
            : darkenColor(getAccentColor(), 0.18);

        return {
            type: "bar",
            data: {
                labels: labels,
                datasets: [{
                    label: datasetLabel,
                    data: values,
                    backgroundColor: makeGradientBackground(baseColor, false),
                    hoverBackgroundColor: hoverColor,
                    borderColor: hoverColor,
                    borderWidth: 2,
                    borderRadius: 6,
                    maxBarThickness: 48
                }]
            },
            options: {
                indexAxis: "y",
                responsive: true,
                maintainAspectRatio: false,
                interaction: showCrosshair
                    ? { mode: "index", intersect: false }
                    : { mode: "nearest", intersect: true },
                animation: {
                    duration: 900,
                    easing: "easeOutQuart",
                    delay: function (context) {
                        return context.type === "data" ? context.dataIndex * 80 : 0;
                    }
                },
                transitions: {
                    active: { animation: { duration: 250 } }
                },
                plugins: {
                    legend: { display: false },
                    modernBarDataLabels: { enabled: showDataLabels },
                    modernBarCrosshair: { enabled: showCrosshair },
                    tooltip: {
                        titleFont: { size: 16, weight: "bold" },
                        bodyFont: { size: 15 },
                        padding: 14,
                        cornerRadius: 8,
                        displayColors: true,
                        boxPadding: 6
                    }
                },
                scales: {
                    x: { beginAtZero: true, ticks: { font: { size: 13, weight: "bold" } } },
                    y: { ticks: { font: { size: 13, weight: "bold" } } }
                }
            }
        };
    }

    // Multi-series config — one grouped bar cluster per label, one color per series
    function buildMultiSeriesConfig(labels, seriesArray, showDataLabels, showCrosshair) {
        var palette = generatePalette(seriesArray.length);

        var datasets = seriesArray.map(function (s, i) {
            var color = s.color || palette[i];
            return {
                label: s.label,
                data: s.data,
                backgroundColor: makeGradientBackground(color, false),
                hoverBackgroundColor: darkenColor(color, 0.18),
                borderColor: darkenColor(color, 0.18),
                borderWidth: 2,
                borderRadius: 6,
                maxBarThickness: 40
            };
        });

        return {
            type: "bar",
            data: { labels: labels, datasets: datasets },
            options: {
                indexAxis: "y",
                responsive: true,
                maintainAspectRatio: false,
                interaction: showCrosshair
                    ? { mode: "index", intersect: false }
                    : { mode: "nearest", intersect: true },
                animation: {
                    duration: 900,
                    easing: "easeOutQuart",
                    delay: function (context) {
                        return context.type === "data" ? context.dataIndex * 80 : 0;
                    }
                },
                transitions: {
                    active: { animation: { duration: 250 } }
                },
                plugins: {
                    legend: { display: false },   // replaced by the custom HTML legend box
                    modernBarDataLabels: { enabled: showDataLabels },
                    modernBarCrosshair: { enabled: showCrosshair },
                    tooltip: {
                        titleFont: { size: 16, weight: "bold" },
                        bodyFont: { size: 15 },
                        padding: 14,
                        cornerRadius: 8,
                        displayColors: true,
                        boxPadding: 6
                    }
                },
                scales: {
                    x: { beginAtZero: true, ticks: { font: { size: 13, weight: "bold" } } },
                    y: { ticks: { font: { size: 13, weight: "bold" } } }
                }
            }
        };
    }

    function renderCustomLegend(legendBoxId, seriesLabels, colors) {
        var $legendBox = $("#" + legendBoxId);
        if ($legendBox.length === 0) return;

        $legendBox.empty();
        seriesLabels.forEach(function (label, i) {
            var $swatch = $("<span class='mc-legend-swatch'></span>").css("background", colors[i % colors.length]);
            var $text = $("<span></span>").text(label);
            $("<div class='mc-legend-item'></div>").append($swatch, $text).appendTo($legendBox);
        });
    }

    $(".modern-bar-chart-canvas").each(function () {
        var $canvas = $(this);
        var id = $canvas.attr("id");
        var labels = [];

        try {
            labels = JSON.parse($canvas.attr("data-labels") || "[]");
        } catch (e) {
            console.warn("Modern Bar Chart: invalid labels for", id, e);
        }

        var seriesRaw = $canvas.attr("data-series");
        var showDataLabels = $canvas.attr("data-show-data-labels") === "true";
        var showCrosshair = $canvas.attr("data-show-crosshair") === "true";

        if (seriesRaw) {
            var seriesArray = [];
            try {
                seriesArray = JSON.parse(seriesRaw);
            } catch (e) {
                console.warn("Modern Bar Chart: invalid series JSON for", id, e);
            }
            chartInstances[id] = new Chart($canvas[0], buildMultiSeriesConfig(labels, seriesArray, showDataLabels, showCrosshair));

            var palette = generatePalette(seriesArray.length);
            var seriesColors = seriesArray.map(function (s, i) { return s.color || palette[i]; });
            var seriesLabels = seriesArray.map(function (s) { return s.label; });
            renderCustomLegend(id + "_legend", seriesLabels, seriesColors);
        } else {
            var values = [];
            var customColors = [];
            try {
                values = JSON.parse($canvas.attr("data-values") || "[]");
                customColors = JSON.parse($canvas.attr("data-colors") || "[]");
            } catch (e) {
                console.warn("Modern Bar Chart: invalid data attributes for", id, e);
            }
            var datasetLabel = $canvas.attr("data-dataset-label") || "Value";
            chartInstances[id] = new Chart($canvas[0], buildConfig(labels, values, datasetLabel, customColors, showDataLabels, showCrosshair));

            // FIXED — legend now populates for single-series too. Per-category
            // if custom colors were set (colors distinguish categories here,
            // not series), otherwise one entry for the whole dataset.
            if (customColors && customColors.length > 0) {
                renderCustomLegend(id + "_legend", labels, customColors);
            } else {
                renderCustomLegend(id + "_legend", [datasetLabel], [getAccentColor()]);
            }
        }
    });

    // Usage: setModernBarChartData("#DeptChart", ["Eng","Sales"], [45, 32]);
    //        setModernBarChartData("#DeptChart", ["Eng","Sales"], [45, 32], ["#dc2626", "#16a34a"]);
    window.setModernBarChartData = function (chartSelector, labels, values, colors) {
        var id = chartSelector.replace("#", "");
        var chart = chartInstances[id];
        if (!chart) return;

        var datasetLabel = chart.data.datasets[0] ? chart.data.datasets[0].label : "Value";
        var barColor = (colors && colors.length > 0) ? colors : getAccentColor();
        var borderColorValue = Array.isArray(barColor)
            ? barColor.map(function (c) { return darkenColor(c, 0.18); })
            : darkenColor(barColor, 0.18);

        chart.data.labels = labels;
        chart.data.datasets = [{
            label: datasetLabel,
            data: values,
            backgroundColor: makeGradientBackground(barColor, false),
            borderColor: borderColorValue,
            borderWidth: 2,
            borderRadius: 6,
            maxBarThickness: 48
        }];
        chart.update();

        if (colors && colors.length > 0) {
            renderCustomLegend(id + "_legend", labels, colors);
        } else {
            renderCustomLegend(id + "_legend", [datasetLabel], [getAccentColor()]);
        }
    };

    // Usage: setModernBarChartMultiSeries("#DeptChart", ["Eng","Sales","HR"], [
    //            { label: "2025", data: [40, 28, 10] },
    //            { label: "2026", data: [45, 32, 12], color: "#dc2626" }
    //        ]);
    // Supports as many series as you need — 4, 5, or more.
    window.setModernBarChartMultiSeries = function (chartSelector, labels, seriesArray) {
        var id = chartSelector.replace("#", "");
        var chart = chartInstances[id];
        if (!chart) return;

        var palette = generatePalette(seriesArray.length);
        chart.data.labels = labels;
        chart.data.datasets = seriesArray.map(function (s, i) {
            var color = s.color || palette[i];
            return {
                label: s.label,
                data: s.data,
                backgroundColor: makeGradientBackground(color, false),
                hoverBackgroundColor: darkenColor(color, 0.18),
                borderColor: darkenColor(color, 0.18),
                borderWidth: 2,
                borderRadius: 6,
                maxBarThickness: 40
            };
        });
        chart.update();

        var seriesColors = seriesArray.map(function (s, i) { return s.color || palette[i]; });
        var seriesLabels = seriesArray.map(function (s) { return s.label; });
        renderCustomLegend(id + "_legend", seriesLabels, seriesColors);
    };

    // Usage: getModernBarChartData("#DeptChart") -> { labels: [...], series: [{ label, data }, ...] }
    window.getModernBarChartData = function (chartSelector) {
        var id = chartSelector.replace("#", "");
        var chart = chartInstances[id];
        if (!chart) return { labels: [], series: [] };
        return {
            labels: chart.data.labels,
            series: chart.data.datasets.map(function (d) { return { label: d.label, data: d.data }; })
        };
    };

    // Usage (single series): setModernBarChartDataSource("#DeptChart", myData, "dept", ["headcount"]);
    //   where myData = [{ dept: "Engineering", headcount: 45 }, { dept: "Sales", headcount: 32 }, ...]
    // Usage (multi-series):  setModernBarChartDataSource("#DeptChart", myData, "dept", ["2025", "2026"]);
    //   where myData = [{ dept: "Engineering", "2025": 40, "2026": 45 }, ...]
    window.setModernBarChartDataSource = function (chartSelector, dataSource, labelField, seriesFields, colors) {
        var labels = dataSource.map(function (item) { return item[labelField]; });

        if (seriesFields.length === 1) {
            var values = dataSource.map(function (item) { return item[seriesFields[0]]; });
            window.setModernBarChartData(chartSelector, labels, values, colors);
        } else {
            var seriesArray = seriesFields.map(function (field, i) {
                var series = { label: field, data: dataSource.map(function (item) { return item[field]; }) };
                if (colors && colors[i]) {
                    series.color = colors[i];   // FIXED — colors was previously ignored here entirely
                }
                return series;
            });
            window.setModernBarChartMultiSeries(chartSelector, labels, seriesArray);
        }
    };

    // Usage: clearModernBarChart("#DeptChart")
    // Resets to a single empty series, clearing any multi-series state too.
    window.clearModernBarChart = function (chartSelector) {
        var id = chartSelector.replace("#", "");
        var chart = chartInstances[id];
        if (!chart) return;

        chart.data.labels = [];
        chart.data.datasets = [{
            label: "Value",
            data: [],
            backgroundColor: getAccentColor(),
            borderColor: darkenColor(getAccentColor(), 0.18),
            borderWidth: 2,
            borderRadius: 6,
            maxBarThickness: 48
        }];
        chart.update();

        renderCustomLegend(id + "_legend", [], []);
    };

});