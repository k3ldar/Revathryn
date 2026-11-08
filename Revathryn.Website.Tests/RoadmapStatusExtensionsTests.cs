using System.Diagnostics.CodeAnalysis;

using Revathryn.Website.Models;

namespace Revathryn.Website.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class RoadmapStatusExtensionsTests
    {
        [TestMethod]
        public void GetDisplayName_AllStatuses_ReturnsLabels()
        {
            Assert.AreEqual("Completed", RoadmapStatus.Completed.GetDisplayName());
            Assert.AreEqual("In Progress", RoadmapStatus.InProgress.GetDisplayName());
            Assert.AreEqual("Planned", RoadmapStatus.Planned.GetDisplayName());
            Assert.AreEqual("Under Consideration", RoadmapStatus.UnderConsideration.GetDisplayName());
        }

        [TestMethod]
        public void GetDisplayName_UndefinedStatus_ReturnsToString()
        {
            RoadmapStatus undefined = (RoadmapStatus)999;

            Assert.AreEqual("999", undefined.GetDisplayName());
        }

        [TestMethod]
        public void GetCssClass_AllStatuses_ReturnsClasses()
        {
            Assert.AreEqual("status-completed", RoadmapStatus.Completed.GetCssClass());
            Assert.AreEqual("status-inprogress", RoadmapStatus.InProgress.GetCssClass());
            Assert.AreEqual("status-planned", RoadmapStatus.Planned.GetCssClass());
            Assert.AreEqual("status-consideration", RoadmapStatus.UnderConsideration.GetCssClass());
        }

        [TestMethod]
        public void GetCssClass_UndefinedStatus_ReturnsConsideration()
        {
            RoadmapStatus undefined = (RoadmapStatus)999;

            Assert.AreEqual("status-consideration", undefined.GetCssClass());
        }
    }
}
