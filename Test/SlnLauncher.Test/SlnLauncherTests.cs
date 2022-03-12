using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using NUnit.Framework;

namespace SlnLauncher.Test
{
    [TestFixture]
    public class CommandLineTest
    {
        [SetUp]
        public void Setup()
        {
            var resultFolder = TestHelper.GetResultsPath();
            if (Directory.Exists(resultFolder))
            {
                Directory.Delete(resultFolder, true);
            }
            Directory.CreateDirectory(resultFolder);
            Directory.CreateDirectory(TestHelper.GetResultPathFor(TestAppFileWriter.FolderName));
            Directory.CreateDirectory(TestHelper.GetResultPathFor(DebugTestAppAssemblyRefFileWriter.FolderName));
            Directory.CreateDirectory(TestHelper.GetResultPathFor(DebugTestAppNugetRefFileWriter.FolderName));
        }
        }

        //[TestCase("InvalidMinVersionOnProjectRef.slnx", typeof(NuGetClientHelper.Exceptions.InvalidMinVersionDependencyFoundException))]
        [TestCase("InvalidMinVersionOnSlnx.slnx", typeof(NuGetClientHelper.Exceptions.InvalidMinVersionDependencyFoundException))]
        public void MinVersion_TestFails(string slnxFile, Type expectedException)
        {
            Assert.Throws(expectedException, () => SlnLauncher.Program.Main(TestHelper.GetArguments(slnxFile)));
        }

        [TestCase("InvalidMinVersionOnSlnxForceMinFalseOnPackage.slnx", null)]
        [TestCase("InvalidMinVersionOnSlnx.slnx", "-nf-")]
        //[TestCase("InvalidMinVersionOnProjectRef.slnx", "-nf-")]
        //[TestCase("InvalidMinVersionOnProjectRefForceMinFalseOnPackage.slnx", null)]
        public void MinVersion_TestPasses(string slnxFile, string argument)
        {
            Assert.DoesNotThrow(() => SlnLauncher.Program.Main(TestHelper.GetArguments(slnxFile, argument)));
        }

        [TestCase("DuplicatePackageReferenceOnSlnxIgnoreCase.slnx", typeof(Slnx.Exceptions.DuplicatePackageReferenceException))]
        [TestCase("DuplicatePackageReferenceOnSlnx.slnx", typeof(Slnx.Exceptions.DuplicatePackageReferenceException))]
        [TestCase("DuplicatePackageReferenceSlnxCsProjNuget.slnx", typeof(Slnx.Exceptions.DuplicatePackageReferenceException))]
        [TestCase("DuplicatePackageReferenceSlnxCsProjNugetIgnoreCase.slnx", typeof(Slnx.Exceptions.DuplicatePackageReferenceException))]
        [TestCase("DuplicatePackageReferenceSlnxCsProjAssembly.slnx", typeof(Slnx.Exceptions.DuplicatePackageReferenceException))]
        [TestCase("DuplicatePackageReferenceCsProjAssembly.slnx", typeof(Slnx.Exceptions.DuplicatePackageReferenceException))]
        public void TestPackageReferenceDuplicates(string slnxFile, Type expectedException)
        {
            Assert.Throws(expectedException, () => SlnLauncher.Program.Main(TestHelper.GetArguments(Path.Combine("DuplicateReferences", slnxFile))));
        }

        [TestCase("ValidDuplicateCsProjAssembly.slnx")]
        public void TestPackageReferenceDuplicates(string slnxFile)
        {
            Assert.DoesNotThrow(() => SlnLauncher.Program.Main(TestHelper.GetArguments(Path.Combine("DuplicateReferences", slnxFile))));
        }

        [TestCase("dump.txt", "--dump")]
        [TestCase("MsBuildGeneratedProperties.targets", "-msb")]
        [TestCase("SetEnvVars.bat", "-b", ".")]
        [TestCase("SetEnvVars.py", "-py", ".")]
        [TestCase("SetEnvVars.ps1", "-ps", ".")]
        public void TestApp_FileGeneration(string fileName, params string[] commandLineArg)
        {
            var expectedFile = TestHelper.GetExpectedPathFor(Path.Combine(TestAppFileWriter.FolderName, fileName));
            var resultFile = TestHelper.GetResultPathFor(Path.Combine(TestAppFileWriter.FolderName, fileName));

            SlnLauncher.Program.Main(TestHelper.GetArguments(new TestAppFileWriter().SlnxName, commandLineArg), new TestAppFileWriter());

            Assert.IsTrue(TestHelper.Compare(resultFile, expectedFile,
                            Path.Combine("Stimuli", "TestApp"),
                            Path.Combine("Sources", "Slnx")
                            ));
        }

        [TestCase("TestApp.Lib.csproj")]
        [TestCase("TestApp.Lib.Test.csproj")]
        [TestCase("TestApp.UiUnformattedProj.csproj")]
        [TestCase("TestApp.sln")]
        public void TestApp_CsProj(string fileName)
        {
            var expectedFile = TestHelper.GetExpectedPathFor(Path.Combine(TestAppFileWriter.FolderName, fileName));
            var resultFile = TestHelper.GetResultPathFor(Path.Combine(TestAppFileWriter.FolderName, fileName));

            SlnLauncher.Program.Main(TestHelper.GetArguments(new TestAppFileWriter().SlnxName), new TestAppFileWriter());

            Assert.IsTrue(TestHelper.Compare(resultFile, expectedFile));
        }

        [Test]
        public void TestApp_CompareLog()
        {
            var filename = "SlnLauncher.log";
            var expectedFile = TestHelper.GetExpectedPathFor(Path.Combine(TestAppFileWriter.FolderName, filename));
            var resultFile = TestHelper.GetResultPathFor(Path.Combine(TestAppFileWriter.FolderName, filename));

            SlnLauncher.Program.Main(TestHelper.GetArguments(new TestAppFileWriter().SlnxName, "--log"), new TestAppFileWriter());

            Assert.IsTrue(TestHelper.CompareLogFile(resultFile, expectedFile,
                            Path.Combine("Stimuli", "TestApp"),
                            Path.Combine("Sources", "Slnx")
                            ));
        }

        [Test]
        public void TestApp_CompareSlnxPackageRefs()
        {
            var expectedFile = TestHelper.GetExpectedPathFor(Path.Combine(TestAppFileWriter.FolderName, Slnx.CsProject.ImportSlnxConfigName));
            var resultFile = TestHelper.GetResultPathFor(Path.Combine(TestAppFileWriter.FolderName, Slnx.CsProject.ImportSlnxConfigName));

            SlnLauncher.Program.Main(TestHelper.GetArguments(new TestAppFileWriter().SlnxName), new TestAppFileWriter());

            Assert.IsTrue(TestHelper.Compare(resultFile, expectedFile));
        }

        [TestCase("DebugTestApp.ProjWithAssemblyRefToDebugPrj.csproj", "App")]
        [TestCase(Slnx.CsProject.ImportSlnxConfigName, "App")]
        [TestCase("TestApp.UiUnformattedProj.csproj", "Ui")]
        [TestCase(Slnx.CsProject.ImportSlnxConfigName, "Ui")]
        [TestCase("DebugTestAppAssemblyRef.sln", DebugTestAppAssemblyRefFileWriter.FolderName)]
        public void DebugTestAppAssemblyRef_CompareGeneratedFiles(string f, string subFolder)
        {
            SlnLauncher.Program.Main(TestHelper.GetArguments(new DebugTestAppAssemblyRefFileWriter().SlnxName), new DebugTestAppAssemblyRefFileWriter());

            var expectedFile = TestHelper.GetExpectedPathFor(Path.Combine(DebugTestAppAssemblyRefFileWriter.FolderName, subFolder, f));
            var resultFile = TestHelper.GetResultPathFor(Path.Combine(DebugTestAppAssemblyRefFileWriter.FolderName, subFolder, f));
            Assert.IsTrue(TestHelper.Compare(resultFile, expectedFile));
        }

        [TestCase("DebugTestApp.ProjWithNugetRefToDebugPrj.csproj", "App")]
        [TestCase(Slnx.CsProject.ImportSlnxConfigName, "App")]
        [TestCase("TestApp.UiUnformattedProj.csproj", "Ui")]
        [TestCase(Slnx.CsProject.ImportSlnxConfigName, "Ui")]
        [TestCase("DebugTestAppNugetRef.sln", DebugTestAppNugetRefFileWriter.FolderName)]
        public void DebugTestAppNugetyRef_CompareGeneratedFiles(string f, string subFolder)
        {
            SlnLauncher.Program.Main(TestHelper.GetArguments(new DebugTestAppNugetRefFileWriter().SlnxName), new DebugTestAppNugetRefFileWriter());

            var expectedFile = TestHelper.GetExpectedPathFor(Path.Combine(DebugTestAppNugetRefFileWriter.FolderName, subFolder, f));
            var resultFile = TestHelper.GetResultPathFor(Path.Combine(DebugTestAppNugetRefFileWriter.FolderName, subFolder, f));
            Assert.IsTrue(TestHelper.Compare(resultFile, expectedFile));
        }
    }
}