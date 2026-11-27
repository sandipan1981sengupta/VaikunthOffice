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
