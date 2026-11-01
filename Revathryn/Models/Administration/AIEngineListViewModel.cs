using Revathryn.Contracts.Models;

namespace Revathryn.Models.Administration
{
    /// <summary>List of configured AI engines for the analyser configuration page.</summary>
    public sealed class AIEngineListViewModel
    {
        /// <summary>Configured AI engines.</summary>
        public IReadOnlyList<AIEngine> Engines { get; set; } = [];
    }
}
