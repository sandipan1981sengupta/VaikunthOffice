using System.Text.Encodings.Web;

namespace Vaikunth.UI.TagHelpers;

/// <summary>Small helpers shared by the tag helpers.</summary>
internal static class VkHtml
{
    public static string E(string? s) => HtmlEncoder.Default.Encode(s ?? "");

    /// <summary>"fa-bold" or "bold" → Font Awesome 4.7 classes.</summary>
    public static string Fa(string? icon) =>
        string.IsNullOrWhiteSpace(icon) ? "" : icon.StartsWith("fa ") ? icon : $"fa {(icon.StartsWith("fa-") ? icon : "fa-" + icon)}";

    public static string Cls(params string?[] parts) => string.Join(' ', parts.Where(p => !string.IsNullOrWhiteSpace(p)));
}
