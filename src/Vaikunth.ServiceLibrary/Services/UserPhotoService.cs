using System.Collections.Concurrent;
using Vaikunth.DtoLibrary;
using Vaikunth.ServiceLibrary.Data;

namespace Vaikunth.ServiceLibrary;

/// <summary>Profile photos. They are kept in the database with the rest of the person (Vaikunth_Users.Photo).</summary>
public interface IUserPhotoService
{
    /// <summary>The photo of <paramref name="userId"/>, if the viewer may see it: people of the same client, or the super admin.</summary>
    Task<UserPhotoDto?> GetAsync(Guid userId, Guid? viewerTenantId, bool viewerIsSuperAdmin);
    /// <summary>0 when the person has no photo.</summary>
    Task<long> VersionAsync(Guid userId);
    /// <summary>Saves (or with null, removes) the photo. Returns the new version, 0 when removed.</summary>
    Task<long> SetAsync(Guid userId, byte[]? data, string? contentType);
}

public static class UserPhotos
{
    public const int MaxBytes = 2 * 1024 * 1024;

    /// <summary>Checks the bytes really are a PNG, JPEG, WebP or GIF picture (not just the file name), and returns its type.</summary>
    public static string? Sniff(byte[] data)
    {
        if (data.Length < 12) return null;
        if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47) return "image/png";
        if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF) return "image/jpeg";
        if (data[0] == 'G' && data[1] == 'I' && data[2] == 'F') return "image/gif";
        if (data[0] == 'R' && data[1] == 'I' && data[2] == 'F' && data[3] == 'F' && data[8] == 'W' && data[9] == 'E' && data[10] == 'B' && data[11] == 'P') return "image/webp";
        return null;
    }
}

/// <summary>Photos in SQL Server (Vaikunth_User_GetPhoto / _GetPhotoVersion / _SetPhoto).</summary>
public sealed class UserPhotoService : IUserPhotoService
{
    private readonly SqlDb _db;
    public UserPhotoService(SqlDb db) => _db = db;

    private static long Version(DateTime? utc) => utc?.Ticks ?? 0;

    public Task<UserPhotoDto?> GetAsync(Guid userId, Guid? viewerTenantId, bool viewerIsSuperAdmin) =>
        _db.QueryFirstAsync("dbo.Vaikunth_User_GetPhoto",
            p => p.With("@UserId", userId).With("@ViewerTenantId", viewerTenantId).With("@ViewerIsSuperAdmin", viewerIsSuperAdmin),
            r => new UserPhotoDto((byte[])r["Photo"], r.S("PhotoType"), Version(r.UtcN("PhotoUpdatedUtc"))));

    public async Task<long> VersionAsync(Guid userId) =>
        Version((await _db.QueryAsync("dbo.Vaikunth_User_GetPhotoVersion", p => p.With("@UserId", userId), r => r.UtcN("PhotoUpdatedUtc"))).FirstOrDefault());

    public async Task<long> SetAsync(Guid userId, byte[]? data, string? contentType) =>
        Version((await _db.QueryAsync("dbo.Vaikunth_User_SetPhoto",
            p =>
            {
                p.With("@UserId", userId);
                p.Add("@Photo", System.Data.SqlDbType.VarBinary, -1).Value = (object?)data ?? DBNull.Value;
                p.AddText("@PhotoType", contentType, 50);
            }, r => r.UtcN("PhotoUpdatedUtc"))).FirstOrDefault());
}

/// <summary>Photos in memory for mock mode (lost on restart).</summary>
public sealed class MockUserPhotoService : IUserPhotoService
{
    internal static readonly ConcurrentDictionary<Guid, UserPhotoDto> Photos = new();

    public Task<UserPhotoDto?> GetAsync(Guid userId, Guid? viewerTenantId, bool viewerIsSuperAdmin) =>
        Task.FromResult(Photos.TryGetValue(userId, out var p) ? p : null);

    public Task<long> VersionAsync(Guid userId) => Task.FromResult(Photos.TryGetValue(userId, out var p) ? p.Version : 0);

    public Task<long> SetAsync(Guid userId, byte[]? data, string? contentType)
    {
        if (data is null) { Photos.TryRemove(userId, out _); return Task.FromResult(0L); }
        var v = DateTime.UtcNow.Ticks;
        Photos[userId] = new UserPhotoDto(data, contentType ?? "image/jpeg", v);
        return Task.FromResult(v);
    }
}
