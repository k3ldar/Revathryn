using System.Diagnostics.CodeAnalysis;

using Revathryn.Contracts.Analysis;
using Revathryn.Contracts.Review;

namespace Revathryn.Engine.Tests
{
    /// <summary>Completion client returning a configured result and recording requests. No network access occurs.</summary>
    [ExcludeFromCodeCoverage]
    internal sealed class ScriptedCompletionClient : IAICompletionClient
    {
        public AICompletionResult Result { get; set; } = new() { Success = true, Content = "ok" };

        public List<AICompletionRequest> Requests { get; } = [];

        public Task<AICompletionResult> CompleteAsync(AICompletionRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(Result);
        }
    }
}
