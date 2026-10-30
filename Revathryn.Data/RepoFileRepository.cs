using Microsoft.Data.Sqlite;

using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;

namespace Revathryn.Data
{
    /// <summary>SQLite implementation of <see cref="IRepoFileRepository"/>.</summary>
    public sealed class RepoFileRepository : SqliteRepositoryBase, IRepoFileRepository
    {
        private const string SelectColumns =
            "Id, RepositoryId, RelativePath, FileName, Extension, LastModifiedDate, Sha256, FileSize, IsExcluded, IsRemoved, ProjectPath, LastDiscoveredDate";

        /// <summary>Initialises a new repository.</summary>
        public RepoFileRepository(SqliteConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        /// <summary>Returns the file with the given id, or null when not found.</summary>
        public async Task<RepoFile> GetByIdAsync(long id, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM RepoFiles WHERE Id = @Id LIMIT 1;";
            command.Parameters.AddWithValue("@Id", id);

            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return MapRepoFile(reader);
            }

            return null;
        }

        /// <summary>Returns all files belonging to a repository.</summary>
        public async Task<IReadOnlyList<RepoFile>> GetByRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM RepoFiles WHERE RepositoryId = @RepositoryId ORDER BY RelativePath ASC;";
            command.Parameters.AddWithValue("@RepositoryId", repositoryId);

            List<RepoFile> results = [];
            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(MapRepoFile(reader));
            }

            return results;
        }

        /// <summary>Returns the file with the given repository-relative path, or null when not found.</summary>
        public async Task<RepoFile> GetByRelativePathAsync(long repositoryId, string relativePath, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM RepoFiles WHERE RepositoryId = @RepositoryId AND RelativePath = @RelativePath LIMIT 1;";
            command.Parameters.AddWithValue("@RepositoryId", repositoryId);
            command.Parameters.AddWithValue("@RelativePath", relativePath);

            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return MapRepoFile(reader);
            }

            return null;
        }

        /// <summary>Inserts or updates the file and returns its id.</summary>
        public async Task<long> UpsertAsync(RepoFile file, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO RepoFiles (RepositoryId, RelativePath, FileName, Extension, LastModifiedDate, Sha256, FileSize, IsExcluded, IsRemoved, ProjectPath, LastDiscoveredDate)
                VALUES (@RepositoryId, @RelativePath, @FileName, @Extension, @LastModifiedDate, @Sha256, @FileSize, @IsExcluded, @IsRemoved, @ProjectPath, @LastDiscoveredDate)
                ON CONFLICT (RepositoryId, RelativePath) DO UPDATE SET
                    FileName = excluded.FileName,
                    Extension = excluded.Extension,
                    LastModifiedDate = excluded.LastModifiedDate,
                    Sha256 = excluded.Sha256,
                    FileSize = excluded.FileSize,
                    IsExcluded = excluded.IsExcluded,
                    IsRemoved = excluded.IsRemoved,
                    ProjectPath = excluded.ProjectPath,
                    LastDiscoveredDate = excluded.LastDiscoveredDate;
                SELECT Id FROM RepoFiles WHERE RepositoryId = @RepositoryId AND RelativePath = @RelativePath;
                """;
            AddParameters(command, file);

            object result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return (long)result;
        }

        /// <summary>Reconciles discovered files against the repository inventory atomically.</summary>
        public async Task<RepositoryReconciliationResult> ReconcileAsync(
            long repositoryId,
            IReadOnlyList<RepoFile> discoveredFiles,
            IReadOnlyCollection<string> failedPaths,
            CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteTransaction transaction = connection.BeginTransaction();

            Dictionary<string, RepoFile> existing = await LoadInventoryAsync(connection, transaction, repositoryId, cancellationToken).ConfigureAwait(false);
            HashSet<string> discoveredPaths = new(StringComparer.Ordinal);
            HashSet<string> failedSet = failedPaths is null
                ? new HashSet<string>(StringComparer.Ordinal)
                : new HashSet<string>(failedPaths, StringComparer.Ordinal);

            RepositoryReconciliationResult result = new();

            foreach (RepoFile discovered in discoveredFiles)
            {
                discovered.RepositoryId = repositoryId;
                discovered.IsRemoved = false;
                discoveredPaths.Add(discovered.RelativePath);

                if (existing.TryGetValue(discovered.RelativePath, out RepoFile current))
                {
                    discovered.Id = current.Id;
                    discovered.IsExcluded = current.IsExcluded;
                    discovered.ProjectPath = string.IsNullOrEmpty(discovered.ProjectPath) ? current.ProjectPath : discovered.ProjectPath;

                    if (string.Equals(current.Sha256, discovered.Sha256, StringComparison.Ordinal))
                    {
                        result.UnchangedFiles++;
                    }
                    else
                    {
                        result.ChangedFiles++;
                    }
                }
                else
                {
                    result.NewFiles++;
                    discovered.IsExcluded = false;
                }

                await UpsertFileAsync(connection, transaction, discovered, cancellationToken).ConfigureAwait(false);
            }

            foreach (RepoFile current in existing.Values)
            {
                if (discoveredPaths.Contains(current.RelativePath) || failedSet.Contains(current.RelativePath) || current.IsRemoved)
                {
                    continue;
                }

                result.RemovedFiles++;
                await MarkRemovedAsync(connection, transaction, current.Id, cancellationToken).ConfigureAwait(false);
            }

            transaction.Commit();
            return result;
        }

        /// <summary>Updates an existing file.</summary>
        public async Task UpdateAsync(RepoFile file, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = """
                UPDATE RepoFiles
                SET RepositoryId = @RepositoryId,
                    RelativePath = @RelativePath,
                    FileName = @FileName,
                    Extension = @Extension,
                    LastModifiedDate = @LastModifiedDate,
                    Sha256 = @Sha256,
                    FileSize = @FileSize,
                    IsExcluded = @IsExcluded,
                    IsRemoved = @IsRemoved,
                    ProjectPath = @ProjectPath,
                    LastDiscoveredDate = @LastDiscoveredDate
                WHERE Id = @Id;
                """;
            AddParameters(command, file);
            command.Parameters.AddWithValue("@Id", file.Id);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Deletes the file with the given id.</summary>
        public async Task DeleteAsync(long id, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM RepoFiles WHERE Id = @Id;";
            command.Parameters.AddWithValue("@Id", id);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Deletes all files belonging to a repository.</summary>
        public async Task DeleteByRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = "DELETE FROM RepoFiles WHERE RepositoryId = @RepositoryId;";
            command.Parameters.AddWithValue("@RepositoryId", repositoryId);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private static async Task<Dictionary<string, RepoFile>> LoadInventoryAsync(
            SqliteConnection connection,
            SqliteTransaction transaction,
            long repositoryId,
            CancellationToken cancellationToken)
        {
            Dictionary<string, RepoFile> existing = new(StringComparer.Ordinal);

            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"SELECT {SelectColumns} FROM RepoFiles WHERE RepositoryId = @RepositoryId;";
            command.Parameters.AddWithValue("@RepositoryId", repositoryId);

            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                RepoFile file = MapRepoFile(reader);
                existing[file.RelativePath] = file;
            }

            return existing;
        }

        private static async Task UpsertFileAsync(SqliteConnection connection, SqliteTransaction transaction, RepoFile file, CancellationToken cancellationToken)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO RepoFiles (RepositoryId, RelativePath, FileName, Extension, LastModifiedDate, Sha256, FileSize, IsExcluded, IsRemoved, ProjectPath, LastDiscoveredDate)
                VALUES (@RepositoryId, @RelativePath, @FileName, @Extension, @LastModifiedDate, @Sha256, @FileSize, @IsExcluded, @IsRemoved, @ProjectPath, @LastDiscoveredDate)
                ON CONFLICT (RepositoryId, RelativePath) DO UPDATE SET
                    FileName = excluded.FileName,
                    Extension = excluded.Extension,
                    LastModifiedDate = excluded.LastModifiedDate,
                    Sha256 = excluded.Sha256,
                    FileSize = excluded.FileSize,
                    IsExcluded = excluded.IsExcluded,
                    IsRemoved = excluded.IsRemoved,
                    ProjectPath = excluded.ProjectPath,
                    LastDiscoveredDate = excluded.LastDiscoveredDate;
                """;
            AddParameters(command, file);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private static async Task MarkRemovedAsync(SqliteConnection connection, SqliteTransaction transaction, long id, CancellationToken cancellationToken)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "UPDATE RepoFiles SET IsRemoved = 1 WHERE Id = @Id;";
            command.Parameters.AddWithValue("@Id", id);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private static void AddParameters(SqliteCommand command, RepoFile file)
        {
            command.Parameters.AddWithValue("@RepositoryId", file.RepositoryId);
            command.Parameters.AddWithValue("@RelativePath", file.RelativePath);
            command.Parameters.AddWithValue("@FileName", file.FileName);
            command.Parameters.AddWithValue("@Extension", file.Extension);
            command.Parameters.AddWithValue("@LastModifiedDate", SqliteValueConverter.ToText(file.LastModifiedDate));
            command.Parameters.AddWithValue("@Sha256", file.Sha256);
            command.Parameters.AddWithValue("@FileSize", file.FileSize);
            command.Parameters.AddWithValue("@IsExcluded", file.IsExcluded);
            command.Parameters.AddWithValue("@IsRemoved", file.IsRemoved);
            command.Parameters.AddWithValue("@ProjectPath", file.ProjectPath);
            command.Parameters.AddWithValue("@LastDiscoveredDate", SqliteValueConverter.ToText(file.LastDiscoveredDate));
        }

        private static RepoFile MapRepoFile(SqliteDataReader reader)
        {
            return new RepoFile
            {
                Id = reader.GetInt64(0),
                RepositoryId = reader.GetInt64(1),
                RelativePath = reader.GetString(2),
                FileName = reader.GetString(3),
                Extension = reader.GetString(4),
                LastModifiedDate = SqliteValueConverter.ToDateTime(reader.GetString(5)),
                Sha256 = reader.GetString(6),
                FileSize = reader.GetInt64(7),
                IsExcluded = reader.GetBoolean(8),
                IsRemoved = reader.GetBoolean(9),
                ProjectPath = reader.GetString(10),
                LastDiscoveredDate = SqliteValueConverter.ToDateTime(reader.GetString(11))
            };
        }
    }
}
