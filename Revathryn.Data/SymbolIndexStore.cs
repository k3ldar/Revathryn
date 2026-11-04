using System.Text.Json;

using Microsoft.Data.Sqlite;

using Revathryn.Contracts;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;

namespace Revathryn.Data
{
    /// <summary>SQLite implementation of <see cref="ISymbolIndexStore"/> for a single repository.</summary>
    public sealed class SymbolIndexStore : ISymbolIndexStore
    {
        private const int BusyTimeoutMilliseconds = 30000;
        private const string SelectSymbolColumns = "Id, SourcePath, Name, FullyQualifiedName, Kind, LineNumber, EndLineNumber, Namespace, Accessibility, IsStatic, IsAbstract, IsSealed, DependenciesJson";
        private const string SelectMemberColumns = "SymbolId, Kind, Name, Signature, LineNumber, EndLineNumber, Accessibility, IsStatic";

        private readonly string _analysisOutputDirectory;

        /// <summary>Initialises a new store rooted at the supplied analysis output directory.</summary>
        /// <param name="analysisOutputDirectory">Directory under which per-repository index databases are created.</param>
        public SymbolIndexStore(string analysisOutputDirectory)
        {
            if (string.IsNullOrWhiteSpace(analysisOutputDirectory))
            {
                throw new ArgumentException("An analysis output directory is required.", nameof(analysisOutputDirectory));
            }

            _analysisOutputDirectory = analysisOutputDirectory;
        }

        /// <summary>Replaces the indexed symbols for a single source file in the repository's index.</summary>
        public async Task UpsertFileAsync(string repositoryName, IndexedFile file, IReadOnlyList<IndexedSymbol> symbols, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(file);
            ArgumentNullException.ThrowIfNull(symbols);

            using SqliteConnection connection = await OpenConnectionAsync(repositoryName, cancellationToken).ConfigureAwait(false);
            EnsureSchema(connection);

            using SqliteTransaction transaction = connection.BeginTransaction();

            await DeleteFileCoreAsync(connection, transaction, file.SourcePath, cancellationToken).ConfigureAwait(false);

            using (SqliteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
                    INSERT INTO Files (SourcePath, SourceHash, SourceLastWriteUtc, AnalysedUtc, AnalyserVersion, Language, LanguageVersion)
                    VALUES (@SourcePath, @SourceHash, @SourceLastWriteUtc, @AnalysedUtc, @AnalyserVersion, @Language, @LanguageVersion);
                    """;
                command.Parameters.AddWithValue("@SourcePath", file.SourcePath ?? string.Empty);
                command.Parameters.AddWithValue("@SourceHash", file.SourceHash ?? string.Empty);
                command.Parameters.AddWithValue("@SourceLastWriteUtc", SqliteValueConverter.ToText(file.SourceLastWriteUtc));
                command.Parameters.AddWithValue("@AnalysedUtc", SqliteValueConverter.ToText(file.AnalysedUtc));
                command.Parameters.AddWithValue("@AnalyserVersion", file.AnalyserVersion);
                command.Parameters.AddWithValue("@Language", file.Language ?? string.Empty);
                command.Parameters.AddWithValue("@LanguageVersion", file.LanguageVersion ?? string.Empty);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            foreach (IndexedSymbol symbol in symbols)
            {
                long symbolId = await InsertSymbolAsync(connection, transaction, file.SourcePath, symbol, cancellationToken).ConfigureAwait(false);

                foreach (IndexedMember member in symbol.Members ?? [])
                {
                    await InsertMemberAsync(connection, transaction, symbolId, member, cancellationToken).ConfigureAwait(false);
                }
            }

            transaction.Commit();
        }

        /// <summary>Removes a single source file and its symbols from the repository's index.</summary>
        public async Task DeleteFileAsync(string repositoryName, string sourcePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return;
            }

            using SqliteConnection connection = await OpenConnectionAsync(repositoryName, cancellationToken).ConfigureAwait(false);
            EnsureSchema(connection);

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Files WHERE SourcePath = @SourcePath;";
            command.Parameters.AddWithValue("@SourcePath", sourcePath);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Removes every indexed file and symbol for the repository.</summary>
        public async Task DeleteRepositoryAsync(string repositoryName, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(repositoryName, cancellationToken).ConfigureAwait(false);
            EnsureSchema(connection);

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Files;";
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Returns symbols defined in the given source file, including members and dependencies.</summary>
        public async Task<IReadOnlyList<IndexedSymbol>> GetSymbolsForFileAsync(string repositoryName, string sourcePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return [];
            }

            using SqliteConnection connection = await OpenConnectionAsync(repositoryName, cancellationToken).ConfigureAwait(false);
            EnsureSchema(connection);

            return await ReadSymbolsAsync(
                connection,
                "SourcePath = @SourcePath",
                [new SqliteParameter("@SourcePath", sourcePath)],
                cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Returns indexed symbols whose name or fully qualified name matches any of the supplied names.</summary>
        public async Task<IReadOnlyList<IndexedSymbol>> GetSymbolsByNamesAsync(string repositoryName, IReadOnlyCollection<string> names, CancellationToken cancellationToken = default)
        {
            List<string> cleaned = CleanNames(names);

            if (cleaned.Count == 0)
            {
                return [];
            }

            List<SqliteParameter> parameters = [];
            List<string> nameParameters = [];
            List<string> fullyQualifiedNameParameters = [];

            for (int index = 0; index < cleaned.Count; index++)
            {
                string nameParameter = $"@Name{index}";
                string fullyQualifiedNameParameter = $"@FullyQualifiedName{index}";
                parameters.Add(new SqliteParameter(nameParameter, cleaned[index]));
                parameters.Add(new SqliteParameter(fullyQualifiedNameParameter, cleaned[index]));
                nameParameters.Add(nameParameter);
                fullyQualifiedNameParameters.Add(fullyQualifiedNameParameter);
            }

            string whereClause = $"Name IN ({string.Join(", ", nameParameters)}) OR FullyQualifiedName IN ({string.Join(", ", fullyQualifiedNameParameters)})";

            using SqliteConnection connection = await OpenConnectionAsync(repositoryName, cancellationToken).ConfigureAwait(false);
            EnsureSchema(connection);

            return await ReadSymbolsAsync(connection, whereClause, parameters, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Returns symbols that depend on any of the supplied target names.</summary>
        public async Task<IReadOnlyList<IndexedSymbol>> GetSymbolsReferencingAsync(string repositoryName, IReadOnlyCollection<string> targetNames, CancellationToken cancellationToken = default)
        {
            List<string> cleaned = CleanNames(targetNames);

            if (cleaned.Count == 0)
            {
                return [];
            }

            using SqliteConnection connection = await OpenConnectionAsync(repositoryName, cancellationToken).ConfigureAwait(false);
            EnsureSchema(connection);

            IReadOnlyList<IndexedSymbol> allSymbols = await ReadSymbolsAsync(connection, "1 = 1", [], cancellationToken).ConfigureAwait(false);

            List<IndexedSymbol> matches = [];

            foreach (IndexedSymbol symbol in allSymbols)
            {
                foreach (string dependency in symbol.Dependencies ?? [])
                {
                    if (MatchesAny(dependency, cleaned))
                    {
                        matches.Add(symbol);
                        break;
                    }
                }
            }

            return matches;
        }

        /// <summary>Returns symbols whose name, fully qualified name or namespace contains the search text.</summary>
        public async Task<IReadOnlyList<IndexedSymbol>> SearchSymbolsAsync(string repositoryName, string searchText, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                return [];
            }

            string term = searchText.Trim();
            string like = $"%{term}%";

            using SqliteConnection connection = await OpenConnectionAsync(repositoryName, cancellationToken).ConfigureAwait(false);
            EnsureSchema(connection);

            return await ReadSymbolsAsync(
                connection,
                "Name LIKE @Like OR FullyQualifiedName LIKE @Like OR Namespace LIKE @Like",
                [new SqliteParameter("@Like", like)],
                cancellationToken).ConfigureAwait(false);
        }

        private async Task<SqliteConnection> OpenConnectionAsync(string repositoryName, CancellationToken cancellationToken)
        {
            string databasePath = ResolveDatabasePath(repositoryName);
            string directory = Path.GetDirectoryName(databasePath);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            SqliteConnection connection = new($"{string.Format(Consts.DefaultDataStore, databasePath)};{Consts.SqlitePoolingDisabled}");
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            ConfigureConnection(connection);
            return connection;
        }

        private string ResolveDatabasePath(string repositoryName)
        {
            string safeName = RepositoryFileNaming.GetSafeName(repositoryName);
            string repositoryDirectory = Path.Combine(_analysisOutputDirectory, safeName);
            return Path.Combine(repositoryDirectory, Consts.SymbolIndexDatabaseFileName);
        }

        private static void ConfigureConnection(SqliteConnection connection)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"PRAGMA foreign_keys = ON; PRAGMA busy_timeout = {BusyTimeoutMilliseconds};";
            command.ExecuteNonQuery();
        }

        private static void EnsureSchema(SqliteConnection connection)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = string.Join(
                Environment.NewLine,
                SymbolIndexSchema.CreateFiles,
                SymbolIndexSchema.CreateSymbols,
                SymbolIndexSchema.CreateMembers,
                SymbolIndexSchema.CreateIndexes);
            command.ExecuteNonQuery();
        }

        private static async Task DeleteFileCoreAsync(SqliteConnection connection, SqliteTransaction transaction, string sourcePath, CancellationToken cancellationToken)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM Files WHERE SourcePath = @SourcePath;";
            command.Parameters.AddWithValue("@SourcePath", sourcePath ?? string.Empty);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private static async Task<long> InsertSymbolAsync(SqliteConnection connection, SqliteTransaction transaction, string sourcePath, IndexedSymbol symbol, CancellationToken cancellationToken)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO Symbols (SourcePath, Name, FullyQualifiedName, Kind, LineNumber, EndLineNumber, Namespace, Accessibility, IsStatic, IsAbstract, IsSealed, DependenciesJson)
                VALUES (@SourcePath, @Name, @FullyQualifiedName, @Kind, @LineNumber, @EndLineNumber, @Namespace, @Accessibility, @IsStatic, @IsAbstract, @IsSealed, @DependenciesJson);
                SELECT last_insert_rowid();
                """;
            command.Parameters.AddWithValue("@SourcePath", sourcePath ?? string.Empty);
            command.Parameters.AddWithValue("@Name", symbol.Name ?? string.Empty);
            command.Parameters.AddWithValue("@FullyQualifiedName", symbol.FullyQualifiedName ?? string.Empty);
            command.Parameters.AddWithValue("@Kind", symbol.Kind ?? string.Empty);
            command.Parameters.AddWithValue("@LineNumber", symbol.LineNumber);
            command.Parameters.AddWithValue("@EndLineNumber", symbol.EndLineNumber);
            command.Parameters.AddWithValue("@Namespace", symbol.Namespace ?? string.Empty);
            command.Parameters.AddWithValue("@Accessibility", symbol.Accessibility ?? string.Empty);
            command.Parameters.AddWithValue("@IsStatic", ToInteger(symbol.IsStatic));
            command.Parameters.AddWithValue("@IsAbstract", ToInteger(symbol.IsAbstract));
            command.Parameters.AddWithValue("@IsSealed", ToInteger(symbol.IsSealed));
            command.Parameters.AddWithValue("@DependenciesJson", SerializeDependencies(symbol.Dependencies));

            object result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return (long)result;
        }

        private static async Task InsertMemberAsync(SqliteConnection connection, SqliteTransaction transaction, long symbolId, IndexedMember member, CancellationToken cancellationToken)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO Members (SymbolId, Kind, Name, Signature, LineNumber, EndLineNumber, Accessibility, IsStatic)
                VALUES (@SymbolId, @Kind, @Name, @Signature, @LineNumber, @EndLineNumber, @Accessibility, @IsStatic);
                """;
            command.Parameters.AddWithValue("@SymbolId", symbolId);
            command.Parameters.AddWithValue("@Kind", member.Kind ?? string.Empty);
            command.Parameters.AddWithValue("@Name", member.Name ?? string.Empty);
            command.Parameters.AddWithValue("@Signature", member.Signature ?? string.Empty);
            command.Parameters.AddWithValue("@LineNumber", member.LineNumber);
            command.Parameters.AddWithValue("@EndLineNumber", member.EndLineNumber);
            command.Parameters.AddWithValue("@Accessibility", member.Accessibility ?? string.Empty);
            command.Parameters.AddWithValue("@IsStatic", ToInteger(member.IsStatic));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private static async Task<IReadOnlyList<IndexedSymbol>> ReadSymbolsAsync(
            SqliteConnection connection,
            string whereClause,
            IReadOnlyList<SqliteParameter> parameters,
            CancellationToken cancellationToken)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectSymbolColumns} FROM Symbols WHERE {whereClause} ORDER BY LineNumber ASC, Id ASC;";

            foreach (SqliteParameter parameter in parameters)
            {
                command.Parameters.Add(parameter);
            }

            List<IndexedSymbol> symbols = [];
            List<long> symbolIds = [];

            using (SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    symbolIds.Add(reader.GetInt64(0));
                    symbols.Add(MapSymbol(reader));
                }
            }

            if (symbols.Count > 0)
            {
                await LoadMembersAsync(connection, symbols, symbolIds, cancellationToken).ConfigureAwait(false);
            }

            return symbols;
        }

        private static async Task LoadMembersAsync(
            SqliteConnection connection,
            List<IndexedSymbol> symbols,
            List<long> symbolIds,
            CancellationToken cancellationToken)
        {
            Dictionary<long, IndexedSymbol> symbolsById = [];

            for (int index = 0; index < symbols.Count; index++)
            {
                symbolsById[symbolIds[index]] = symbols[index];
            }

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectMemberColumns} FROM Members WHERE SymbolId IN ({string.Join(", ", symbolIds)}) ORDER BY LineNumber ASC, Id ASC;";

            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                long symbolId = reader.GetInt64(0);

                if (symbolsById.TryGetValue(symbolId, out IndexedSymbol symbol))
                {
                    symbol.Members.Add(MapMember(reader));
                }
            }
        }

        private static IndexedSymbol MapSymbol(SqliteDataReader reader)
        {
            return new IndexedSymbol
            {
                FilePath = reader.GetString(1),
                Name = reader.GetString(2),
                FullyQualifiedName = reader.GetString(3),
                Kind = reader.GetString(4),
                LineNumber = reader.GetInt32(5),
                EndLineNumber = reader.GetInt32(6),
                Namespace = reader.GetString(7),
                Accessibility = reader.GetString(8),
                IsStatic = reader.GetInt32(9) == 1,
                IsAbstract = reader.GetInt32(10) == 1,
                IsSealed = reader.GetInt32(11) == 1,
                Dependencies = DeserializeDependencies(reader.GetString(12))
            };
        }

        private static IndexedMember MapMember(SqliteDataReader reader)
        {
            return new IndexedMember
            {
                Kind = reader.GetString(1),
                Name = reader.GetString(2),
                Signature = reader.GetString(3),
                LineNumber = reader.GetInt32(4),
                EndLineNumber = reader.GetInt32(5),
                Accessibility = reader.GetString(6),
                IsStatic = reader.GetInt32(7) == 1
            };
        }

        private static List<string> CleanNames(IReadOnlyCollection<string> names)
        {
            if (names is null || names.Count == 0)
            {
                return [];
            }

            return [.. names
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name.Trim())
                .Distinct(StringComparer.Ordinal)];
        }

        private static bool MatchesAny(string reference, List<string> targetNames)
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                return false;
            }

            string candidate = reference.Trim();

            foreach (string target in targetNames)
            {
                if (string.Equals(candidate, target, StringComparison.Ordinal))
                {
                    return true;
                }

                if (string.Equals(LastSegment(candidate), target, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string LastSegment(string value)
        {
            int index = value.LastIndexOf(Consts.CharFullStop);
            return index >= 0 ? value[(index + 1)..] : value;
        }

        private static string SerializeDependencies(List<string> dependencies)
        {
            return dependencies is null || dependencies.Count == 0 ? string.Empty : JsonSerializer.Serialize(dependencies);
        }

        private static List<string> DeserializeDependencies(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return [];
            }

            try
            {
                return JsonSerializer.Deserialize<List<string>>(json) ?? [];
            }
            catch (JsonException)
            {
                return [];
            }
        }

        private static int ToInteger(bool value)
        {
            return value ? 1 : 0;
        }
    }
}
