using System.Globalization;
using System.Text;

using Revathryn.Contracts;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Services;

namespace Revathryn.Engine
{
    /// <summary>Computes Jaccard similarity over the text of two findings.</summary>
    public sealed class IssueSimilarityService : IIssueSimilarityService
    {
        private readonly IIssueSettingsProvider _issueSettingsProvider;

        /// <summary>Initialises a new similarity service.</summary>
        public IssueSimilarityService(IIssueSettingsProvider issueSettingsProvider)
        {
            _issueSettingsProvider = issueSettingsProvider;
        }

        /// <summary>Returns the Jaccard similarity between two findings, from 0.0 to 1.0.</summary>
        public double CalculateSimilarity(Finding left, Finding right)
        {
            ArgumentNullException.ThrowIfNull(left);
            ArgumentNullException.ThrowIfNull(right);

            IReadOnlySet<string> leftTokens = TokenizeFinding(left);
            IReadOnlySet<string> rightTokens = TokenizeFinding(right);

            if (leftTokens.Count == 0 && rightTokens.Count == 0)
            {
                return Consts.IssueSimilarityPerfectScore;
            }

            int intersectionCount = leftTokens.Count(rightTokens.Contains);
            int unionCount = leftTokens.Count + rightTokens.Count - intersectionCount;

            if (unionCount == 0)
            {
                return Consts.IssueSimilarityMinimumScore;
            }

            return (double)intersectionCount / unionCount;
        }

        /// <summary>Returns true when the two findings meet the supplied similarity threshold.</summary>
        public bool IsDuplicate(Finding left, Finding right, double threshold)
        {
            return CalculateSimilarity(left, right) >= threshold;
        }

        /// <summary>Tokenises text for similarity comparison, filtering insignificant tokens.</summary>
        public IReadOnlySet<string> Tokenize(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new HashSet<string>(StringComparer.Ordinal);
            }

            HashSet<string> tokens = new(StringComparer.Ordinal);
            StringBuilder current = new();

            foreach (char character in text)
            {
                if (char.IsLetterOrDigit(character))
                {
                    current.Append(char.ToLowerInvariant(character));
                    continue;
                }

                AddToken(tokens, current);
            }

            AddToken(tokens, current);

            return tokens;
        }

        private IReadOnlySet<string> TokenizeFinding(Finding finding)
        {
            HashSet<string> tokens = new(StringComparer.Ordinal);

            foreach (string token in Tokenize(finding.Title ?? string.Empty))
            {
                tokens.Add(token);
            }

            foreach (string token in Tokenize(finding.Description ?? string.Empty))
            {
                tokens.Add(token);
            }

            foreach (string token in Tokenize(finding.SuggestedFix ?? string.Empty))
            {
                tokens.Add(token);
            }

            return tokens;
        }

        private void AddToken(ISet<string> tokens, StringBuilder current)
        {
            if (current.Length >= _issueSettingsProvider.IssueSimilarity.MinimumTokenLength)
            {
                tokens.Add(current.ToString());
            }

            current.Clear();
        }
    }
}
