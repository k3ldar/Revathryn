using System.Diagnostics.CodeAnalysis;

using Revathryn.Contracts.Models;
using Revathryn.Contracts.Review;

namespace Revathryn.Contracts.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class ModelsTests
    {
        [TestMethod]
        public void ReviewLink_PriorityOverride_DefaultsToNull()
        {
            ReviewLink link = new();

            Assert.IsNull(link.PriorityOverride);
        }

        [TestMethod]
        public void Finding_FileAndReviewType_AreNullableForHistoricalSurvival()
        {
            Finding finding = new();

            Assert.IsNull(finding.RepoFileId);
            Assert.IsNull(finding.ReviewTypeId);
        }

        [TestMethod]
        public void Repository_StringProperties_DefaultToEmpty()
        {
            Repository repository = new();

            Assert.AreEqual(string.Empty, repository.Name);
            Assert.AreEqual(string.Empty, repository.Description);
            Assert.AreEqual(string.Empty, repository.LocalPath);
            Assert.AreEqual(string.Empty, repository.RepositoryUri);
        }

        [TestMethod]
        public void ReviewRequest_AdditionalContext_DefaultsToEmpty()
        {
            ReviewRequest request = new();

            Assert.HasCount(0, request.AdditionalContext);
        }

        [TestMethod]
        public void ReviewResponse_Results_DefaultsToEmpty()
        {
            ReviewResponse response = new();

            Assert.IsFalse(response.Success);
            Assert.HasCount(0, response.Results);
        }

        [TestMethod]
        public void ReviewContextRequest_CarriesAssignedValues()
        {
            ReviewContextRequest request = new()
            {
                RepositoryId = 7,
                RepositoryName = "Demo Repo",
                RepoFileId = 11,
                Language = "CSharp",
                ContextKey = "symbols",
                MaxItems = 25
            };

            Assert.AreEqual(7, request.RepositoryId);
            Assert.AreEqual("Demo Repo", request.RepositoryName);
            Assert.AreEqual(11, request.RepoFileId);
            Assert.AreEqual("CSharp", request.Language);
            Assert.AreEqual("symbols", request.ContextKey);
            Assert.AreEqual(25, request.MaxItems);
        }

        [TestMethod]
        public void RepoFile_RelativePath_IsTheRepositoryIdentity()
        {
            RepoFile file = new()
            {
                RelativePath = "src/Program.cs",
                FileName = "Program.cs",
                Extension = "cs"
            };

            Assert.AreEqual("src/Program.cs", file.RelativePath);
            Assert.AreEqual("Program.cs", file.FileName);
            Assert.AreEqual("cs", file.Extension);
        }
    }
}
