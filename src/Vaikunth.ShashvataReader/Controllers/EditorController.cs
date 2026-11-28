using System.Text.Json;
using Vaikunth.DtoLibrary;
using Vaikunth.ServiceLibrary;
using Vaikunth.UI.Controllers;
using Vaikunth.UI.Infrastructure;

namespace Vaikunth.ShashvataReader.Controllers;

/// <summary>
/// Shashvata Reader: open a PDF into Vaikunth Desk, read it, keep notes and bookmarks. Open/New/Save live in
/// EditorControllerBase. The PDF is drawn in the browser (pdf.js) and Download gives back the original PDF, so
/// there is nothing to build on the server.
/// </summary>
[Microsoft.AspNetCore.Mvc.Area("reader")]
public sealed class EditorController : EditorControllerBase
{
    public EditorController(IDocumentService documents, CurrentApp current) : base(documents, current) { }

    /// <summary>A new Reader file has no PDF yet: the page asks the person to pick or drop one.</summary>
    protected override string BlankContent() => JsonSerializer.Serialize(new ReaderContent(), JsonSerializerOptions.Web);

    /// <summary>Download from the home page: the original PDF, as it was opened.</summary>
    protected override Task<ExportedFile?> ExportAsync(string content, string title, CancellationToken ct)
    {
        var reader = JsonSerializer.Deserialize<ReaderContent>(content, JsonSerializerOptions.Web);
        if (string.IsNullOrEmpty(reader?.Pdf)) return Task.FromResult<ExportedFile?>(null);
        var b64 = reader.Pdf;
        var comma = b64.IndexOf(',');
        if (b64.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma > 0) b64 = b64[(comma + 1)..];
        return Task.FromResult<ExportedFile?>(new ExportedFile(Convert.FromBase64String(b64), "application/pdf", ".pdf"));
    }
}
