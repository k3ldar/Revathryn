namespace Revathryn.Contracts.Infrastructure
{
    /// <summary>Initialises the persistence store schema.</summary>
    public interface IDatabaseInitializer
    {
        /// <summary>Creates the database and applies pending schema migrations.</summary>
        Task InitializeAsync(CancellationToken cancellationToken = default);

        /// <summary>Synchronous counterpart of <see cref="InitializeAsync"/>.</summary>
        void Initialize();
    }
}
