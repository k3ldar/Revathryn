using System.Globalization;

namespace Revathryn.Data
{
    /// <summary>Converts between model values and SQLite storage representations.</summary>
    internal static class SqliteValueConverter
    {
        /// <summary>Formats a UTC timestamp as a sortable, round-trippable ISO 8601 string.</summary>
        internal static string ToText(DateTime value) =>
            value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);

        /// <summary>Parses an ISO 8601 string back into a UTC timestamp.</summary>
        internal static DateTime ToDateTime(string value) =>
            DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind).ToUniversalTime();

        /// <summary>Maps a nullable string to a parameter value.</summary>
        internal static object ToParameter(string value) => value is null ? DBNull.Value : value;

        /// <summary>Maps a nullable long to a parameter value.</summary>
        internal static object ToParameter(long? value) => value.HasValue ? value.Value : DBNull.Value;

        /// <summary>Maps a nullable int to a parameter value.</summary>
        internal static object ToParameter(int? value) => value.HasValue ? value.Value : DBNull.Value;

        /// <summary>Maps a nullable UTC timestamp to a sortable text parameter.</summary>
        internal static object ToParameter(DateTime? value) => value.HasValue ? ToText(value.Value) : DBNull.Value;
    }
}
