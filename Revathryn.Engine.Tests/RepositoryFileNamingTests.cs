using System.Diagnostics.CodeAnalysis;

using Revathryn.Contracts;
using Revathryn.Engine;

namespace Revathryn.Engine.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class RepositoryFileNamingTests
    {
        [TestMethod]
        public void GetSafeName_PreservesPlainNames()
        {
            Assert.AreEqual("DemoRepo", RepositoryFileNaming.GetSafeName("DemoRepo"));
        }

        [TestMethod]
        public void GetSafeName_ReplacesWhitespaceWithHyphen()
        {
            Assert.AreEqual("Demo-Repo", RepositoryFileNaming.GetSafeName("Demo Repo"));
        }

        [TestMethod]
        public void GetSafeName_ReplacesInvalidFileNameCharacters()
        {
            Assert.AreEqual("Demo-Repo", RepositoryFileNaming.GetSafeName("Demo:Repo"));
            Assert.AreEqual("Demo-Repo", RepositoryFileNaming.GetSafeName("Demo/Repo"));
            Assert.AreEqual("Demo-Repo", RepositoryFileNaming.GetSafeName("Demo*Repo"));
        }

        [TestMethod]
        public void GetSafeName_CollapsesConsecutiveSeparators()
        {
            Assert.AreEqual("Demo-Repo", RepositoryFileNaming.GetSafeName("Demo   Repo"));
        }

        [TestMethod]
        public void GetSafeName_TrimsLeadingAndTrailingHyphens()
        {
            Assert.AreEqual("Demo", RepositoryFileNaming.GetSafeName(" Demo "));
        }

        [TestMethod]
        public void GetSafeName_FallsBackForEmptyInput()
        {
            Assert.AreEqual(Consts.DefaultRepositoryFolderName, RepositoryFileNaming.GetSafeName(null));
            Assert.AreEqual(Consts.DefaultRepositoryFolderName, RepositoryFileNaming.GetSafeName(string.Empty));
            Assert.AreEqual(Consts.DefaultRepositoryFolderName, RepositoryFileNaming.GetSafeName("   "));
        }

        [TestMethod]
        public void GetSafeName_FallsBackForAllInvalidInput()
        {
            Assert.AreEqual(Consts.DefaultRepositoryFolderName, RepositoryFileNaming.GetSafeName("***"));
        }
    }
}
