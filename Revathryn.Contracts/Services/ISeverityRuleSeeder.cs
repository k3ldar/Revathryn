namespace Revathryn.Contracts.Services
{
    /// <summary>Seeds default severity override rules without overwriting user changes.</summary>
    public interface ISeverityRuleSeeder
    {
        /// <summary>Inserts default rules when none are configured. Returns the number inserted.</summary>
        Task<int> SeedAsync(CancellationToken cancellationToken = default);
    }
}
