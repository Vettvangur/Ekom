using Ekom.Repositories;
using LinqToDB.Data;
using Microsoft.Data.SqlClient;

namespace Ekom.Services;

/// <summary>
/// Installs narrow performance indexes without replacing existing index definitions.
/// </summary>
internal static class DatabaseIndexInstaller
{
    internal static void EnsureIndex(DbContext db, bool sqlServer, string tableName, string indexName, params string[] columns)
    {
        var parameters = new List<DataParameter>
        {
            new("tableName", tableName),
            new("indexName", indexName),
            new("columnCount", columns.Length),
        };

        string eligibleQuery;
        string namedQuery;
        if (sqlServer)
        {
            eligibleQuery = @"
SELECT i.name FROM sys.indexes i
WHERE i.object_id = OBJECT_ID(@tableName) AND i.type IN (1, 2)
AND i.has_filter = 0 AND i.is_disabled = 0 AND i.is_hypothetical = 0
AND (SELECT COUNT(*) FROM sys.index_columns k
    WHERE k.object_id = i.object_id AND k.index_id = i.index_id AND k.key_ordinal > 0) = @columnCount";
            namedQuery = "SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID(@tableName) AND name = @indexName";
        }
        else
        {
            eligibleQuery = @"
SELECT i.name FROM pragma_index_list(@tableName) i
WHERE i.partial = 0
AND (SELECT COUNT(*) FROM pragma_index_info(i.name)) = @columnCount";
            // SQLite index names are shared across all tables in the database.
            namedQuery = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = @indexName COLLATE NOCASE";
        }

        for (int ordinal = 0; ordinal < columns.Length; ordinal++)
        {
            string parameterName = $"column{ordinal}";
            parameters.Add(new DataParameter(parameterName, columns[ordinal]));
            eligibleQuery += sqlServer
                ? $@"
AND EXISTS (SELECT 1 FROM sys.index_columns k JOIN sys.columns c ON c.object_id = k.object_id AND c.column_id = k.column_id
    WHERE k.object_id = i.object_id AND k.index_id = i.index_id AND k.key_ordinal = {ordinal + 1} AND c.name = @{parameterName})"
                : $@"
AND EXISTS (SELECT 1 FROM pragma_index_xinfo(i.name) k
    WHERE k.[key] = 1 AND k.seqno = {ordinal} AND k.name = @{parameterName} COLLATE NOCASE AND k.coll = 'BINARY' COLLATE NOCASE)";
        }

        string canonicalQuery = $"SELECT COUNT(*) FROM ({eligibleQuery}) eligible WHERE name = @indexName"
            + (sqlServer ? string.Empty : " COLLATE NOCASE");
        var args = parameters.ToArray();

        // Inspect existence before eligibility to tolerate another node installing the index.
        bool namedIndexExists = db.Execute<int>(namedQuery, args) != 0;
        if (namedIndexExists && db.Execute<int>(canonicalQuery, args) == 0)
        {
            throw new InvalidOperationException($"Index '{indexName}' has an incompatible definition. Expected a full index on {tableName} ({string.Join(", ", columns)}). No existing index has been replaced.");
        }

        if (db.Query<string>(eligibleQuery, args).Any())
        {
            return;
        }

        if (sqlServer)
        {
            string columnParameters = string.Join(", ", columns.Select((_, ordinal) => $"@column{ordinal}"));
            int declaredKeyBytes = db.Execute<int>($@"
SELECT COALESCE(SUM(CASE WHEN c.max_length < 0 THEN 1701 ELSE c.max_length END), 0)
FROM sys.columns c
WHERE c.object_id = OBJECT_ID(@tableName) AND c.name IN ({columnParameters})", args);
            if (declaredKeyBytes > 1700)
            {
                throw new InvalidOperationException($"Index '{indexName}' cannot safely index the existing declared column widths on {tableName}. Review the schema before retrying; no column types or lengths have been changed.");
            }
        }

        string keys = string.Join(", ", columns.Select(QuoteIdentifier));
        string createSql = sqlServer
            ? $"CREATE NONCLUSTERED INDEX {QuoteIdentifier(indexName)} ON {QuoteIdentifier(tableName)} ({keys});"
            : $"CREATE INDEX IF NOT EXISTS {QuoteIdentifier(indexName)} ON {QuoteIdentifier(tableName)} ({keys});";
        try
        {
            db.Execute(createSql);
        }
        catch (SqlException ex) when (ex.Number == 1913)
        {
            if (db.Execute<int>(canonicalQuery, args) == 0)
            {
                throw;
            }
        }

        if (db.Execute<int>(canonicalQuery, args) == 0)
        {
            throw new InvalidOperationException($"Index '{indexName}' could not be verified after creation. Check for an incompatible index definition before retrying the migration.");
        }
    }

    private static string QuoteIdentifier(string identifier) => $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";
}
