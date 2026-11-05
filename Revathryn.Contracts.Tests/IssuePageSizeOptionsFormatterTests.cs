using System.Diagnostics.CodeAnalysis;

using Revathryn.Contracts;

namespace Revathryn.Contracts.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class IssuePageSizeOptionsFormatterTests
    {
        [TestMethod]
        public void Format_Null_ReturnsEmptyString()
        {
            Assert.AreEqual(string.Empty, IssuePageSizeOptionsFormatter.Format(null));
        }

        [TestMethod]
        public void Format_Values_JoinsWithSeparator()
        {
            Assert.AreEqual("10,25,50", IssuePageSizeOptionsFormatter.Format([10, 25, 50]));
        }

        [TestMethod]
        public void Parse_NullOrWhitespace_ReturnsEmptyList()
        {
            Assert.HasCount(0, IssuePageSizeOptionsFormatter.Parse(null));
            Assert.HasCount(0, IssuePageSizeOptionsFormatter.Parse(string.Empty));
            Assert.HasCount(0, IssuePageSizeOptionsFormatter.Parse("   "));
        }

        [TestMethod]
        public void Parse_ValidValues_TrimsSortsAndDistincts()
        {
            List<int> parsed = IssuePageSizeOptionsFormatter.Parse(" 50, 10 ,25,50 ");

            CollectionAssert.AreEqual(new int[] { 10, 25, 50 }, parsed);
        }

        [TestMethod]
        public void Parse_InvalidToken_ReturnsEmptyList()
        {
            Assert.HasCount(0, IssuePageSizeOptionsFormatter.Parse("10, abc"));
        }

        [TestMethod]
        public void Parse_NonPositiveValue_ReturnsEmptyList()
        {
            Assert.HasCount(0, IssuePageSizeOptionsFormatter.Parse("10,0,25"));
        }
    }
}
