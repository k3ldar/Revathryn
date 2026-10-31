using Microsoft.Data.Sqlite;

using Revathryn.Contracts.Enums;
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
            HashSet<string> discoveredPaths = [with(StringComparer.Ordinal)];
            HashSet<string> failedSet = failedPaths is null
                ? [with(StringComparer.Ordinal)]
                : new HashSet<string>(failedPaths, StringComparer.Ordinal);

            RepositoryReconciliationResult result = new();
            List<RepositoryFileChange> changes = [];
            List<RepoFile> addedCandidates = [];
            List<RepoFile> removedCandidates = [];

            foreach (RepoFile discovered in discoveredFiles)
            {
                discovered.RepositoryId = repositoryId;
                discovered.IsRemoved = false;
                discoveredPaths.Add(discovered.RelativePath);

                bool isNew = !existing.TryGetValue(discovered.RelativePath, out RepoFile current);

                if (isNew)
                {
                    discovered.IsExcluded = false;
                    addedCandidates.Add(discovered);
                }
                else
                {
                    discovered.Id = current.Id;
                    discovered.IsExcluded = current.IsExcluded;
                    discovered.ProjectPath = string.IsNullOrEmpty(discovered.ProjectPath) ? current.ProjectPath : discovered.ProjectPath;
                }

                discovered.Id = await UpsertFileAsync(connection, transaction, discovered, cancellationToken).ConfigureAwait(false);

                if (isNew)
                {
                    continue;
                }

                if (string.Equals(current.Sha256, discovered.Sha256, StringComparison.Ordinal))
                {
                    result.UnchangedFiles++;
                    changes.Add(BuildChange(repositoryId, discovered, RepositoryChangeType.Unchanged));
                }
                else
                {
                    result.ChangedFiles++;
                    changes.Add(BuildChange(repositoryId, discovered, RepositoryChangeType.Modified));
                }
            }

            foreach (RepoFile current in existing.Values)
            {
                if (discoveredPaths.Contains(current.RelativePath) || failedSet.Contains(current.RelativePath) || current.IsRemoved)
                {
                    continue;
                }

                removedCandidates.Add(current);
                await MarkRemovedAsync(connection, transaction, current.Id, cancellationToken).ConfigureAwait(false);
            }

            HashSet<string> matchedAddedPaths = ClassifyAdditionsAndRemovals(repositoryId, addedCandidates, removedCandidates, changes, result);

            foreach (RepoFile added in addedCandidates)
            {
                if (!matchedAddedPaths.Contains(added.RelativePath))
                {
                    result.NewFiles++;
                    changes.Add(BuildChange(repositoryId, added, RepositoryChangeType.Added));
                }
            }

            result.Changes = changes;
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
            Dictionary<string, RepoFile> existing = [with(StringComparer.Ordinal)];

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

        private static async Task<long> UpsertFileAsync(SqliteConnection connection, SqliteTransaction transaction, RepoFile file, CancellationToken cancellationToken)
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
                SELECT Id FROM RepoFiles WHERE RepositoryId = @RepositoryId AND RelativePath = @RelativePath;
                """;
            AddParameters(command, file);

            object result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return (long)result;
        }

        private static HashSet<string> ClassifyAdditionsAndRemovals(
            long repositoryId,
            List<RepoFile> addedCandidates,
            List<RepoFile> removedCandidates,
            List<RepositoryFileChange> changes,
            RepositoryReconciliationResult result)
        {
            HashSet<string> matchedAddedPaths = [with(StringComparer.Ordinal)];

            foreach (RepoFile removed in removedCandidates)
            {
                RepoFile match = null;

                foreach (RepoFile added in addedCandidates)
                {
                    if (matchedAddedPaths.Contains(added.RelativePath))
                    {
                        continue;
                    }

                    if (string.Equals(removed.Sha256, added.Sha256, StringComparison.Ordinal) && removed.FileSize == added.FileSize)
                    {
                        match = added;
                        break;
                    }
                }

                if (match is not null)
                {
                    result.RenamedFiles++;
                    matchedAddedPaths.Add(match.RelativePath);
                    changes.Add(BuildRenameChange(repositoryId, match, removed.RelativePath));
                }
                else
                {
                    result.RemovedFiles++;
                    changes.Add(BuildChange(repositoryId, removed, RepositoryChangeType.Removed));
                }
            }

            return matchedAddedPaths;
        }

        private static RepositoryFileChange BuildChange(long repositoryId, RepoFile file, RepositoryChangeType changeType)
        {
            return new RepositoryFileChange
            {
                RepositoryId = repositoryId,
                FileId = file.Id,
                RelativePath = file.RelativePath,
                ChangeType = changeType,
                Sha256 = file.Sha256,
                FileSize = file.FileSize,
                LastModifiedDate = file.LastModifiedDate
            };
        }

        private static RepositoryFileChange BuildRenameChange(long repositoryId, RepoFile file, string previousRelativePath)
        {
            return new RepositoryFileChange
            {
                RepositoryId = repositoryId,
                FileId = file.Id,
                RelativePath = file.RelativePath,
                PreviousRelativePath = previousRelativePath,
                ChangeType = RepositoryChangeType.Renamed,
                Sha256 = file.Sha256,
                FileSize = file.FileSize,
                LastModifiedDate = file.LastModifiedDate
            };
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
