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
            var csProject = new CsProject(TestHelper.GetStimulPathFor(Path.Combine("Projects", project)), null, null);

            Assert.AreEqual(expectedPlatform, csProject.Platforms);
        }

        [TestCase("PlatformTarget.csproj", CsProject.PlatformType.x86)]
        [TestCase("Platforms.csproj", CsProject.PlatformType.AnyCPU)]
        public void PlatformTarget(string project, CsProject.PlatformType expectedPlatform)
        {
            var csProject = new CsProject(TestHelper.GetStimulPathFor(Path.Combine("Projects", project)), null, null);

            Assert.AreEqual(expectedPlatform, csProject.PlatformTarget);
        }

        [TestCase("PlatformTarget.csproj", "net48")]
        [TestCase("Platforms.csproj", "net5.0")]
        public void Framework(string project, string expectedPlatform)
        {
            var csProject = new CsProject(TestHelper.GetStimulPathFor(Path.Combine("Projects", project)), null, null);

            Assert.AreEqual(expectedPlatform, csProject.Framework);
        }

        [TestCase("PackageReference_Duplicated.csproj")]
        [TestCase("PackageReference_DuplicatedCaseInsensitive.csproj")]
        [TestCase("PackageReferenceDuplicatedDifferentVesions.csproj")]
        public void DuplicatePackageReferencesFromCsProj(string proj)
        {
            Assert.Throws(typeof(Slnx.Exceptions.DuplicatePackageReferenceException), () =>
                                new CsProject(TestHelper.GetStimulPathFor(Path.Combine("Projects", proj)), null, null));
        }

        [TestCase("PackageReference_NoInclude.csproj")]
        [TestCase("PackageReference_NoVersion.csproj")]
        public void InvalidPackageReferenceFromCsProj(string proj)
        {
            Assert.Throws(typeof(Slnx.Exceptions.InvalidPackageReferenceException), () =>
                                new CsProject(TestHelper.GetStimulPathFor(Path.Combine("Projects", proj)), null, null));
        }

        [Test]
        public void PackageReferences()
        {
            var csProject = new CsProject(TestHelper.GetStimulPathFor(Path.Combine("Projects", "PackageReference_Single.csproj")), null, null);
            Assert.AreEqual(1, csProject.PackageReferencesFromCsProj.Count);
            Assert.AreEqual(new NuGetClientHelper.NuGetPackageIdentity("Microsoft.NET.Test.Sdk", "16.9.4"), csProject.PackageReferencesFromCsProj[0]);

            csProject = new CsProject(TestHelper.GetStimulPathFor(Path.Combine("Projects", "PackageReference_Many.csproj")), null, null);
            Assert.AreEqual(3, csProject.PackageReferencesFromCsProj.Count);
            Assert.AreEqual(new NuGetClientHelper.NuGetPackageIdentity("nunit", "3.13.1"), csProject.PackageReferencesFromCsProj[0]);
            Assert.AreEqual(new NuGetClientHelper.NuGetPackageIdentity("NUnit3TestAdapter", "3.17.0"), csProject.PackageReferencesFromCsProj[1]);
            Assert.AreEqual(new NuGetClientHelper.NuGetPackageIdentity("Microsoft.NET.Test.Sdk", "16.9.4"), csProject.PackageReferencesFromCsProj[2]);
        }
    }
}