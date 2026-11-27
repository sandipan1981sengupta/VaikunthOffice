using System.Text.Json;
using Vaikunth.DtoLibrary;
using Vaikunth.ServiceLibrary;
using Vaikunth.UI.Controllers;
using Vaikunth.UI.Infrastructure;

namespace Vaikunth.AksharaPages.Web.Controllers;

/// <summary>Akshara Pages: open, create and save documents. Open/New/Save live in EditorControllerBase.</summary>
public sealed class EditorController : EditorControllerBase
{
    public EditorController(IDocumentService documents, CurrentApp current) : base(documents, current) { }

    protected override string BlankContent() =>
        JsonSerializer.Serialize(new PagesContent { Html = "<h1>Untitled document</h1><p>Start writing here.</p>" }, JsonSerializerOptions.Web);
}
