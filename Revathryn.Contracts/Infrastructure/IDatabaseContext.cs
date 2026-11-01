namespace Revathryn.Contracts.Infrastructure
{
    /// <summary>Provides access to persistence transactions and connectivity.</summary>
    public interface IDatabaseContext
    {
        /// <summary>Begins a new transaction.</summary>
        ITransactionScope BeginTransaction();

        /// <summary>Verifies that the database is reachable and queryable.</summary>
        Task<bool> CanConnectAsync(CancellationToken cancellationToken = default);
    }
}
