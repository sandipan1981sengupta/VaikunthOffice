using System.Text.Json;
using Vaikunth.DtoLibrary;
using Vaikunth.ServiceLibrary;
using Vaikunth.UI.Controllers;
using Vaikunth.UI.Infrastructure;

namespace Vaikunth.AksharaPages.Controllers;

/// <summary>Akshara Pages: open, create and save documents. Open/New/Save live in EditorControllerBase.</summary>
[Microsoft.AspNetCore.Mvc.Area("pages")]
public sealed class EditorController : EditorControllerBase
{
    public EditorController(IDocumentService documents, CurrentApp current) : base(documents, current) { }

    protected override string BlankContent() =>
        JsonSerializer.Serialize(new PagesContent { Html = "<h1>Untitled document</h1><p>Start writing here.</p>" }, JsonSerializerOptions.Web);

    protected override async Task<ExportedFile?> ExportAsync(string content, string title, CancellationToken ct)
    {
        var doc = JsonSerializer.Deserialize<PagesContent>(content, JsonSerializerOptions.Web) ?? new PagesContent();
        return new ExportedFile(await Vaikunth.AksharaPages.Export.DocxExport.BuildAsync(doc, title, ct),
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document", ".docx");
    }
}
