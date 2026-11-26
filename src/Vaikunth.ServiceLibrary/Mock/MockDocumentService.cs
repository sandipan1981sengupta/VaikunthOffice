using System.Collections.Concurrent;
using System.Text.Json;
using Vaikunth.DtoLibrary;

namespace Vaikunth.ServiceLibrary.Mock;

/// <summary>
/// Sample documents without SQL Server: the list (name, owner, dates) is in memory and lost on restart,
/// but the content is written to real files in the storage folder, the same as with SQL Server.
/// </summary>
public sealed class MockDocumentService : IDocumentService
{
    /// <summary>The list of files. Content is always "" here; it lives on disk.</summary>
    private static readonly ConcurrentDictionary<Guid, DocumentDto> Store = new();
    /// <summary>Which client each file belongs to (Vaikunth_Documents.TenantId).</summary>
    private static readonly ConcurrentDictionary<Guid, Guid> TenantOf = new();
    /// <summary>Size on disk of each file (Vaikunth_Documents.SizeBytes).</summary>
    private static readonly ConcurrentDictionary<Guid, long> SizeOf = new();
    /// <summary>Content of the sample files, written to disk the first time each one is opened.</summary>
    private static readonly ConcurrentDictionary<Guid, string> SeedContent = new();
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static readonly Guid SamplePages = Guid.Parse("a1000000-0000-0000-0000-000000000001");
    public static readonly Guid SampleWorkbook = Guid.Parse("b2000000-0000-0000-0000-000000000002");
    public static readonly Guid SampleSlides = Guid.Parse("c3000000-0000-0000-0000-000000000003");

    private readonly IDocumentFileStore _files;
    private readonly StorageOptions _storage;

    static MockDocumentService() => Seed();

    public MockDocumentService(IDocumentFileStore files, Microsoft.Extensions.Options.IOptions<StorageOptions> storage)
    {
        _files = files; _storage = storage.Value;
    }

    /// <summary>File counts and size for one client (the Clients page).</summary>
    internal static (int Pages, int Workbooks, int Decks, long Bytes) CountsFor(Guid tenantId)
    {
        var mine = Store.Values.Where(d => TenantOf.TryGetValue(d.DocumentId, out var t) && t == tenantId).ToList();
        return (mine.Count(d => d.App == SuiteApp.Pages), mine.Count(d => d.App == SuiteApp.Workbook), mine.Count(d => d.App == SuiteApp.Slides),
            mine.Sum(d => SizeOf.GetValueOrDefault(d.DocumentId)));
    }

    private static bool InTenant(Guid documentId, Guid tenantId) => TenantOf.TryGetValue(documentId, out var t) && t == tenantId;

    public Task<List<DocumentSummaryDto>> RecentAsync(Guid tenantId, Guid userId, SuiteApp? app, int take = 20) =>
        Task.FromResult(Store.Values
            .Where(d => InTenant(d.DocumentId, tenantId) && (app is null || d.App == app))
            .OrderByDescending(d => d.ModifiedUtc).Take(take)
            .Select(d => new DocumentSummaryDto(d.DocumentId, d.App, d.Title, d.OwnerName, d.ModifiedUtc, d.IsStarred))
            .ToList());

    public async Task<DocumentDto?> GetAsync(Guid tenantId, Guid userId, Guid documentId)
    {
        if (!InTenant(documentId, tenantId) || !Store.TryGetValue(documentId, out var d)) return null;
        var path = _files.PathFor(tenantId, d.App, documentId);
        if (!_files.Exists(path) && SeedContent.TryGetValue(documentId, out var seed))
            _files.Commit(await _files.StageAsync(path, seed), path);   // first time a sample is opened
        return d with { Content = await _files.ReadAsync(path) ?? "" };
    }

    public async Task<OperationResult> SaveAsync(Guid tenantId, Guid userId, DocumentSave save)
    {
        var now = DateTime.UtcNow;
        var id = save.DocumentId ?? Guid.NewGuid();
        if (save.DocumentId is not null && !InTenant(id, tenantId)) return OperationResult.Fail("You can't change this file.");
        var content = save.Content ?? "";
        var size = _files.SizeOf(content);
        if (size > _storage.MaxFileBytes)
            return OperationResult.Fail($"This file is {Sizes.Bytes(size)}, more than the {_storage.MaxFileMb} MB limit. Make the pictures smaller or split the file.");

        var path = _files.PathFor(tenantId, save.App, id);
        _files.Commit(await _files.StageAsync(path, content), path);
        TenantOf[id] = tenantId;
        SizeOf[id] = size;
        SeedContent.TryRemove(id, out _);
        Store.AddOrUpdate(id,
            _ => new DocumentDto(id, save.App, save.Title, "", userId, "You", now, now, false),
            (_, old) => old with { Title = save.Title, ModifiedUtc = now });
        return OperationResult.Ok(id);
    }

    public Task<bool> SetStarredAsync(Guid userId, Guid documentId, bool starred)
    {
        if (!Store.TryGetValue(documentId, out var d)) return Task.FromResult(false);
        Store[documentId] = d with { IsStarred = starred };
        return Task.FromResult(true);
    }

    /// <summary>Like SQL mode, deleting hides the file from the lists and keeps it on disk.</summary>
    public Task<bool> DeleteAsync(Guid tenantId, Guid userId, Guid documentId) =>
        Task.FromResult(InTenant(documentId, tenantId) && Store.TryRemove(documentId, out _));

    private static void Add(Guid id, SuiteApp app, string title, object content, int hoursAgo, bool starred = false)
    {
        var when = DateTime.UtcNow.AddHours(-hoursAgo);
        var body = content as string ?? JsonSerializer.Serialize(content, Json);
        Store[id] = new DocumentDto(id, app, title, "", MockUserService.DemoUserId, "Demo User", when.AddDays(-3), when, starred);
        SeedContent[id] = body;
        SizeOf[id] = System.Text.Encoding.UTF8.GetByteCount(body);
        TenantOf[id] = MockData.AcmeId;   // the samples belong to the sample client "Acme Corp"
    }

    private static void Seed()
    {
        Add(SamplePages, SuiteApp.Pages, "Project charter", new PagesContent
        {
            Html = """
                <h1>Project charter: Vaikunth Office</h1>
                <p><em>Prepared by the product team · Draft 0.3</em></p>
                <h2>Purpose</h2>
                <p>Vaikunth Office brings documents, workbooks and slides together in one place, so a team can
                <b>write, calculate and present</b> without leaving the browser.</p>
                <h2>Goals for this quarter</h2>
                <ul>
                  <li>Ship the three apps with the everyday editing features people expect.</li>
                  <li>Sign in once and move between apps from the launcher.</li>
                  <li>Keep every file in our own SQL Server database.</li>
                </ul>
                <blockquote>Yogah karmasu kaushalam: Excellence in every action.</blockquote>
                <h2>Milestones</h2>
                <table><tr><th>Milestone</th><th>Owner</th><th>Due</th></tr>
                <tr><td>Mock screens</td><td>Design</td><td>December</td></tr>
                <tr><td>Database and services</td><td>Platform</td><td>January</td></tr>
                <tr><td>First release</td><td>Everyone</td><td>March</td></tr></table>
                """
        }, 1, starred: true);

        var sheet = new SheetDto { Name = "Sales 2026" };
        void C(string a, string v, bool bold = false, string? fill = null, string? fmt = null, string? align = null) =>
            sheet.Cells[a] = new CellDto { Input = v, Bold = bold, Fill = fill, Format = fmt, Align = align };
        string[] head = ["Region", "Q1", "Q2", "Q3", "Q4", "Total", "Growth"];
        for (var i = 0; i < head.Length; i++) C($"{(char)('A' + i)}1", head[i], true, "#D1FAE5", align: i == 0 ? null : "center");
        (string Region, int[] Q)[] rows =
        [
            ("North", [42000, 45500, 51000, 58250]),
            ("South", [38500, 40100, 39800, 44700]),
            ("East", [27300, 31900, 36400, 40200]),
            ("West", [33100, 35600, 37800, 43900]),
            ("Central", [21800, 24300, 26900, 30100]),
        ];
        for (var r = 0; r < rows.Length; r++)
        {
            var n = r + 2;
            C($"A{n}", rows[r].Region, true);
            for (var q = 0; q < 4; q++) C($"{(char)('B' + q)}{n}", rows[r].Q[q].ToString(), fmt: "currency");
            C($"F{n}", $"=SUM(B{n}:E{n})", true, fmt: "currency");
            C($"G{n}", $"=(E{n}-B{n})/B{n}", fmt: "percent");
        }
        C("A7", "Total", true, "#ECFDF5");
        foreach (var col in "BCDEF") C($"{col}7", $"=SUM({col}2:{col}6)", true, "#ECFDF5", "currency");
        C("G7", "=(E7-B7)/B7", true, "#ECFDF5", "percent");
        C("A9", "Average per region", true);
        C("F9", "=AVERAGE(F2:F6)", fmt: "currency");
        C("A10", "Best quarter", true);
        C("F10", "=MAX(B7:E7)", fmt: "currency");
        Add(SampleWorkbook, SuiteApp.Workbook, "Sales by region 2026", new WorkbookContent { Sheets = [sheet, new SheetDto { Name = "Notes" }] }, 3, starred: true);

        Add(SampleSlides, SuiteApp.Slides, "Quarterly review", new SlidesContent
        {
            Theme = "sunrise",
            Slides =
            [
                new SlideDto { Layout = "title", Notes = "Welcome everyone. Keep this short.", Boxes =
                [
                    new SlideBoxDto { X = 8, Y = 30, W = 84, H = 20, Html = "Quarterly review", FontSize = 54, Role = "title" },
                    new SlideBoxDto { X = 8, Y = 52, W = 84, H = 12, Html = "Excellence in every action · Q4 2026", FontSize = 24, Role = "subtitle" },
                ]},
                new SlideDto { Layout = "content", Notes = "Numbers come from the Sales by region workbook.", Boxes =
                [
                    new SlideBoxDto { X = 6, Y = 6, W = 88, H = 14, Html = "What went well", FontSize = 40, Role = "title" },
                    new SlideBoxDto { X = 6, Y = 24, W = 56, H = 66, FontSize = 24, Role = "body",
                        Html = "<ul><li>Sales up 26% across all regions</li><li>North passed 58,000 in Q4</li><li>East grew fastest: 47%</li><li>Two new teams on board</li></ul>" },
                    new SlideBoxDto { Kind = "shape", X = 66, Y = 28, W = 28, H = 50, Fill = "#E8A84A", Html = "<b>+26%</b><br>year on year", FontSize = 30, Color = "#ffffff" },
                ]},
                new SlideDto { Layout = "content", Notes = "", Boxes =
                [
                    new SlideBoxDto { X = 6, Y = 6, W = 88, H = 14, Html = "Next quarter", FontSize = 40, Role = "title" },
                    new SlideBoxDto { X = 6, Y = 24, W = 88, H = 66, FontSize = 24, Role = "body",
                        Html = "<ol><li>Launch Vaikunth Office to the first customers</li><li>Open the Central region office</li><li>Hire two more engineers</li></ol>" },
                ]},
            ]
        }, 6);

        Add(Guid.NewGuid(), SuiteApp.Pages, "Meeting notes, 24 November", new PagesContent { Html = "<h1>Meeting notes</h1><p>Agreed the launcher design and the three app colours.</p>" }, 26);
        Add(Guid.NewGuid(), SuiteApp.Workbook, "Team budget", new WorkbookContent { Sheets = [new SheetDto()] }, 50);
        Add(Guid.NewGuid(), SuiteApp.Slides, "Product launch pitch", new SlidesContent { Slides = [new SlideDto { Boxes = [new SlideBoxDto { X = 8, Y = 34, W = 84, H = 20, Html = "Product launch", FontSize = 54, Role = "title" }] }] }, 75);
    }
}
