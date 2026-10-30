namespace Revathryn.Contracts.Infrastructure
{
    /// <summary>Provides access to persistence transactions.</summary>
    public interface IDatabaseContext
    {
        /// <summary>Begins a new transaction.</summary>
        ITransactionScope BeginTransaction();
    }
}
