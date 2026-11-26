using Microsoft.Data.SqlClient;
using Vaikunth.DtoLibrary;
using Vaikunth.ServiceLibrary.Data;

namespace Vaikunth.ServiceLibrary;

/// <summary>
/// Files of all three apps, kept per client (tenant). Every read only returns what the person owns or was shared,
/// inside their own client. The list of files is the table Vaikunth_Documents; the content is a file on the server.
/// </summary>
public interface IDocumentService
{
    Task<List<DocumentSummaryDto>> RecentAsync(Guid tenantId, Guid userId, SuiteApp? app, int take = 20);
    Task<DocumentDto?> GetAsync(Guid tenantId, Guid userId, Guid documentId);
    Task<OperationResult> SaveAsync(Guid tenantId, Guid userId, DocumentSave save);
    Task<bool> SetStarredAsync(Guid userId, Guid documentId, bool starred);
    Task<bool> DeleteAsync(Guid tenantId, Guid userId, Guid documentId);
}

/// <summary>
/// Documents: the list (name, who made it, dates, deleted) is in SQL Server (Vaikunth_Document_* in database/Vaikunth_Update.sql),
/// the content of each file is a file on the server (<see cref="IDocumentFileStore"/>, appsettings "Storage").
/// </summary>
public sealed class DocumentService : IDocumentService
{
    private readonly SqlDb _db;
    private readonly IDocumentFileStore _files;
    private readonly StorageOptions _storage;

    public DocumentService(SqlDb db, IDocumentFileStore files, Microsoft.Extensions.Options.IOptions<StorageOptions> storage)
    {
        _db = db; _files = files; _storage = storage.Value;
    }

    private static DocumentSummaryDto Summary(SqlDataReader r) => new(
        r.G("DocumentId"), (SuiteApp)Convert.ToByte(r["AppKind"]), r.S("FileName"), r.S("CreatedByName"), r.Utc("UpdatedUtc"), r.B("IsStarred"));

    public Task<List<DocumentSummaryDto>> RecentAsync(Guid tenantId, Guid userId, SuiteApp? app, int take = 20) =>
        _db.QueryAsync("dbo.Vaikunth_Document_Recent",
            p => p.With("@TenantId", tenantId).With("@UserId", userId).With("@AppKind", app is null ? null : (byte)app).With("@Take", take), Summary);

    public async Task<DocumentDto?> GetAsync(Guid tenantId, Guid userId, Guid documentId)
    {
        var rows = await _db.QueryAsync("dbo.Vaikunth_Document_Get",
            p => p.With("@TenantId", tenantId).With("@UserId", userId).With("@DocumentId", documentId),
            r => (Doc: new DocumentDto(r.G("DocumentId"), (SuiteApp)Convert.ToByte(r["AppKind"]), r.S("FileName"), "",
                    r.G("CreatedBy"), r.S("CreatedByName"), r.Utc("CreatedUtc"), r.Utc("UpdatedUtc"), r.B("IsStarred")),
                  Path: r.S("StoragePath")));
        if (rows.Count == 0) return null;
        var row = rows[0];
        var content = string.IsNullOrEmpty(row.Path) ? null : await _files.ReadAsync(row.Path);
        return row.Doc with { Content = content ?? "" };
    }

    public async Task<OperationResult> SaveAsync(Guid tenantId, Guid userId, DocumentSave save)
    {
        var name = string.IsNullOrWhiteSpace(save.Title) ? "Untitled" : save.Title.Trim();
        if (name.Length > 200) name = name[..200];
        var content = save.Content ?? "";
        var size = _files.SizeOf(content);
        if (size > _storage.MaxFileBytes)
            return OperationResult.Fail($"This file is {Sizes.Bytes(size)}, more than the {_storage.MaxFileMb} MB limit. Make the pictures smaller or split the file.");

        var isNew = save.DocumentId is null;
        var id = save.DocumentId ?? Guid.NewGuid();
        var path = _files.PathFor(tenantId, save.App, id);

        // 1. write the content next to the real file, 2. record it in the database (checks rights and quota), 3. swap the file in
        var staged = await _files.StageAsync(path, content);
        try
        {
            var rows = await _db.QueryAsync("dbo.Vaikunth_Document_Save", p => p
                .With("@DocumentId", id).With("@IsNew", isNew).With("@TenantId", tenantId).With("@UserId", userId).With("@AppKind", (byte)save.App)
                .AddText("@FileName", name, 200).AddText("@StoragePath", path, 400).With("@SizeBytes", size),
                r => (Result: r.I("Result"), Path: r.S("StoragePath")));
            if (rows.Count == 0) { _files.Discard(staged); return OperationResult.Fail("You can't change this file."); }
            if (rows[0].Result == -1)
            {
                _files.Discard(staged);
                return OperationResult.Fail("Your company's storage is full. Ask your admin to free space or move to a bigger plan.");
            }
            _files.Commit(staged, string.IsNullOrEmpty(rows[0].Path) ? path : rows[0].Path);
            return OperationResult.Ok(id);
        }
        catch
        {
            _files.Discard(staged);
            throw;
        }
    }

    public async Task<bool> SetStarredAsync(Guid userId, Guid documentId, bool starred) =>
        await _db.ExecAsync("dbo.Vaikunth_Document_SetStarred", p => p.With("@UserId", userId).With("@DocumentId", documentId).With("@IsStarred", starred)) > 0;

    /// <summary>Soft delete: the row gets IsDeleted = 1 and the file stays on disk, so an admin can still bring it back.</summary>
    public async Task<bool> DeleteAsync(Guid tenantId, Guid userId, Guid documentId) =>
        await _db.ExecAsync("dbo.Vaikunth_Document_Delete", p => p.With("@TenantId", tenantId).With("@UserId", userId).With("@DocumentId", documentId)) > 0;
}
