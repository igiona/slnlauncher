using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using NUnit.Framework;
using NuGetClientHelper;

namespace SlnLauncher.Test
{
    [TestFixture]
    public class CommandLineTest
    {
        static Dictionary<string, string> _originalVariables = null;

        private void BackupVariables()
        {
            var cp = (System.Collections.Hashtable)System.Environment.GetEnvironmentVariables();
            var keys = cp.Keys.Cast<string>().ToArray();
            var values = cp.Values.Cast<string>().ToArray();

            _originalVariables = new Dictionary<string, string>();
            for (int i = 0; i < keys.Length; i++)
            {
                _originalVariables[keys[i]] = values[i];
            }
        }

        private void ClearUndesiredVariables()
        {
            //Remove the variable that are set
            //by the Slnlauncher while opening the SlnLauncher solution to allow the tests to set different values.
            var skip = new[]
            {
                "NuGet_",
                "Newtonsoft_"
            };
            var cp = (System.Collections.Hashtable)System.Environment.GetEnvironmentVariables();
            var keys = cp.Keys.Cast<string>().ToArray();
            for (int i = 0; i < keys.Length; i++)
            {
                if (skip.Any(x => keys[i].StartsWith(x)))
                {
                    Environment.SetEnvironmentVariable(keys[i], null);
                }
            }
        }

        private void RestoreVariables()
        {
            var cp = (System.Collections.Hashtable)System.Environment.GetEnvironmentVariables();
            var keys = cp.Keys.Cast<string>().ToArray();
            for (int i = 0; i < keys.Length; i++)
            {
                if (!_originalVariables.ContainsKey(keys[i])) //Reset all new vars
                {
                    Console.WriteLine("Resetting: {0}", keys[i]);
                    System.Environment.SetEnvironmentVariable(keys[i], null);
                }
            }
            for (int i = 0; i < _originalVariables.Count(); i++)
            {
                System.Environment.SetEnvironmentVariable(_originalVariables.Keys.ElementAt(i), _originalVariables.Values.ElementAt(i));
            }
        }

        [OneTimeTearDown]
        public void Teardown()
        {
            RestoreVariables();
        }

        [SetUp]
        public void Setup()
        {
            if (_originalVariables == null)
            {
                BackupVariables();
                ClearUndesiredVariables();
            }
            else
            {
                RestoreVariables();
                ClearUndesiredVariables();
            }

            var resultFolder = TestHelper.GetResultsPath();
            if (Directory.Exists(resultFolder))
            {
                Directory.Delete(resultFolder, true);
            }
            Directory.CreateDirectory(resultFolder);
            Directory.CreateDirectory(TestHelper.GetResultPathFor(TestAppFileWriter.FolderName));
            Directory.CreateDirectory(TestHelper.GetResultPathFor(DebugTestAppAssemblyRefFileWriter.FolderName));
            Directory.CreateDirectory(TestHelper.GetResultPathFor(DebugTestAppNugetRefFileWriter.FolderName));

            //The NuSpecGenerator doesn't have a mean to provide the IFileWriter yet
            var pack = TestHelper.GetStimulPathFor(Path.Combine(TestAppFileWriter.FolderName, "pack"));
            if (Directory.Exists(pack))
                Directory.Delete(pack, true);
        }

        [Test]
        public void OfflineModeTest()
        {
            var args = TestHelper.GetArguments("OfflineTest.slnx", "--offline");
            var p = new NuGetPackageInfo("MyLocalTestApp", "1.0.0", TestHelper.GetStimulPathFor("Packages"), NuGetPackageType.DotNet, TestHelper.GetResultPathFor("Cache"));
            //Ensure the packet doesn't exists
            Assert.Throws(typeof(NuGetClientHelper.Exceptions.PackageInstallationException), () => SlnLauncher.Program.Main(args));
            //Manually install the package from the local copy
            NuGetClientHelper.NuGetClientHelper.InstallPackages(new[] { p }, false, null, NuGet.Frameworks.NuGetFramework.ParseFolder("net48"));
            Assert.DoesNotThrow(() => SlnLauncher.Program.Main(args));
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

        [TestCase("FrameworkAppNet45Net5.slnx")]
        public void MultiFramework_Unsupported(string slnxFile, params string[] commandLineArg)
        {
            Assert.Throws<Exceptions.MultiFrameworkAppException>(() => SlnLauncher.Program.Main(TestHelper.GetArguments(slnxFile, commandLineArg)));
        }

        [TestCase("FrameworkAppNet5.slnx")]
        [TestCase("FrameworkAppNet45.slnx")]
        [TestCase("FrameworkAppNet45Net48.slnx")]
        public void MultiFramework_Supported(string slnxFile, params string[] commandLineArg)
        {
            Assert.DoesNotThrow(() => SlnLauncher.Program.Main(TestHelper.GetArguments(slnxFile, commandLineArg)));
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

        [TestCase("nuget.config")]
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

            Assert.IsTrue(TestHelper.Compare(resultFile, expectedFile));
        }

        [TestCase("TestApp.nuspec", "-ns", "pack")]
        public void TestApp_NuspecPass(string fileName, params string[] commandLineArg)
        {
            var expectedFile = TestHelper.GetExpectedPathFor(Path.Combine(TestAppFileWriter.FolderName, fileName));
            var resultFile = TestHelper.GetStimulPathFor(Path.Combine(TestAppFileWriter.FolderName, "pack", fileName));

            SlnLauncher.Program.Main(TestHelper.GetArguments(new TestAppFileWriter().SlnxName, commandLineArg), new TestAppFileWriter());

            Assert.IsTrue(TestHelper.Compare(resultFile, expectedFile));
        }

        [TestCase("TestApp.FailNoContent.slnx", typeof(Exception), "-ns", "pack")]
        [TestCase("TestApp.FailNoAssemblies.slnx", typeof(Exception), "-ns", "pack")]
        [TestCase("TestApp.slnx", typeof(Exception), "-ns", ".")] //Can't generated the nuspec in the SlnX folder
        public void TestApp_NuspecFail(string slnxName, Type ex, params string[] commandLineArg)
        {
            var slnx = TestHelper.GetStimulPathFor(Path.Combine(TestAppFileWriter.FolderName, slnxName));

            var errro = Assert.Throws(ex, () =>
                 SlnLauncher.Program.Main(TestHelper.GetArguments(slnx, commandLineArg), new TestAppFileWriter())
            );
            Console.WriteLine(errro.Message);
        }


        [TestCase("TestApp.FailNoProjectOut.slnx", "-ns", "pack")]
        public void TestApp_NuspecFail2(string slnxName, params string[] commandLineArg)
        {
            var slnx = TestHelper.GetStimulPathFor(Path.Combine(TestAppFileWriter.FolderName, slnxName));

            try
            {
                SlnLauncher.Program.Main(TestHelper.GetArguments(slnx, commandLineArg), new TestAppFileWriter());
            }
            catch (Exception error)
            {
                Console.WriteLine(error.Message);
                Assert.True(error.GetType() == typeof(FileNotFoundException) || error.GetType() == typeof(DirectoryNotFoundException));
                return;
            }
            Assert.Fail();
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