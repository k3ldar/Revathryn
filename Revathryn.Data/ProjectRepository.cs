using Microsoft.Data.Sqlite;

using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;

namespace Revathryn.Data
{
    /// <summary>SQLite implementation of <see cref="IProjectRepository"/>.</summary>
    public sealed class ProjectRepository : SqliteRepositoryBase, IProjectRepository
    {
        private const string SelectColumns =
            "Id, RepositoryId, Name, Kind, RelativePath, DirectoryPath, SolutionPath, SolutionId";

        /// <summary>Initialises a new repository.</summary>
        public ProjectRepository(SqliteConnectionFactory connectionFactory)
            : base(connectionFactory)
        {
        }

        /// <summary>Returns all projects belonging to a repository.</summary>
        public async Task<IReadOnlyList<RepositoryProject>> GetByRepositoryAsync(long repositoryId, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT {SelectColumns} FROM RepoProjects WHERE RepositoryId = @RepositoryId ORDER BY Kind ASC, RelativePath ASC;";
            command.Parameters.AddWithValue("@RepositoryId", repositoryId);

            List<RepositoryProject> results = [];
            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(MapProject(reader));
            }

            return results;
        }

        /// <summary>Reconciles the repository's project structures in a single transaction.</summary>
        public async Task ReconcileAsync(long repositoryId, IReadOnlyList<RepositoryProject> projects, CancellationToken cancellationToken = default)
        {
            using SqliteConnection connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteTransaction transaction = connection.BeginTransaction();

            Dictionary<string, RepositoryProject> existing = await LoadInventoryAsync(connection, transaction, repositoryId, cancellationToken).ConfigureAwait(false);
            HashSet<string> discoveredPaths = new(StringComparer.Ordinal);
            Dictionary<string, long> solutionIds = new(StringComparer.Ordinal);

            foreach (RepositoryProject project in projects)
            {
                project.RepositoryId = repositoryId;
                discoveredPaths.Add(project.RelativePath);

                if (existing.TryGetValue(project.RelativePath, out RepositoryProject current))
                {
                    project.Id = current.Id;
                }

                await UpsertProjectAsync(connection, transaction, project, cancellationToken).ConfigureAwait(false);

                if (project.Kind == ProjectKind.Solution)
                {
                    solutionIds[project.RelativePath] = project.Id;
                }
            }

            foreach (RepositoryProject project in projects)
            {
                if (project.Kind != ProjectKind.Project || string.IsNullOrEmpty(project.SolutionPath))
                {
                    continue;
                }

                long? solutionId = solutionIds.TryGetValue(project.SolutionPath, out long id) ? id : (long?)null;
                await UpdateSolutionIdAsync(connection, transaction, project.Id, solutionId, cancellationToken).ConfigureAwait(false);
            }

            foreach (RepositoryProject current in existing.Values)
            {
                if (!discoveredPaths.Contains(current.RelativePath))
                {
                    await DeleteProjectAsync(connection, transaction, current.Id, cancellationToken).ConfigureAwait(false);
                }
            }

            transaction.Commit();
        }

        private static async Task<Dictionary<string, RepositoryProject>> LoadInventoryAsync(
            SqliteConnection connection,
            SqliteTransaction transaction,
            long repositoryId,
            CancellationToken cancellationToken)
        {
            Dictionary<string, RepositoryProject> existing = new(StringComparer.Ordinal);

            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"SELECT {SelectColumns} FROM RepoProjects WHERE RepositoryId = @RepositoryId;";
            command.Parameters.AddWithValue("@RepositoryId", repositoryId);

            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                RepositoryProject project = MapProject(reader);
                existing[project.RelativePath] = project;
            }

            return existing;
        }

        private static async Task UpsertProjectAsync(
            SqliteConnection connection,
            SqliteTransaction transaction,
            RepositoryProject project,
            CancellationToken cancellationToken)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO RepoProjects (RepositoryId, Name, Kind, RelativePath, DirectoryPath, SolutionPath, SolutionId)
                VALUES (@RepositoryId, @Name, @Kind, @RelativePath, @DirectoryPath, @SolutionPath, @SolutionId)
                ON CONFLICT (RepositoryId, RelativePath) DO UPDATE SET
                    Name = excluded.Name,
                    Kind = excluded.Kind,
                    DirectoryPath = excluded.DirectoryPath,
                    SolutionPath = excluded.SolutionPath,
                    SolutionId = excluded.SolutionId;
                SELECT Id FROM RepoProjects WHERE RepositoryId = @RepositoryId AND RelativePath = @RelativePath;
                """;
            AddParameters(command, project);

            object result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            project.Id = (long)result;
        }

        private static async Task UpdateSolutionIdAsync(
            SqliteConnection connection,
            SqliteTransaction transaction,
            long projectId,
            long? solutionId,
            CancellationToken cancellationToken)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "UPDATE RepoProjects SET SolutionId = @SolutionId WHERE Id = @Id;";
            command.Parameters.AddWithValue("@SolutionId", SqliteValueConverter.ToParameter(solutionId));
            command.Parameters.AddWithValue("@Id", projectId);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private static async Task DeleteProjectAsync(
            SqliteConnection connection,
            SqliteTransaction transaction,
            long id,
            CancellationToken cancellationToken)
        {
            using SqliteCommand command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM RepoProjects WHERE Id = @Id;";
            command.Parameters.AddWithValue("@Id", id);

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        private static void AddParameters(SqliteCommand command, RepositoryProject project)
        {
            command.Parameters.AddWithValue("@RepositoryId", project.RepositoryId);
            command.Parameters.AddWithValue("@Name", project.Name);
            command.Parameters.AddWithValue("@Kind", (int)project.Kind);
            command.Parameters.AddWithValue("@RelativePath", project.RelativePath);
            command.Parameters.AddWithValue("@DirectoryPath", project.DirectoryPath);
            command.Parameters.AddWithValue("@SolutionPath", project.SolutionPath);
            command.Parameters.AddWithValue("@SolutionId", SqliteValueConverter.ToParameter(project.SolutionId));
        }

        private static RepositoryProject MapProject(SqliteDataReader reader)
        {
            return new RepositoryProject
            {
                Id = reader.GetInt64(0),
                RepositoryId = reader.GetInt64(1),
                Name = reader.GetString(2),
                Kind = (ProjectKind)reader.GetInt32(3),
                RelativePath = reader.GetString(4),
                DirectoryPath = reader.GetString(5),
                SolutionPath = reader.GetString(6),
                SolutionId = reader.IsDBNull(7) ? (long?)null : reader.GetInt64(7)
            };
        }
    }
}
