using System.Text.Json;
using Vaikunth.DtoLibrary;
using Vaikunth.ServiceLibrary;
using Vaikunth.UI.Controllers;
using Vaikunth.UI.Infrastructure;

namespace Vaikunth.HrishikeshaWorkbook.Controllers;

/// <summary>Hrishikesha Workbook: open, create and save workbooks. Open/New/Save live in EditorControllerBase.</summary>
[Microsoft.AspNetCore.Mvc.Area("workbook")]
public sealed class EditorController : EditorControllerBase
{
    public EditorController(IDocumentService documents, CurrentApp current) : base(documents, current) { }

    protected override string BlankContent() =>
        JsonSerializer.Serialize(new WorkbookContent { Sheets = [new SheetDto { Name = "Sheet1" }] }, JsonSerializerOptions.Web);

    protected override Task<ExportedFile?> ExportAsync(string content, string title, CancellationToken ct)
    {
        var book = JsonSerializer.Deserialize<WorkbookContent>(content, JsonSerializerOptions.Web) ?? new WorkbookContent();
        return Task.FromResult<ExportedFile?>(new ExportedFile(Vaikunth.HrishikeshaWorkbook.Export.XlsxExport.Build(book, title),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ".xlsx"));
    }
}
