namespace Vaikunth.DtoLibrary;

/// <summary>One file in any of the three apps (a page document, a workbook or a slide deck).</summary>
public sealed record DocumentDto(
    Guid DocumentId,
    SuiteApp App,
    string Title,
    /// <summary>The body as JSON or HTML, in the shape each app expects (see the three *Content classes).</summary>
    string Content,
    Guid OwnerId,
    string OwnerName,
    DateTime CreatedUtc,
    DateTime ModifiedUtc,
    bool IsStarred);

/// <summary>A row of the "Recent" list: no content, so it stays light.</summary>
public sealed record DocumentSummaryDto(Guid DocumentId, SuiteApp App, string Title, string OwnerName, DateTime ModifiedUtc, bool IsStarred);

public sealed class DocumentSave
{
    /// <summary>Empty for a new file.</summary>
    public Guid? DocumentId { get; set; }
    public SuiteApp App { get; set; }
    public string Title { get; set; } = "Untitled";
    public string Content { get; set; } = "";
}

/// <summary>Akshara Pages: the page body is stored as sanitised HTML.</summary>
public sealed class PagesContent
{
    public string Html { get; set; } = "";
    public string PageSize { get; set; } = "A4";
}

/// <summary>Hrishikesha Workbook: sheets of sparse cells. A cell holds what was typed (a value or a =formula).</summary>
public sealed class WorkbookContent
{
    public List<SheetDto> Sheets { get; set; } = new();
}

public sealed class SheetDto
{
    public string Name { get; set; } = "Sheet1";
    public int Rows { get; set; } = 40;
    public int Cols { get; set; } = 14;
    /// <summary>Key is the address, e.g. "B3".</summary>
    public Dictionary<string, CellDto> Cells { get; set; } = new();
}

public sealed class CellDto
{
    public string Input { get; set; } = "";
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public string? Fill { get; set; }
    public string? Color { get; set; }
    public string? Align { get; set; }
    /// <summary>general | number | currency | percent</summary>
    public string? Format { get; set; }
}

/// <summary>Vishwaroopa Slides: a deck of slides, each with positioned boxes.</summary>
public sealed class SlidesContent
{
    public string Theme { get; set; } = "sunrise";
    public List<SlideDto> Slides { get; set; } = new();
}

public sealed class SlideDto
{
    public string Layout { get; set; } = "title";
    public string? Background { get; set; }
    public string Notes { get; set; } = "";
    public List<SlideBoxDto> Boxes { get; set; } = new();
}

public sealed class SlideBoxDto
{
    /// <summary>text | shape</summary>
    public string Kind { get; set; } = "text";
    /// <summary>Position and size in percent of the slide, so it scales to any screen.</summary>
    public double X { get; set; }
    public double Y { get; set; }
    public double W { get; set; }
    public double H { get; set; }
    public string Html { get; set; } = "";
    public double FontSize { get; set; } = 24;
    public string? Color { get; set; }
    public string? Fill { get; set; }
    public string? Role { get; set; }
}

/// <summary>Shashvata Reader: one PDF (kept as base64 inside the file on the server) with the reader's notes and bookmarks.</summary>
public sealed class ReaderContent
{
    /// <summary>The original file name, e.g. "Annual report.pdf".</summary>
    public string? FileName { get; set; }
    /// <summary>The PDF itself, base64. Null until a PDF is opened into this file.</summary>
    public string? Pdf { get; set; }
    public long SizeBytes { get; set; }
    public int Pages { get; set; }
    /// <summary>Where the reader left off.</summary>
    public int LastPage { get; set; } = 1;
    public int Zoom { get; set; } = 100;
    public List<int> Bookmarks { get; set; } = new();
    public List<ReaderNoteDto> Notes { get; set; } = new();
}

public sealed class ReaderNoteDto
{
    public string Id { get; set; } = "";
    public int Page { get; set; } = 1;
    public string Text { get; set; } = "";
    /// <summary>gold | green | blue | pink</summary>
    public string Color { get; set; } = "gold";
    public string? By { get; set; }
    public DateTime? At { get; set; }
}

/// <summary>Dhruva Planner: one project with its tasks (board, list and timeline are views of the same tasks).</summary>
public sealed class PlannerContent
{
    public string Description { get; set; } = "";
    public DateOnly? Start { get; set; }
    public DateOnly? End { get; set; }
    /// <summary>The board's columns, in order. Each task's Status is one of their keys.</summary>
    public List<PlannerColumnDto> Columns { get; set; } = PlannerColumnDto.Defaults();
    public List<PlannerTaskDto> Tasks { get; set; } = new();
}

public sealed class PlannerColumnDto
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    public static List<PlannerColumnDto> Defaults() =>
    [
        new() { Key = "todo", Name = "To do" }, new() { Key = "doing", Name = "In progress" },
        new() { Key = "review", Name = "Review" }, new() { Key = "done", Name = "Done" }
    ];
}

public sealed class PlannerTaskDto
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Status { get; set; } = "todo";
    /// <summary>low | medium | high</summary>
    public string Priority { get; set; } = "medium";
    public string Owner { get; set; } = "";
    public DateOnly? Start { get; set; }
    public DateOnly? Due { get; set; }
    /// <summary>0 to 100.</summary>
    public int Progress { get; set; }
    public bool Milestone { get; set; }
    public string Notes { get; set; } = "";
}
