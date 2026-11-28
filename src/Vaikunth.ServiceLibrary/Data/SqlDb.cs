using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Vaikunth.ServiceLibrary.Data;

/// <summary>
/// Thin ADO.NET helper: every database call in Vaikunth Desk is a stored procedure run through here.
/// Opening a connection retries automatically on Azure SQL's short "transient" errors.
/// </summary>
public sealed class SqlDb
{
    private readonly string _connectionString;

    private static readonly SqlRetryLogicBaseProvider OpenRetry = SqlConfigurableRetryFactory.CreateExponentialRetryProvider(
        new SqlRetryLogicOption
        {
            NumberOfTries = 4,
            DeltaTime = TimeSpan.FromSeconds(1),
            MaxTimeInterval = TimeSpan.FromSeconds(10)
        });

    public SqlDb(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Connection string 'Default' is missing in appsettings.json.");
    }

    public async Task<SqlConnection> OpenAsync(CancellationToken ct = default)
    {
        var connection = new SqlConnection(_connectionString) { RetryLogicProvider = OpenRetry };
        await connection.OpenAsync(ct);
        return connection;
    }

    private static SqlCommand Proc(SqlConnection connection, string name, Action<SqlParameterCollection>? parameters)
    {
        var command = new SqlCommand(name, connection) { CommandType = CommandType.StoredProcedure, CommandTimeout = 30 };
        parameters?.Invoke(command.Parameters);
        return command;
    }

    /// <summary>Runs a procedure that returns no rows. Output parameters can be read after this returns.</summary>
    public async Task<int> ExecAsync(string procedure, Action<SqlParameterCollection>? parameters = null)
    {
        await using var connection = await OpenAsync();
        await using var command = Proc(connection, procedure, parameters);
        return await command.ExecuteNonQueryAsync();
    }

    /// <summary>Runs a procedure and maps every row. Output parameters can be read after this returns.</summary>
    public async Task<List<T>> QueryAsync<T>(string procedure, Action<SqlParameterCollection>? parameters, Func<SqlDataReader, T> map)
    {
        await using var connection = await OpenAsync();
        await using var command = Proc(connection, procedure, parameters);
        var rows = new List<T>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
                rows.Add(map(reader));
        }
        return rows;
    }

    public async Task<T?> QueryFirstAsync<T>(string procedure, Action<SqlParameterCollection>? parameters, Func<SqlDataReader, T> map)
        where T : class
    {
        var rows = await QueryAsync(procedure, parameters, map);
        return rows.Count > 0 ? rows[0] : null;
    }

    /// <summary>For procedures that return two result sets (e.g. meetings + their invitees).</summary>
    public async Task<(List<TA> First, List<TB> Second)> QueryMultipleAsync<TA, TB>(
        string procedure, Action<SqlParameterCollection>? parameters, Func<SqlDataReader, TA> mapA, Func<SqlDataReader, TB> mapB)
    {
        await using var connection = await OpenAsync();
        await using var command = Proc(connection, procedure, parameters);
        var first = new List<TA>();
        var second = new List<TB>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync()) first.Add(mapA(reader));
            if (await reader.NextResultAsync())
                while (await reader.ReadAsync()) second.Add(mapB(reader));
        }
        return (first, second);
    }
}

/// <summary>Short helpers for adding parameters and reading columns.</summary>
public static class SqlExtensions
{
    public static SqlParameterCollection With(this SqlParameterCollection p, string name, object? value)
    {
        p.AddWithValue(name, value ?? DBNull.Value);
        return p;
    }

    public static SqlParameterCollection AddText(this SqlParameterCollection p, string name, string? value, int size)
    {
        p.Add(new SqlParameter(name, SqlDbType.NVarChar, size) { Value = (object?)value ?? DBNull.Value });
        return p;
    }

    public static SqlParameterCollection AddDateTime2(this SqlParameterCollection p, string name, DateTime? value)
    {
        p.Add(new SqlParameter(name, SqlDbType.DateTime2) { Value = (object?)value ?? DBNull.Value });
        return p;
    }

    public static SqlParameterCollection AddDate(this SqlParameterCollection p, string name, DateOnly? value)
    {
        p.Add(new SqlParameter(name, SqlDbType.Date) { Value = value is DateOnly d ? d.ToDateTime(TimeOnly.MinValue) : DBNull.Value });
        return p;
    }

    public static DateOnly? DN(this SqlDataReader r, string column) => r[column] is DateTime d ? DateOnly.FromDateTime(d) : null;
    public static DateOnly D(this SqlDataReader r, string column) => DateOnly.FromDateTime(Convert.ToDateTime(r[column]));
    public static decimal M(this SqlDataReader r, string column) => Convert.ToDecimal(r[column]);

    public static SqlParameter AddOutput(this SqlParameterCollection p, string name, SqlDbType type)
    {
        var parameter = new SqlParameter(name, type) { Direction = ParameterDirection.Output };
        p.Add(parameter);
        return parameter;
    }

    public static int OutInt(this SqlParameter p) => p.Value is DBNull or null ? 0 : Convert.ToInt32(p.Value);

    public static Guid G(this SqlDataReader r, string column) => r.GetGuid(r.GetOrdinal(column));

    public static Guid? GN(this SqlDataReader r, string column)
    {
        var i = r.GetOrdinal(column);
        return r.IsDBNull(i) ? null : r.GetGuid(i);
    }

    public static string S(this SqlDataReader r, string column)
    {
        var i = r.GetOrdinal(column);
        return r.IsDBNull(i) ? "" : r.GetString(i);
    }

    public static string? SN(this SqlDataReader r, string column)
    {
        var i = r.GetOrdinal(column);
        return r.IsDBNull(i) ? null : r.GetString(i);
    }

    public static int I(this SqlDataReader r, string column) => Convert.ToInt32(r[column]);

    public static int? IN(this SqlDataReader r, string column)
    {
        var i = r.GetOrdinal(column);
        return r.IsDBNull(i) ? null : Convert.ToInt32(r.GetValue(i));
    }

    public static long L(this SqlDataReader r, string column) => Convert.ToInt64(r[column]);

    public static bool B(this SqlDataReader r, string column) => Convert.ToBoolean(r[column]);

    /// <summary>True when the result has this column (lets the app run before an optional upgrade script).</summary>
    public static bool Has(this SqlDataReader r, string column)
    {
        for (var i = 0; i < r.FieldCount; i++)
            if (string.Equals(r.GetName(i), column, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>A bit column that may not exist yet: false when missing or NULL.</summary>
    public static bool BOpt(this SqlDataReader r, string column) =>
        r.Has(column) && r[column] is not DBNull && Convert.ToBoolean(r[column]);

    /// <summary>A number column that may not exist yet: 0 when missing or NULL.</summary>
    public static int IOpt(this SqlDataReader r, string column) =>
        r.Has(column) && r[column] is not DBNull ? Convert.ToInt32(r[column]) : 0;

    /// <summary>A UTC time column that may not exist yet: null when missing or NULL.</summary>
    public static DateTime? UtcOpt(this SqlDataReader r, string column) =>
        r.Has(column) && r[column] is DateTime d ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : null;

    /// <summary>SQL stores UTC times; mark them as UTC so they convert and serialise correctly.</summary>
    public static DateTime Utc(this SqlDataReader r, string column) =>
        DateTime.SpecifyKind(r.GetDateTime(r.GetOrdinal(column)), DateTimeKind.Utc);

    public static DateTime? UtcN(this SqlDataReader r, string column)
    {
        var i = r.GetOrdinal(column);
        return r.IsDBNull(i) ? null : DateTime.SpecifyKind(r.GetDateTime(i), DateTimeKind.Utc);
    }
}
