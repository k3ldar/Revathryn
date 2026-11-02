using System.Diagnostics.CodeAnalysis;

using Revathryn.Contracts.Analysis;
using Revathryn.Contracts.Review;

namespace Revathryn.Engine.Tests
{
    /// <summary>Scripted review engine returning canned results. No AI provider is ever called.</summary>
    [ExcludeFromCodeCoverage]
    internal sealed class ScriptedValidationEngine : IIssueValidationEngine
    {
        private readonly Queue<Func<IssueValidationRequest, IssueValidationEngineResult>> _script = new();

        public List<IssueValidationRequest> Requests { get; } = [];

        public ScriptedValidationEngine Returns(IssueValidationEngineResult result)
        {
            _script.Enqueue(_ => result);
            return this;
        }

        public ScriptedValidationEngine Throws(Exception exception)
        {
            _script.Enqueue(_ => throw exception);
            return this;
        }

        public Task<IssueValidationEngineResult> ValidateAsync(IssueValidationRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);

            if (_script.Count == 0)
            {
                throw new InvalidOperationException("No scripted result available.");
            }

            Func<IssueValidationRequest, IssueValidationEngineResult> next = _script.Count == 1 ? _script.Peek() : _script.Dequeue();
            return Task.FromResult(next(request));
        }
    }
}
