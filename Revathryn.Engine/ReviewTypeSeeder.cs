using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;
using Revathryn.Contracts.Services;

namespace Revathryn.Engine
{
    /// <summary>Seeds built-in review definitions for a repository without overwriting user changes.</summary>
    public sealed class ReviewTypeSeeder : IReviewTypeSeeder
    {
        private readonly IReviewTypeRepository _reviewTypeRepository;

        /// <summary>Initialises a new seeder.</summary>
        public ReviewTypeSeeder(IReviewTypeRepository reviewTypeRepository)
        {
            _reviewTypeRepository = reviewTypeRepository;
        }

        /// <summary>
        /// Inserts any built-in review definitions that are missing for the repository.
        /// Returns the number of review definitions that were inserted.
        /// </summary>
        public async Task<int> SeedAsync(long repositoryId, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ReviewType> existing = await _reviewTypeRepository.GetByRepositoryAsync(repositoryId, cancellationToken).ConfigureAwait(false);

            HashSet<string> existingKeys = new(
                existing.Where(t => !string.IsNullOrEmpty(t.Key)).Select(t => t.Key),
                StringComparer.Ordinal);

            int inserted = 0;
            DateTime now = DateTime.UtcNow;

            foreach (ReviewTypeDefinition definition in DefaultReviewTypes.All)
            {
                if (existingKeys.Contains(definition.Key))
                {
                    continue;
                }

                ReviewType reviewType = new()
                {
                    RepositoryId = repositoryId,
                    Key = definition.Key,
                    Name = definition.Name,
                    Description = definition.Description,
                    FileExtensions = definition.FileExtensions,
                    Prompt = definition.Description,
                    DefaultPriority = definition.Priority,
                    Enabled = true,
                    CreatedDate = now,
                    LastModifiedDate = now
                };

                reviewType.ReviewDefinitionVersion = ReviewDefinitionVersionCalculator.Compute(reviewType);

                await _reviewTypeRepository.AddAsync(reviewType, cancellationToken).ConfigureAwait(false);
                inserted++;
            }

            return inserted;
        }
    }
}
