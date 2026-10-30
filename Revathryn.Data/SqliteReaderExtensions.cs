using Microsoft.Data.Sqlite;

namespace Revathryn.Data
{
    /// <summary>Reading helpers for nullable columns on <see cref="SqliteDataReader"/>.</summary>
    internal static class SqliteReaderExtensions
    {
        /// <summary>Reads a nullable long column.</summary>
        internal static long? GetNullableInt64(this SqliteDataReader reader, int ordinal) =>
            reader.IsDBNull(ordinal) ? null : reader.GetInt64(ordinal);

        /// <summary>Reads a nullable string column.</summary>
        internal static string GetNullableString(this SqliteDataReader reader, int ordinal) =>
            reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }
}
