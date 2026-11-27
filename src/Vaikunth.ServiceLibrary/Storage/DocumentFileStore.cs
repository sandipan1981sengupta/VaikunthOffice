using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vaikunth.DtoLibrary;

namespace Vaikunth.ServiceLibrary;

/// <summary>
/// appsettings "Storage": where the Word, Excel and PowerPoint files are kept on the server.
/// The database only keeps each file's name, who made it, dates and whether it is deleted (Vaikunth_Documents).
/// </summary>
public sealed class StorageOptions
{
    /// <summary>
    /// Folder for the files, e.g. "D:\\VaikunthFiles" or a share "\\\\fileserver\\VaikunthFiles".
    /// Leave empty to use App_Data\VaikunthFiles inside the folder where the app is hosted.
    /// </summary>
    public string RootPath { get; set; } = "";

    /// <summary>Largest file a person can save, in MB (pictures inside the file count). Default 200.</summary>
    public int MaxFileMb { get; set; } = 200;

    public long MaxFileBytes => Math.Max(1, MaxFileMb) * 1024L * 1024L;
}

/// <summary>Reads and writes file contents on the server's disk.</summary>
public interface IDocumentFileStore
{
    /// <summary>The full folder in use (for the About window and the logs).</summary>
    string Root { get; }

    /// <summary>Where a file lives, relative to <see cref="Root"/>: {client}/{App}/{file id}.json. This is what Vaikunth_Documents.StoragePath holds.</summary>
    string PathFor(Guid tenantId, SuiteApp app, Guid documentId);

    /// <summary>Size in bytes that <paramref name="content"/> takes on disk (UTF-8).</summary>
    long SizeOf(string content);

    /// <summary>Writes to a temporary file next to the real one; call <see cref="Commit"/> or <see cref="Discard"/> after.</summary>
    Task<string> StageAsync(string relativePath, string content, CancellationToken ct = default);
    void Commit(string stagedFile, string relativePath);
    void Discard(string stagedFile);

    Task<string?> ReadAsync(string relativePath, CancellationToken ct = default);
    bool Exists(string relativePath);

    /// <summary>Removes a deleted client's folder ({client}/) with all its files. Returns false when it couldn't (the error is logged).</summary>
    bool DeleteClientFolder(Guid tenantId);
}

public sealed class DocumentFileStore : IDocumentFileStore
{
    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);
    private readonly ILogger<DocumentFileStore> _log;

    public DocumentFileStore(IOptions<StorageOptions> options, ILogger<DocumentFileStore> log)
    {
        _log = log;
        var root = options.Value.RootPath;
        if (string.IsNullOrWhiteSpace(root)) root = Path.Combine(AppContext.BaseDirectory, "App_Data", "VaikunthFiles");
        Root = Path.GetFullPath(root);
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public string PathFor(Guid tenantId, SuiteApp app, Guid documentId) =>
        $"{tenantId:N}/{app}/{documentId:N}.json";

    public long SizeOf(string content) => Utf8.GetByteCount(content);

    /// <summary>Turns a stored relative path into a full path, refusing anything that would leave the root folder.</summary>
    private string Full(string relativePath)
    {
        var full = Path.GetFullPath(Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var rootWithSep = Root.EndsWith(Path.DirectorySeparatorChar) ? Root : Root + Path.DirectorySeparatorChar;
        if (!full.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("File path is outside the storage folder.");
        return full;
    }

    public async Task<string> StageAsync(string relativePath, string content, CancellationToken ct = default)
    {
        var full = Full(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var temp = $"{full}.{Guid.NewGuid():N}.saving";
        await File.WriteAllTextAsync(temp, content, Utf8, ct);
        return temp;
    }

    public void Commit(string stagedFile, string relativePath) => File.Move(stagedFile, Full(relativePath), overwrite: true);

    public void Discard(string stagedFile)
    {
        try { File.Delete(stagedFile); }
        catch (Exception ex) { _log.LogWarning(ex, "Could not remove temporary file {File}", stagedFile); }
    }

    public async Task<string?> ReadAsync(string relativePath, CancellationToken ct = default)
    {
        var full = Full(relativePath);
        if (!File.Exists(full))
        {
            _log.LogWarning("File {File} is listed in the database but missing from {Root}", relativePath, Root);
            return null;
        }
        return await File.ReadAllTextAsync(full, Utf8, ct);
    }

    public bool Exists(string relativePath) => File.Exists(Full(relativePath));

    public bool DeleteClientFolder(Guid tenantId)
    {
        var folder = Full($"{tenantId:N}");
        try
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
            return true;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Could not remove the files of deleted client {Tenant} in {Folder}", tenantId, folder);
            return false;
        }
    }
}
