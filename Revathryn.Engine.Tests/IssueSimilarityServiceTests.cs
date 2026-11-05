using System.Diagnostics.CodeAnalysis;

using Revathryn.Contracts;
using Revathryn.Contracts.Models;
using Revathryn.Contracts.Services;

namespace Revathryn.Engine.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class IssueSimilarityServiceTests
    {
        [TestMethod]
        public void CalculateSimilarity_IdenticalText_ReturnsPerfectScore()
        {
            IssueSimilarityService service = CreateService();
            Finding left = CreateFinding("Missing null check", "The value can be null when dereferenced.", "Add a null guard.");
            Finding right = CreateFinding("Missing null check", "The value can be null when dereferenced.", "Add a null guard.");

            double score = service.CalculateSimilarity(left, right);

            Assert.AreEqual(Consts.IssueSimilarityPerfectScore, score);
        }

        [TestMethod]
        public void CalculateSimilarity_DisjointText_ReturnsMinimumScore()
        {
            IssueSimilarityService service = CreateService();
            Finding left = CreateFinding("alpha beta gamma");
            Finding right = CreateFinding("xray yankee zulu");

            double score = service.CalculateSimilarity(left, right);

            Assert.AreEqual(Consts.IssueSimilarityMinimumScore, score);
        }

        [TestMethod]
        public void CalculateSimilarity_PartialOverlap_ReturnsJaccardScore()
        {
            IssueSimilarityService service = CreateService();
            Finding left = CreateFinding("alpha beta gamma");
            Finding right = CreateFinding("alpha beta delta");

            double score = service.CalculateSimilarity(left, right);

            Assert.AreEqual(0.5, score);
        }

        [TestMethod]
        public void CalculateSimilarity_BothFindingsEmpty_ReturnsPerfectScore()
        {
            IssueSimilarityService service = CreateService();
            Finding left = CreateFinding(string.Empty, string.Empty, string.Empty);
            Finding right = CreateFinding(string.Empty, string.Empty, string.Empty);

            double score = service.CalculateSimilarity(left, right);

            Assert.AreEqual(Consts.IssueSimilarityPerfectScore, score);
        }

        [TestMethod]
        public void CalculateSimilarity_OneFindingEmpty_ReturnsMinimumScore()
        {
            IssueSimilarityService service = CreateService();
            Finding left = CreateFinding(string.Empty, string.Empty, string.Empty);
            Finding right = CreateFinding("alpha beta gamma");

            double score = service.CalculateSimilarity(left, right);

            Assert.AreEqual(Consts.IssueSimilarityMinimumScore, score);
        }

        [TestMethod]
        public void CalculateSimilarity_NullLeftFinding_Throws()
        {
            IssueSimilarityService service = CreateService();

            Assert.ThrowsExactly<ArgumentNullException>(() => service.CalculateSimilarity(null, CreateFinding("alpha beta")));
        }

        [TestMethod]
        public void CalculateSimilarity_NullRightFinding_Throws()
        {
            IssueSimilarityService service = CreateService();

            Assert.ThrowsExactly<ArgumentNullException>(() => service.CalculateSimilarity(CreateFinding("alpha beta"), null));
        }

        [TestMethod]
        public void IsDuplicate_ScoreAtThreshold_ReturnsTrue()
        {
            IssueSimilarityService service = CreateService();
            Finding left = CreateFinding("alpha beta gamma");
            Finding right = CreateFinding("alpha beta delta");

            bool isDuplicate = service.IsDuplicate(left, right, 0.5);

            Assert.IsTrue(isDuplicate);
        }

        [TestMethod]
        public void IsDuplicate_ScoreBelowThreshold_ReturnsFalse()
        {
            IssueSimilarityService service = CreateService();
            Finding left = CreateFinding("alpha beta gamma");
            Finding right = CreateFinding("alpha beta delta");

            bool isDuplicate = service.IsDuplicate(left, right, 0.51);

            Assert.IsFalse(isDuplicate);
        }

        [TestMethod]
        public void Tokenize_FiltersTokensShorterThanMinimumLength()
        {
            IssueSimilarityService service = CreateService(minimumTokenLength: 4);

            IReadOnlySet<string> tokens = service.Tokenize("ab abc abcd");

            Assert.HasCount(1, tokens);
            Assert.Contains("abcd", tokens);
        }

        [TestMethod]
        public void Tokenize_IsCaseInsensitiveAndIgnoresPunctuation()
        {
            IssueSimilarityService service = CreateService();

            IReadOnlySet<string> tokens = service.Tokenize("Alpha, beta! gamma?");

            Assert.HasCount(3, tokens);
            Assert.Contains("alpha", tokens);
            Assert.Contains("beta", tokens);
            Assert.Contains("gamma", tokens);
        }

        [TestMethod]
        public void Tokenize_NullOrWhitespace_ReturnsEmptySet()
        {
            IssueSimilarityService service = CreateService();

            Assert.HasCount(0, service.Tokenize(null));
            Assert.HasCount(0, service.Tokenize("   "));
        }

        private static IssueSimilarityService CreateService(int minimumTokenLength = Consts.DefaultIssueSimilarityMinimumTokenLength)
        {
            IssueSettingsProvider provider = new();
            provider.Refresh(new AISettings { IssueSimilarityMinimumTokenLength = minimumTokenLength });
            return new IssueSimilarityService(provider);
        }

        private static Finding CreateFinding(string title, string description = "", string suggestedFix = "")
        {
            return new Finding
            {
                Title = title,
                Description = description,
                SuggestedFix = suggestedFix
            };
        }
    }
}
