using System.Text.Json;
using Vaikunth.DtoLibrary;
using Vaikunth.ServiceLibrary;
using Vaikunth.UI.Controllers;
using Vaikunth.UI.Infrastructure;

namespace Vaikunth.HrishikeshaWorkbook.Web.Controllers;

/// <summary>Hrishikesha Workbook: open, create and save workbooks. Open/New/Save live in EditorControllerBase.</summary>
public sealed class EditorController : EditorControllerBase
{
    public EditorController(IDocumentService documents, CurrentApp current) : base(documents, current) { }

    protected override string BlankContent() =>
        JsonSerializer.Serialize(new WorkbookContent { Sheets = [new SheetDto { Name = "Sheet1" }] }, JsonSerializerOptions.Web);
}
