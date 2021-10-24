using System.IO;
using NUnit.Framework;
using Slnx;

namespace SlnLauncher.Test
{
    [TestFixture]
    public class CsProjectTest
    {
        [SetUp]
        public void Setup()
        {
        }

        [TestCase("PlatformTarget.csproj", "Any CPU")]
        [TestCase("Platforms.csproj", "My Own Platform")]
        public void Platforms(string project, string expectedPlatform)
        {
            var csProject = new CsProject(TestHelper.GetStimulPathFor(Path.Combine("Projects", project)), null);

            Assert.AreEqual(expectedPlatform, csProject.Platforms);
        }

        [TestCase("PlatformTarget.csproj", CsProject.PlatformType.x86)]
        [TestCase("Platforms.csproj", CsProject.PlatformType.AnyCPU)]
        public void PlatformTarget(string project, CsProject.PlatformType expectedPlatform)
        {
            var csProject = new CsProject(TestHelper.GetStimulPathFor(Path.Combine("Projects", project)), null);

            Assert.AreEqual(expectedPlatform, csProject.PlatformTarget);
        }

        [TestCase("PlatformTarget.csproj", "net48")]
        [TestCase("Platforms.csproj", "net5.0")]
        public void Framework(string project, string expectedPlatform)
        {
            var csProject = new CsProject(TestHelper.GetStimulPathFor(Path.Combine("Projects", project)), null);

            Assert.AreEqual(expectedPlatform, csProject.Framework);
        }
    }
}