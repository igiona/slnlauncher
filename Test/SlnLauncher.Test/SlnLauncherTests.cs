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
            var resultFolder = TestHelper.GeResultsPath();
            if (Directory.Exists(resultFolder))
            {
                Directory.Delete(resultFolder, true);
            }
            Directory.CreateDirectory(resultFolder);
            Directory.CreateDirectory(TestHelper.GeResultPathFor(TestAppFileWriter.FolderName));
            Directory.CreateDirectory(TestHelper.GeResultPathFor(DebugTestAppAssemblyRefFileWriter.FolderName));
            Directory.CreateDirectory(TestHelper.GeResultPathFor(DebugTestAppNugetRefFileWriter.FolderName));
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

        [TestCase("dump.txt", "--dump")]
        [TestCase("MsBuildGeneratedProperties.targets", "-msb")]
        [TestCase("SetEnvVars.bat", "-b", ".")]
        [TestCase("SetEnvVars.py", "-py", ".")]
        [TestCase("SetEnvVars.ps1", "-ps", ".")]
        public void TestApp_FileGeneration(string fileName, params string[] commandLineArg)
        {
            var expectedFile = TestHelper.GetExpectedPathFor(Path.Combine(TestAppFileWriter.FolderName, fileName));
            var resultFile = TestHelper.GeResultPathFor(Path.Combine(TestAppFileWriter.FolderName, fileName));

            SlnLauncher.Program.Main(TestHelper.GetArguments(new TestAppFileWriter().SlnxName, commandLineArg), new TestAppFileWriter());

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

            SlnLauncher.Program.Main(TestHelper.GetArguments(new TestAppFileWriter().SlnxName), new TestAppFileWriter());

            Assert.IsTrue(TestHelper.Compare(resultFile, expectedFile));
        }

        [Test]
        public void TestApp_CompareLog()
        {
            var filename = "SlnLauncher.log";
            var expectedFile = TestHelper.GetExpectedPathFor(Path.Combine(TestAppFileWriter.FolderName, filename));
            var resultFile = TestHelper.GeResultPathFor(Path.Combine(TestAppFileWriter.FolderName, filename));

            SlnLauncher.Program.Main(TestHelper.GetArguments(new TestAppFileWriter().SlnxName, "--log"), new TestAppFileWriter());

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

            SlnLauncher.Program.Main(TestHelper.GetArguments(new TestAppFileWriter().SlnxName), new TestAppFileWriter());

            Assert.IsTrue(TestHelper.Compare(resultFile, expectedFile));
        }


        [TestCase("DebugTestApp.ProjWithAssemblyRefToDebugPrj.csproj")]
        [TestCase(Slnx.CsProject.ImportDebugProjectName)]
        //[TestCase("DebugTestAppAssemblyRef.sln")] //Requires a file comparer that ignores GUIDs differences
        public void DebugTestAppAssemblyRef_CompareNugetDebugFiles(string f)
        {
            SlnLauncher.Program.Main(TestHelper.GetArguments(new DebugTestAppAssemblyRefFileWriter().SlnxName), new DebugTestAppAssemblyRefFileWriter());

            var expectedFile = TestHelper.GetExpectedPathFor(Path.Combine(DebugTestAppAssemblyRefFileWriter.FolderName, f));
            var resultFile = TestHelper.GeResultPathFor(Path.Combine(DebugTestAppAssemblyRefFileWriter.FolderName, f));
            Assert.IsTrue(TestHelper.Compare(resultFile, expectedFile));
        }

        [TestCase("DebugTestApp.ProjWithNugetRefToDebugPrj.csproj")]
        [TestCase(Slnx.CsProject.ImportDebugProjectName)]
        //[TestCase("DebugTestAppAssemblyRef.sln")] //Requires a file comparer that ignores GUIDs differences

        public void DebugTestAppNugetyRef_CompareNugetDebugFiles(string f)
        {
            SlnLauncher.Program.Main(TestHelper.GetArguments(new DebugTestAppNugetRefFileWriter().SlnxName), new DebugTestAppNugetRefFileWriter());

            var expectedFile = TestHelper.GetExpectedPathFor(Path.Combine(DebugTestAppNugetRefFileWriter.FolderName, f));
            var resultFile = TestHelper.GeResultPathFor(Path.Combine(DebugTestAppNugetRefFileWriter.FolderName, f));
            Assert.IsTrue(TestHelper.Compare(resultFile, expectedFile));
        }
    }
}