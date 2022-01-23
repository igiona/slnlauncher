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
        private string TestAppSlnx = Path.Combine(TestAppFileWriter.FolderName, "TestApp.slnx");

        [SetUp]
        public void Setup()
        {
            var resultFolder = TestHelper.GeResultsPath();
            if (Directory.Exists(resultFolder))
            {
                Directory.Delete(resultFolder, true);
            }
            Directory.CreateDirectory(resultFolder);
            Directory.CreateDirectory(TestHelper.GeResultPathFor(TestAppFileWriter.FolderName));
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

        [Test]
        public void TestApp_CheckDump()
        {
            var expectedFile = TestHelper.GetExpectedPathFor(Path.Combine(TestAppFileWriter.FolderName, "dump.txt"));
            var dumpFile = TestHelper.GeResultPathFor(Path.Combine(TestAppFileWriter.FolderName, "dump.txt"));

            SlnLauncher.Program.Main(TestHelper.GetArguments(TestAppSlnx, "--dump"), new TestAppFileWriter());

            Assert.IsTrue(TestHelper.Compare(dumpFile, expectedFile,
                            Path.Combine("Test", "Stimuli", "TestApp"),
                            Path.Combine("Sources", "Slnx")
                            ));
        }

        [TestCase("dump.txt", "--dump")]
        [TestCase("MsBuildGeneratedProperties.targets", "-msb")]
        [TestCase("SetEnvVars.bat", "-b", ".")]
        [TestCase("SetEnvVars.py", "-py", ".")]
        [TestCase("SetEnvVars.ps1", "-ps", ".")]
        public void TestApp_FileGeneration(string fileName, params string[] commandLineArg)
        {
            var expectedFile = TestHelper.GetExpectedPathFor(Path.Combine(TestAppFileWriter.FolderName, fileName));
            var resultFile = TestHelper.GeResultPathFor(Path.Combine(TestAppFileWriter.FolderName, fileName));

            SlnLauncher.Program.Main(TestHelper.GetArguments(TestAppSlnx, commandLineArg), new TestAppFileWriter());

            Assert.IsTrue(TestHelper.Compare(resultFile, expectedFile,
                            Path.Combine("Stimuli", "TestApp"),
                            Path.Combine("Sources", "Slnx")
                            ));
        }

        [TestCase("TestApp.Lib.csproj")]
        [TestCase("TestApp.Lib.Test.csproj")]
        [TestCase("TestApp.UiUnformattedProj.csproj")]
        public void TestApp_CsProj(string fileName)
        {
            var expectedFile = TestHelper.GetExpectedPathFor(Path.Combine(TestAppFileWriter.FolderName, fileName));
            var resultFile = TestHelper.GeResultPathFor(Path.Combine(TestAppFileWriter.FolderName, fileName));

            SlnLauncher.Program.Main(TestHelper.GetArguments(TestAppSlnx), new TestAppFileWriter());

            Assert.IsTrue(TestHelper.Compare(resultFile, expectedFile));
        }

        [Test]
        public void TestApp_CompareLog()
        {
            var filename = "SlnLauncher.log";
            var expectedFile = TestHelper.GetExpectedPathFor(Path.Combine(TestAppFileWriter.FolderName, filename));
            var resultFile = TestHelper.GeResultPathFor(Path.Combine(TestAppFileWriter.FolderName, filename));

            SlnLauncher.Program.Main(TestHelper.GetArguments(TestAppSlnx, "--log"), new TestAppFileWriter());

            Assert.IsTrue(TestHelper.CompareLogFile(resultFile, expectedFile,
                            Path.Combine("Stimuli", "TestApp"),
                            Path.Combine("Sources", "Slnx")
                            ));
        }

        [Test]
        public void TestApp_CompareSlnxPackageRefs()
        {
            var expectedFile = TestHelper.GetExpectedPathFor(Path.Combine(TestAppFileWriter.FolderName, Slnx.CsProject.ImportPacakageReferencesProjectName));
            var resultFile = TestHelper.GeResultPathFor(Path.Combine(TestAppFileWriter.FolderName, Slnx.CsProject.ImportPacakageReferencesProjectName));

            SlnLauncher.Program.Main(TestHelper.GetArguments(TestAppSlnx), new TestAppFileWriter());

            Assert.IsTrue(TestHelper.Compare(resultFile, expectedFile));
        }
    }
}