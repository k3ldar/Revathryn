namespace Revathryn.Contracts.Infrastructure
{
    /// <summary>A disposable database transaction.</summary>
    public interface ITransactionScope : IDisposable
    {
        /// <summary>Commits the transaction.</summary>
        void Commit();

        /// <summary>Rolls back the transaction.</summary>
        void Rollback();
    }
}
