using Revathryn.Contracts.Models;
using Revathryn.Contracts.Persistence;
using Revathryn.Contracts.Services;

namespace Revathryn.Engine
{
    /// <summary>Seeds built-in review definitions globally and links them to repositories.</summary>
    public sealed class ReviewTypeSeeder : IReviewTypeSeeder
    {
        private readonly IReviewTypeRepository _reviewTypeRepository;
        private readonly IRepositoryReviewRepository _repositoryReviewRepository;

        /// <summary>Initialises a new seeder.</summary>
        public ReviewTypeSeeder(IReviewTypeRepository reviewTypeRepository, IRepositoryReviewRepository repositoryReviewRepository)
        {
            _reviewTypeRepository = reviewTypeRepository;
            _repositoryReviewRepository = repositoryReviewRepository;
        }

        /// <summary>
        /// Inserts any built-in review definitions that are missing globally.
        /// Returns the number of review definitions that were inserted.
        /// </summary>
        public async Task<int> SeedAsync(CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ReviewType> existing = await _reviewTypeRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);

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
                    Key = definition.Key,
                    Name = definition.Name,
                    Description = definition.Description,
                    FileExtensions = definition.FileExtensions,
                    Prompt = definition.Description,
                    DefaultPriority = definition.Priority,
                    CreatedDate = now,
                    LastModifiedDate = now
                };

                reviewType.ReviewDefinitionVersion = ReviewDefinitionVersionCalculator.Compute(reviewType);

                await _reviewTypeRepository.AddAsync(reviewType, cancellationToken).ConfigureAwait(false);
                existingKeys.Add(definition.Key);
                inserted++;
            }

            return inserted;
        }

        /// <summary>
        /// Links every global review type to the repository so its enabled state can be configured.
        /// Returns the number of new links that were created.
        /// </summary>
        public async Task<int> LinkAllReviewTypesAsync(long repositoryId, CancellationToken cancellationToken = default)
        {
            return await _repositoryReviewRepository.LinkAllReviewTypesAsync(repositoryId, cancellationToken).ConfigureAwait(false);
        }
    }
}
