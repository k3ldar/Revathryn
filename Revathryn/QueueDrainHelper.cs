using Revathryn.Contracts;

namespace Revathryn
{
    /// <summary>Drains queue-backed processors concurrently so configured AI engine connection limits can be used.</summary>
    public static class QueueDrainHelper
    {
        /// <summary>
        /// Processes up to <paramref name="drainLimit"/> items, running at most
        /// <see cref="Consts.MaximumAIMaxSimultaneousConnections"/> claims in parallel.
        /// </summary>
        /// <param name="processNextAsync">Claims and processes the next queue item. Returns false when the queue is empty.</param>
        /// <param name="drainLimit">Maximum number of items to process.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="shouldContinue">Optional predicate evaluated between batches. Returning false stops draining.</param>
        public static async Task DrainAsync(Func<CancellationToken, Task<bool>> processNextAsync, int drainLimit, CancellationToken cancellationToken, Func<bool> shouldContinue = null)
        {
            int processed = 0;

            while (processed < drainLimit)
            {
                if (shouldContinue is not null && !shouldContinue())
                {
                    return;
                }

                int batchSize = Math.Min(Consts.MaximumAIMaxSimultaneousConnections, drainLimit - processed);

                Task<bool>[] batch = new Task<bool>[batchSize];

                for (int index = 0; index < batchSize; index++)
                {
                    batch[index] = processNextAsync(cancellationToken);
                }

                bool[] results = await Task.WhenAll(batch).ConfigureAwait(false);

                processed += batchSize;

                if (results.Any(handled => !handled))
                {
                    return;
                }
            }
        }
    }
}
