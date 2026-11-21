using System.Data;
using System.Data.Common;
using System.Data.Odbc;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Npgsql;
using Oracle.ManagedDataAccess.Client;

namespace AVAUIDesigner.Data;

/// <summary>The database engines the designer reads schemas from and writes data-access code for.</summary>
public enum DbProvider { SqlServer, PostgreSql, MySql, Oracle, Sqlite, Db2, Odbc }

/// <summary>How one engine spells things: identifiers, parameters, getting a new row's key back, its ADO.NET classes.</summary>
public sealed class SqlDialect
{
    public required DbProvider Provider { get; init; }
    public required string DisplayName { get; init; }

    /// <summary>The ADO.NET connection class the generated code uses, and its using.</summary>
    public required string ConnectionClass { get; init; }
    public required string Namespace { get; init; }
    public required string Package { get; init; }

    /// <summary>The character before a named parameter ("@", ":"), or "?" for positional ones (ODBC).</summary>
    public required string ParameterPrefix { get; init; }

    public bool Positional => ParameterPrefix == "?";

    private Func<string, string> QuoteName { get; init; } = n => $"\"{n.Replace("\"", "\"\"")}\"";

    public string Quote(string name) => QuoteName(name);

    public string QualifiedTable(EntityModel e) =>
        string.IsNullOrWhiteSpace(e.Schema) ? Quote(e.TableName) : $"{Quote(e.Schema!)}.{Quote(e.TableName)}";

    /// <summary>A parameter placeholder in SQL text.</summary>
    public string Parameter(string name) => Positional ? "?" : ParameterPrefix + name;

    /// <summary>The name a parameter is added under in code ("@FirstName", ":FirstName", or "FirstName" for ODBC's "?").</summary>
    public string ParameterName(string name) => Positional ? name : ParameterPrefix + name;

    /// <summary>
    /// An INSERT that hands back the generated key, as this engine does it. <paramref name="columns"/> and
    /// <paramref name="values"/> are already spelled out.
    /// </summary>
    public string InsertReturningKey(string table, string columns, string values, string keyColumn) => Provider switch
    {
        DbProvider.SqlServer => $"INSERT INTO {table} ({columns}) OUTPUT INSERTED.{Quote(keyColumn)} VALUES ({values})",
        DbProvider.PostgreSql or DbProvider.Sqlite => $"INSERT INTO {table} ({columns}) VALUES ({values}) RETURNING {Quote(keyColumn)}",
        DbProvider.MySql => $"INSERT INTO {table} ({columns}) VALUES ({values}); SELECT LAST_INSERT_ID()",
        DbProvider.Db2 => $"SELECT {Quote(keyColumn)} FROM FINAL TABLE (INSERT INTO {table} ({columns}) VALUES ({values}))",
        _ => $"INSERT INTO {table} ({columns}) VALUES ({values})",
    };

    /// <summary>Oracle hands a generated key back through an output parameter ("RETURNING … INTO :NewKey").</summary>
    public bool KeyByOutputParameter => Provider == DbProvider.Oracle;

    /// <summary>A case-insensitive "contains" test on a text column, for a list's search box.</summary>
    public string Contains(string column, string parameter) => Provider switch
    {
        DbProvider.PostgreSql => $"{column} ILIKE {parameter}",
        DbProvider.Oracle or DbProvider.Db2 => $"UPPER({column}) LIKE UPPER({parameter})",
        _ => $"{column} LIKE {parameter}",
    };

    /// <summary>A row limit on a SELECT that has an ORDER BY.</summary>
    public string Limit(string select, int rows) => Provider switch
    {
        DbProvider.SqlServer => select.Replace("SELECT ", $"SELECT TOP ({rows}) ", StringComparison.Ordinal),
        DbProvider.Oracle or DbProvider.Db2 => $"{select} FETCH FIRST {rows} ROWS ONLY",
        _ => $"{select} LIMIT {rows}",
    };

    public static SqlDialect For(DbProvider provider) => All[provider];

    public static readonly IReadOnlyDictionary<DbProvider, SqlDialect> All = new Dictionary<DbProvider, SqlDialect>
    {
        [DbProvider.SqlServer] = new()
        {
            Provider = DbProvider.SqlServer, DisplayName = "SQL Server", ConnectionClass = "SqlConnection",
            Namespace = "Microsoft.Data.SqlClient", Package = "Microsoft.Data.SqlClient", ParameterPrefix = "@",
            QuoteName = n => $"[{n.Replace("]", "]]")}]",
        },
        [DbProvider.PostgreSql] = new()
        {
            Provider = DbProvider.PostgreSql, DisplayName = "PostgreSQL", ConnectionClass = "NpgsqlConnection",
            Namespace = "Npgsql", Package = "Npgsql", ParameterPrefix = "@",
        },
        [DbProvider.MySql] = new()
        {
            Provider = DbProvider.MySql, DisplayName = "MySQL / MariaDB", ConnectionClass = "MySqlConnection",
            Namespace = "MySqlConnector", Package = "MySqlConnector", ParameterPrefix = "@",
            QuoteName = n => $"`{n.Replace("`", "``")}`",
        },
        [DbProvider.Oracle] = new()
        {
            Provider = DbProvider.Oracle, DisplayName = "Oracle", ConnectionClass = "OracleConnection",
            Namespace = "Oracle.ManagedDataAccess.Client", Package = "Oracle.ManagedDataAccess.Core", ParameterPrefix = ":",
        },
        [DbProvider.Sqlite] = new()
        {
            Provider = DbProvider.Sqlite, DisplayName = "SQLite", ConnectionClass = "SqliteConnection",
            Namespace = "Microsoft.Data.Sqlite", Package = "Microsoft.Data.Sqlite", ParameterPrefix = "@",
        },
        [DbProvider.Db2] = new()
        {
            Provider = DbProvider.Db2, DisplayName = "IBM Db2 (through ODBC)", ConnectionClass = "OdbcConnection",
            Namespace = "System.Data.Odbc", Package = "System.Data.Odbc", ParameterPrefix = "?",
        },
        [DbProvider.Odbc] = new()
        {
            Provider = DbProvider.Odbc, DisplayName = "Another database (through ODBC)", ConnectionClass = "OdbcConnection",
            Namespace = "System.Data.Odbc", Package = "System.Data.Odbc", ParameterPrefix = "?",
        },
    };

    /// <summary>A provider by any common name ("sqlserver", "mssql", "postgres", "mariadb", "db2"…).</summary>
    public static DbProvider Parse(string text) => text.Trim().ToLowerInvariant().Replace(" ", "").Replace("-", "").Replace("_", "") switch
    {
        "sqlserver" or "mssql" or "sql" or "azuresql" or "microsoftsqlserver" or "tsql" => DbProvider.SqlServer,
        "postgresql" or "postgres" or "pg" or "npgsql" => DbProvider.PostgreSql,
        "mysql" or "mariadb" => DbProvider.MySql,
        "oracle" or "oracledb" => DbProvider.Oracle,
        "sqlite" or "sqlite3" => DbProvider.Sqlite,
        "db2" or "ibmdb2" or "db2luw" or "db2i" or "db2zos" => DbProvider.Db2,
        "odbc" or "other" => DbProvider.Odbc,
        _ => throw new ArgumentException($"\"{text}\" isn't a database the designer knows. Use sqlserver, postgresql, mysql (or mariadb), oracle, sqlite, db2 or odbc."),
    };
}

/// <summary>A table or view in a database.</summary>
public sealed record TableRef(string? Schema, string Name, bool IsView)
{
    public string FullName => Schema is null ? Name : $"{Schema}.{Name}";
}

/// <summary>
/// Reads a database's structure from its own catalogue: the tables and views, and each one's columns, keys and
/// foreign keys. It only ever runs catalogue queries: no table's rows are read, so no personal data leaves the
/// database through it.
/// </summary>
public static class SchemaReader
{
    public const int TimeoutSeconds = 20;

    public static DbConnection Open(DbProvider provider, string connectionString)
    {
        DbConnection c = provider switch
        {
            DbProvider.SqlServer => new SqlConnection(connectionString),
            DbProvider.PostgreSql => new NpgsqlConnection(connectionString),
            DbProvider.MySql => new MySqlConnection(connectionString),
            DbProvider.Oracle => new OracleConnection(connectionString),
            DbProvider.Sqlite => new SqliteConnection(connectionString),
            _ => new OdbcConnection(connectionString),
        };
        try
        {
            c.Open();
            return c;
        }
        catch
        {
            c.Dispose();
            throw;
        }
    }

    /// <summary>Connects and says what it connected to ("PostgreSQL 18.1"), or throws why it couldn't.</summary>
    public static string Test(DbProvider provider, string connectionString)
    {
        using var c = Open(provider, connectionString);
        return $"{SqlDialect.For(provider).DisplayName} {c.ServerVersion}".Trim();
    }

    public static List<TableRef> ListTables(DbProvider provider, string connectionString, string? schema = null)
    {
        using var c = Open(provider, connectionString);
        return ListTablesOn(c, provider, schema).OrderBy(t => t.Schema).ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>One table's columns, primary key and foreign keys, as an entity.</summary>
    public static EntityModel Describe(DbProvider provider, string connectionString, string table, string? source = null)
    {
        var (schema, name) = SourceReaders.SplitName(table);
        using var c = Open(provider, connectionString);
        var match = ListMatching(c, provider, schema, name);
        var entity = new EntityModel
        {
            Name = Identifiers.Pascal(Words.Singular(name)),
            Table = match.Name,
            Schema = match.Schema,
            Source = source ?? $"{provider.ToString().ToLowerInvariant()}:{match.FullName}",
        };
        foreach (var col in Columns(c, provider, match)) entity.Fields.Add(col);
        if (entity.Fields.Count == 0) throw new InvalidOperationException($"{match.FullName} has no columns the designer could read.");
        foreach (var key in PrimaryKey(c, provider, match))
            if (entity.Fields.FirstOrDefault(f => f.ColumnName.Equals(key, StringComparison.OrdinalIgnoreCase)) is { } f)
            {
                f.IsKey = true;
                f.Nullable = false;
            }
        var all = ListTablesOn(c, provider, match.Schema);
        foreach (var (column, references) in ForeignKeys(c, provider, match))
            if (entity.Fields.FirstOrDefault(f => f.ColumnName.Equals(column, StringComparison.OrdinalIgnoreCase)) is { } f)
            {
                f.References = references;
                // What a lookup list shows: the referenced table's key and the first text column after it.
                if (all.FirstOrDefault(t => t.Name.Equals(references, StringComparison.OrdinalIgnoreCase)) is { } target)
                {
                    var key = PrimaryKey(c, provider, target);
                    var cols = Columns(c, provider, target).ToList();
                    if (key.Count == 1)
                    {
                        f.ReferencesKey = key[0];
                        f.ReferencesSchema = target.Schema;
                        f.ReferencesDisplay = cols.FirstOrDefault(x => x.Type == "string" && x.ColumnName.Contains("name", StringComparison.OrdinalIgnoreCase))?.ColumnName
                                              ?? cols.FirstOrDefault(x => x.Type == "string")?.ColumnName ?? key[0];
                    }
                }
            }
        foreach (var f in entity.Fields) f.Required = !f.Nullable && !f.IsGenerated && !f.IsComputed;
        return entity.Normalise();
    }

    /// <summary>The table as the catalogue spells it (case differs between engines), or an error listing near misses.</summary>
    private static TableRef ListMatching(DbConnection c, DbProvider provider, string? schema, string name)
    {
        var all = ListTablesOn(c, provider, schema);
        var exact = all.FirstOrDefault(t => t.Name == name) ?? all.FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (exact != null) return exact;
        var near = all.Where(t => t.Name.Contains(name, StringComparison.OrdinalIgnoreCase) || name.Contains(t.Name, StringComparison.OrdinalIgnoreCase)).Take(8).ToList();
        throw new ArgumentException($"There's no table or view \"{name}\"{(schema is null ? "" : $" in {schema}")}." +
                                    (near.Count > 0 ? $" Did you mean {string.Join(", ", near.Select(t => t.FullName))}?" : " list_tables shows what there is."));
    }

    private static List<TableRef> ListTablesOn(DbConnection c, DbProvider provider, string? schema)
    {
        return provider == DbProvider.Odbc ? ListOdbc(c, schema) : ListWith(c, provider, schema);
    }

    private static List<TableRef> ListOdbc(DbConnection c, string? schema)
    {
        var list = new List<TableRef>();
        foreach (DataRow r in c.GetSchema("Tables").Rows)
        {
            var s = r.Table.Columns.Contains("TABLE_SCHEM") ? Convert.ToString(r["TABLE_SCHEM"]) : null;
            if (schema != null && !string.Equals(s, schema, StringComparison.OrdinalIgnoreCase)) continue;
            var type = Convert.ToString(r["TABLE_TYPE"]) ?? "";
            if (!type.Contains("TABLE", StringComparison.OrdinalIgnoreCase) && !type.Contains("VIEW", StringComparison.OrdinalIgnoreCase) || type.Contains("SYSTEM", StringComparison.OrdinalIgnoreCase)) continue;
            list.Add(new TableRef(string.IsNullOrEmpty(s) ? null : s, Convert.ToString(r["TABLE_NAME"])!, type.Contains("VIEW", StringComparison.OrdinalIgnoreCase)));
        }
        return list;
    }

    private static List<TableRef> ListWith(DbConnection c, DbProvider provider, string? schema)
    {
        var sql = provider switch
        {
            DbProvider.SqlServer => "SELECT s.name, o.name, CASE o.type WHEN 'V' THEN 'VIEW' ELSE 'TABLE' END FROM sys.objects o JOIN sys.schemas s ON s.schema_id = o.schema_id WHERE o.type IN ('U', 'V') AND o.is_ms_shipped = 0 AND (@s IS NULL OR s.name = @s)",
            DbProvider.PostgreSql => "SELECT table_schema, table_name, table_type FROM information_schema.tables WHERE (@s::text IS NULL OR table_schema = @s) AND table_schema NOT IN ('pg_catalog', 'information_schema')",
            DbProvider.MySql => "SELECT TABLE_SCHEMA, TABLE_NAME, TABLE_TYPE FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = COALESCE(@s, DATABASE())",
            DbProvider.Oracle => "SELECT OWNER, TABLE_NAME, 'TABLE' FROM ALL_TABLES WHERE OWNER = NVL(:s, USER) UNION ALL SELECT OWNER, VIEW_NAME, 'VIEW' FROM ALL_VIEWS WHERE OWNER = NVL(:s, USER)",
            DbProvider.Sqlite => "SELECT NULL, name, type FROM sqlite_master WHERE type IN ('table', 'view') AND name NOT LIKE 'sqlite_%'",
            _ => "SELECT TABSCHEMA, TABNAME, TYPE FROM SYSCAT.TABLES WHERE TABSCHEMA = COALESCE(CAST(? AS VARCHAR(128)), CURRENT SCHEMA) AND TYPE IN ('T', 'V')",
        };
        var list = new List<TableRef>();
        using var cmd = Command(c, provider, sql, ("s", schema));
        using var r = cmd.ExecuteReader();
        while (r.Read())
            list.Add(new TableRef(r.IsDBNull(0) ? null : r.GetValue(0).ToString()!.Trim(), r.GetValue(1).ToString()!.Trim(), (r.GetValue(2)?.ToString() ?? "").Contains("VIEW") || r.GetValue(2)?.ToString() == "V"));
        return list;
    }

    private static IEnumerable<FieldModel> Columns(DbConnection c, DbProvider provider, TableRef t)
    {
        var sql = provider switch
        {
            DbProvider.SqlServer => """
                SELECT c.COLUMN_NAME, c.DATA_TYPE, c.IS_NULLABLE, c.CHARACTER_MAXIMUM_LENGTH, c.NUMERIC_PRECISION, c.NUMERIC_SCALE,
                       COLUMNPROPERTY(OBJECT_ID(QUOTENAME(c.TABLE_SCHEMA) + '.' + QUOTENAME(c.TABLE_NAME)), c.COLUMN_NAME, 'IsIdentity'),
                       COLUMNPROPERTY(OBJECT_ID(QUOTENAME(c.TABLE_SCHEMA) + '.' + QUOTENAME(c.TABLE_NAME)), c.COLUMN_NAME, 'IsComputed')
                FROM INFORMATION_SCHEMA.COLUMNS c WHERE c.TABLE_SCHEMA = @s AND c.TABLE_NAME = @t ORDER BY c.ORDINAL_POSITION
                """,
            DbProvider.PostgreSql => """
                SELECT column_name, data_type, is_nullable, character_maximum_length, numeric_precision, numeric_scale,
                       CASE WHEN is_identity = 'YES' OR column_default LIKE 'nextval(%' THEN 1 ELSE 0 END,
                       CASE WHEN is_generated = 'ALWAYS' THEN 1 ELSE 0 END
                FROM information_schema.columns WHERE table_schema = @s AND table_name = @t ORDER BY ordinal_position
                """,
            DbProvider.MySql => """
                SELECT COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE, CHARACTER_MAXIMUM_LENGTH, NUMERIC_PRECISION, NUMERIC_SCALE,
                       CASE WHEN EXTRA LIKE '%auto_increment%' THEN 1 ELSE 0 END,
                       CASE WHEN EXTRA LIKE '%GENERATED%' THEN 1 ELSE 0 END
                FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = @s AND TABLE_NAME = @t ORDER BY ORDINAL_POSITION
                """,
            DbProvider.Oracle => """
                SELECT COLUMN_NAME, DATA_TYPE, NULLABLE, CHAR_LENGTH, DATA_PRECISION, DATA_SCALE,
                       CASE WHEN IDENTITY_COLUMN = 'YES' THEN 1 ELSE 0 END, CASE WHEN VIRTUAL_COLUMN = 'YES' THEN 1 ELSE 0 END
                FROM ALL_TAB_COLS WHERE OWNER = :s AND TABLE_NAME = :t AND HIDDEN_COLUMN = 'NO' ORDER BY COLUMN_ID
                """,
            DbProvider.Sqlite => "SELECT name, type, CASE WHEN \"notnull\" = 1 THEN 'NO' ELSE 'YES' END, NULL, NULL, NULL, pk, 0 FROM pragma_table_info(@t)",
            DbProvider.Db2 => """
                SELECT COLNAME, TYPENAME, NULLS, LENGTH, LENGTH, SCALE, CASE WHEN IDENTITY = 'Y' THEN 1 ELSE 0 END, CASE WHEN GENERATED = 'A' AND IDENTITY = 'N' THEN 1 ELSE 0 END
                FROM SYSCAT.COLUMNS WHERE TABSCHEMA = ? AND TABNAME = ? ORDER BY COLNO
                """,
            _ => null,
        };
        if (sql is null)
        {
            foreach (DataRow r in c.GetSchema("Columns", [null, t.Schema, t.Name]).Rows)
            {
                var type = Convert.ToString(r["TYPE_NAME"]) ?? "varchar";
                yield return new FieldModel
                {
                    Name = Identifiers.Pascal(Convert.ToString(r["COLUMN_NAME"])!),
                    Column = Convert.ToString(r["COLUMN_NAME"]),
                    Type = TypeMap.FromSql(type),
                    Nullable = Convert.ToString(r["IS_NULLABLE"]) != "NO",
                    MaxLength = TypeMap.FromSql(type) == "string" && r["COLUMN_SIZE"] is not DBNull ? Convert.ToInt32(r["COLUMN_SIZE"]) : null,
                };
            }
            yield break;
        }
        // Several engines spell a table's schema differently: SQLite has none, and an Oracle or Db2 owner is upper case.
        var sqliteSingleIntegerKey = provider == DbProvider.Sqlite && SqliteIntegerKey(c, t.Name);
        using var cmd = provider == DbProvider.Sqlite ? Command(c, provider, sql, ("t", t.Name)) : Command(c, provider, sql, ("s", t.Schema), ("t", t.Name));
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var column = reader.GetValue(0).ToString()!.Trim();
            var type = reader.GetValue(1)?.ToString()?.Trim() ?? "";
            int? Int(int i) => reader.IsDBNull(i) ? null : Convert.ToInt32(reader.GetValue(i));
            var precision = Int(4);
            var scale = Int(5);
            var nullable = (reader.GetValue(2)?.ToString() ?? "YES").Trim() is "YES" or "Y";
            var generated = Int(6) is > 0 && (provider != DbProvider.Sqlite || sqliteSingleIntegerKey);
            var rowVersion = provider == DbProvider.SqlServer && type is "timestamp" or "rowversion";
            var clr = rowVersion ? "byte[]"
                : provider == DbProvider.MySql && type.StartsWith("tinyint(1)", StringComparison.OrdinalIgnoreCase) ? "bool"
                : provider == DbProvider.Oracle && type.Equals("NUMBER", StringComparison.OrdinalIgnoreCase) && precision == 1 && scale is null or 0 ? "bool"
                : provider == DbProvider.Sqlite ? SqliteAffinity(type)
                : TypeMap.FromSql(type, precision, scale);
            var length = Int(3);
            var f = new FieldModel
            {
                Name = Identifiers.Pascal(column),
                Column = column,
                Type = clr,
                Nullable = nullable,
                IsGenerated = generated,
                IsComputed = Int(7) is > 0 || rowVersion,
                MaxLength = clr == "string" && length is > 0 and < 1_000_000 ? length
                    : provider == DbProvider.Sqlite && clr == "string" && System.Text.RegularExpressions.Regex.Match(type, @"\(\s*(\d+)\s*\)") is { Success: true } m ? int.Parse(m.Groups[1].Value) : null,
            };
            if (provider == DbProvider.MySql && type.StartsWith("enum(", StringComparison.OrdinalIgnoreCase))
            {
                f.Options = System.Text.RegularExpressions.Regex.Matches(type, @"'((?:[^']|'')*)'").Select(m => m.Groups[1].Value.Replace("''", "'")).ToList();
                f.Editor = "choice";
            }
            yield return f;
        }
    }

    private static bool SqliteIntegerKey(DbConnection c, string table)
    {
        using var cmd = Command(c, DbProvider.Sqlite, "SELECT type FROM pragma_table_info(@t) WHERE pk > 0", ("t", table));
        using var r = cmd.ExecuteReader();
        var types = new List<string>();
        while (r.Read()) types.Add(r.GetString(0));
        return types.Count == 1 && types[0].Equals("INTEGER", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>SQLite's type affinity rules, mapped to C#.</summary>
    private static string SqliteAffinity(string declared)
    {
        var t = declared.ToUpperInvariant();
        if (t.Contains("INT")) return t.StartsWith("BIGINT", StringComparison.Ordinal) ? "long" : t == "INTEGER" ? "long" : "int";
        if (t.Contains("CHAR") || t.Contains("CLOB") || t.Contains("TEXT")) return "string";
        if (t.Contains("BLOB") || t.Length == 0) return t.Length == 0 ? "string" : "byte[]";
        if (t.Contains("REAL") || t.Contains("FLOA") || t.Contains("DOUB")) return "double";
        if (t.Contains("BOOL")) return "bool";
        if (t.Contains("DATETIME") || t.Contains("TIMESTAMP")) return "DateTime";
        if (t.Contains("DATE")) return "DateOnly";
        if (t.Contains("DEC") || t.Contains("NUM") || t.Contains("MONEY")) return "decimal";
        return "string";
    }

    private static List<string> PrimaryKey(DbConnection c, DbProvider provider, TableRef t)
    {
        var sql = provider switch
        {
            DbProvider.SqlServer or DbProvider.PostgreSql or DbProvider.MySql => """
                SELECT k.COLUMN_NAME FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE k ON k.CONSTRAINT_NAME = tc.CONSTRAINT_NAME AND k.TABLE_SCHEMA = tc.TABLE_SCHEMA AND k.TABLE_NAME = tc.TABLE_NAME
                WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY' AND tc.TABLE_SCHEMA = @s AND tc.TABLE_NAME = @t ORDER BY k.ORDINAL_POSITION
                """,
            DbProvider.Oracle => """
                SELECT cc.COLUMN_NAME FROM ALL_CONSTRAINTS c JOIN ALL_CONS_COLUMNS cc ON cc.OWNER = c.OWNER AND cc.CONSTRAINT_NAME = c.CONSTRAINT_NAME
                WHERE c.CONSTRAINT_TYPE = 'P' AND c.OWNER = :s AND c.TABLE_NAME = :t ORDER BY cc.POSITION
                """,
            DbProvider.Sqlite => "SELECT name FROM pragma_table_info(@t) WHERE pk > 0 ORDER BY pk",
            DbProvider.Db2 => "SELECT COLNAME FROM SYSCAT.KEYCOLUSE k JOIN SYSCAT.TABCONST c ON c.CONSTNAME = k.CONSTNAME AND c.TABSCHEMA = k.TABSCHEMA AND c.TABNAME = k.TABNAME WHERE c.TYPE = 'P' AND k.TABSCHEMA = ? AND k.TABNAME = ? ORDER BY k.COLSEQ",
            _ => null,
        };
        if (sql is null) return [];
        using var cmd = provider == DbProvider.Sqlite ? Command(c, provider, sql, ("t", t.Name)) : Command(c, provider, sql, ("s", t.Schema), ("t", t.Name));
        using var r = cmd.ExecuteReader();
        var list = new List<string>();
        while (r.Read()) list.Add(r.GetValue(0).ToString()!.Trim());
        return list;
    }

    private static List<(string Column, string References)> ForeignKeys(DbConnection c, DbProvider provider, TableRef t)
    {
        var sql = provider switch
        {
            DbProvider.SqlServer => """
                SELECT COL_NAME(fc.parent_object_id, fc.parent_column_id), OBJECT_NAME(fc.referenced_object_id)
                FROM sys.foreign_key_columns fc WHERE fc.parent_object_id = OBJECT_ID(QUOTENAME(@s) + '.' + QUOTENAME(@t))
                """,
            DbProvider.PostgreSql => """
                SELECT kcu.column_name, ccu.table_name FROM information_schema.table_constraints tc
                JOIN information_schema.key_column_usage kcu ON kcu.constraint_name = tc.constraint_name AND kcu.table_schema = tc.table_schema
                JOIN information_schema.constraint_column_usage ccu ON ccu.constraint_name = tc.constraint_name AND ccu.constraint_schema = tc.table_schema
                WHERE tc.constraint_type = 'FOREIGN KEY' AND tc.table_schema = @s AND tc.table_name = @t
                """,
            DbProvider.MySql => "SELECT COLUMN_NAME, REFERENCED_TABLE_NAME FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE WHERE TABLE_SCHEMA = @s AND TABLE_NAME = @t AND REFERENCED_TABLE_NAME IS NOT NULL",
            DbProvider.Oracle => """
                SELECT cc.COLUMN_NAME, rc.TABLE_NAME FROM ALL_CONSTRAINTS c
                JOIN ALL_CONS_COLUMNS cc ON cc.OWNER = c.OWNER AND cc.CONSTRAINT_NAME = c.CONSTRAINT_NAME
                JOIN ALL_CONSTRAINTS rc ON rc.OWNER = c.R_OWNER AND rc.CONSTRAINT_NAME = c.R_CONSTRAINT_NAME
                WHERE c.CONSTRAINT_TYPE = 'R' AND c.OWNER = :s AND c.TABLE_NAME = :t
                """,
            DbProvider.Sqlite => "SELECT \"from\", \"table\" FROM pragma_foreign_key_list(@t)",
            DbProvider.Db2 => "SELECT k.COLNAME, r.REFTABNAME FROM SYSCAT.REFERENCES r JOIN SYSCAT.KEYCOLUSE k ON k.CONSTNAME = r.CONSTNAME AND k.TABSCHEMA = r.TABSCHEMA AND k.TABNAME = r.TABNAME WHERE r.TABSCHEMA = ? AND r.TABNAME = ?",
            _ => null,
        };
        if (sql is null) return [];
        using var cmd = provider == DbProvider.Sqlite ? Command(c, provider, sql, ("t", t.Name)) : Command(c, provider, sql, ("s", t.Schema), ("t", t.Name));
        using var r = cmd.ExecuteReader();
        var list = new List<(string, string)>();
        while (r.Read()) list.Add((r.GetValue(0).ToString()!.Trim(), r.GetValue(1).ToString()!.Trim()));
        return list;
    }

    /// <summary>A command with parameters named for the engine (Oracle ":", ODBC by position).</summary>
    private static DbCommand Command(DbConnection c, DbProvider provider, string sql, params (string Name, string? Value)[] parameters)
    {
        var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandTimeout = TimeoutSeconds;
        if (cmd is OracleCommand oc) oc.BindByName = true;
        var dialect = SqlDialect.For(provider);
        foreach (var (name, value) in parameters)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = dialect.Positional ? name : dialect.ParameterPrefix + name;
            p.Value = (object?)value ?? DBNull.Value;
            p.DbType = DbType.String;
            if (!dialect.Positional && cmd.Parameters.Contains(p.ParameterName)) continue;
            cmd.Parameters.Add(p);
        }
        return cmd;
    }
}
