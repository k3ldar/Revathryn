using System.Diagnostics.CodeAnalysis;

using Revathryn.Contracts;
using Revathryn.Contracts.Models;

namespace Revathryn.Engine.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class IssueSettingsProviderTests
    {
        [TestMethod]
        public void Defaults_UseContractDefaults()
        {
            IssueSettingsProvider provider = new();

            Assert.IsTrue(provider.IssueSimilarity.Enabled);
            Assert.AreEqual(Consts.DefaultIssueSimilarityThreshold, provider.IssueSimilarity.Threshold);
            Assert.AreEqual(Consts.DefaultIssueSimilarityMinimumTokenLength, provider.IssueSimilarity.MinimumTokenLength);
            Assert.AreEqual(Consts.IssueDefaultPageSize, provider.IssueViewing.DefaultPageSize);
            CollectionAssert.AreEqual(new IssueViewingOptions().PageSizeOptions, provider.IssueViewing.PageSizeOptions);
        }

        [TestMethod]
        public void Refresh_CopiesSimilaritySettings()
        {
            IssueSettingsProvider provider = new();

            provider.Refresh(new AISettings
            {
                IssueSimilarityEnabled = false,
                IssueSimilarityThreshold = 0.65,
                IssueSimilarityMinimumTokenLength = 4,
                IssueDefaultPageSize = 25,
                IssuePageSizeOptions = "10,25,50,100"
            });

            Assert.IsFalse(provider.IssueSimilarity.Enabled);
            Assert.AreEqual(0.65, provider.IssueSimilarity.Threshold);
            Assert.AreEqual(4, provider.IssueSimilarity.MinimumTokenLength);
        }

        [TestMethod]
        public void Refresh_BlankPageSizeOptions_FallsBackToDefaults()
        {
            IssueSettingsProvider provider = new();

            provider.Refresh(new AISettings
            {
                IssueSimilarityEnabled = true,
                IssueSimilarityThreshold = 0.8,
                IssueSimilarityMinimumTokenLength = 3,
                IssueDefaultPageSize = 25,
                IssuePageSizeOptions = string.Empty
            });

            CollectionAssert.AreEqual(new IssueViewingOptions().PageSizeOptions, provider.IssueViewing.PageSizeOptions);
            Assert.AreEqual(25, provider.IssueViewing.DefaultPageSize);
        }

        [TestMethod]
        public void Refresh_DefaultPageSizeMissingFromOptions_AddsIt()
        {
            IssueSettingsProvider provider = new();

            provider.Refresh(new AISettings
            {
                IssueSimilarityEnabled = true,
                IssueSimilarityThreshold = 0.8,
                IssueSimilarityMinimumTokenLength = 3,
                IssueDefaultPageSize = 40,
                IssuePageSizeOptions = "10,25,50"
            });

            CollectionAssert.AreEqual(new int[] { 10, 25, 40, 50 }, provider.IssueViewing.PageSizeOptions);
            Assert.AreEqual(40, provider.IssueViewing.DefaultPageSize);
        }

        [TestMethod]
        public void Refresh_NullSettings_Throws()
        {
            IssueSettingsProvider provider = new();

            Assert.ThrowsExactly<ArgumentNullException>(() => provider.Refresh(null));
        }
    }
}
