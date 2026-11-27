using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vaikunth.DtoLibrary;
using Vaikunth.ServiceLibrary.Data;

namespace Vaikunth.ServiceLibrary;

/// <summary>
/// After upgrading a database whose file content was still inside Vaikunth_Documents, Vaikunth_Update.sql leaves that
/// content in Vaikunth_DocumentsToMove. When an app starts, this writes its own app's files to the storage folder and
/// records where they went. Nothing to do on a new database. Each app moves only its own kind of file
/// (Pages → Word files, Workbook → Excel, Slides → PowerPoint), so each lands in that app's storage folder.
/// </summary>
public sealed class DocumentMoveService : BackgroundService
{
    private readonly SqlDb _db;
    private readonly IDocumentFileStore _files;
    private readonly ILogger<DocumentMoveService> _log;
    private readonly SuiteApp _app;

    public DocumentMoveService(SqlDb db, IDocumentFileStore files, ILogger<DocumentMoveService> log, SuiteApp app)
    {
        _db = db; _files = files; _log = log; _app = app;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var moved = 0;
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var batch = await _db.QueryAsync("dbo.Vaikunth_DocumentMove_Next", p => p.With("@AppKind", (byte)_app).With("@Take", 50),
                    r => (Id: r.G("DocumentId"), Tenant: r.G("TenantId"), Content: r.S("Content")));
                if (batch.Count == 0) break;
                foreach (var d in batch)
                {
                    var path = _files.PathFor(d.Tenant, _app, d.Id);
                    _files.Commit(await _files.StageAsync(path, d.Content, stoppingToken), path);
                    await _db.ExecAsync("dbo.Vaikunth_DocumentMove_Done",
                        p => p.With("@DocumentId", d.Id).AddText("@StoragePath", path, 400).With("@SizeBytes", _files.SizeOf(d.Content)));
                    moved++;
                }
            }
            if (moved > 0) _log.LogInformation("Moved {Count} {App} files out of the database into {Root}", moved, _app, _files.Root);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            _log.LogError(ex, "Moving {App} files out of the database stopped after {Count}; it carries on at the next start", _app, moved);
        }
    }
}
