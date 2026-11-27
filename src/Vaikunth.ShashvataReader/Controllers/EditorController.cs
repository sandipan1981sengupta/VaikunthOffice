using System.Text.Json;
using Vaikunth.DtoLibrary;
using Vaikunth.ServiceLibrary;
using Vaikunth.UI.Controllers;
using Vaikunth.UI.Infrastructure;

namespace Vaikunth.ShashvataReader.Controllers;

/// <summary>
/// Shashvata Reader: open a PDF into Vaikunth Office, read it, keep notes and bookmarks. Open/New/Save live in
/// EditorControllerBase. The PDF is drawn in the browser (pdf.js) and Download gives back the original PDF, so
/// there is nothing to build on the server.
/// </summary>
[Microsoft.AspNetCore.Mvc.Area("reader")]
public sealed class EditorController : EditorControllerBase
{
    public EditorController(IDocumentService documents, CurrentApp current) : base(documents, current) { }

    /// <summary>A new Reader file has no PDF yet: the page asks the person to pick or drop one.</summary>
    protected override string BlankContent() => JsonSerializer.Serialize(new ReaderContent(), JsonSerializerOptions.Web);
}
