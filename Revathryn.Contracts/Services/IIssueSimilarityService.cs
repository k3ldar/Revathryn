using Revathryn.Contracts.Models;

namespace Revathryn.Contracts.Services
{
    /// <summary>Computes Jaccard similarity between findings for duplicate detection.</summary>
    public interface IIssueSimilarityService
    {
        /// <summary>Returns the Jaccard similarity between two findings, from 0.0 to 1.0.</summary>
        double CalculateSimilarity(Finding left, Finding right);

        /// <summary>Returns true when the two findings meet the supplied similarity threshold.</summary>
        bool IsDuplicate(Finding left, Finding right, double threshold);

        /// <summary>Tokenises text for similarity comparison, filtering insignificant tokens.</summary>
        IReadOnlySet<string> Tokenize(string text);
    }
}
