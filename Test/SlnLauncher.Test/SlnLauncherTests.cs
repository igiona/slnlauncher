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
            SlnLauncher.Program.Main(TestHelper.GetArguments(slnxFile, argument));
        }

        [TestCase("InvalidMinVersionOnSlnxForceMinFalseOnPackage.slnx", null)]
        [TestCase("InvalidMinVersionOnSlnx.slnx", "-nf-")]
        //[TestCase("InvalidMinVersionOnProjectRef.slnx", "-nf-")]
        //[TestCase("InvalidMinVersionOnProjectRefForceMinFalseOnPackage.slnx", null)]
        public void CheckDump(string slnxFile, string argument)
        {
            var expectedFile = TestHelper.GetExpectedPathFor($"{slnxFile}.dump.txt");
            var dumpFile = TestHelper.GetDumpFilePathForSlnx(slnxFile);
            SlnLauncher.Program.Main(TestHelper.GetArguments(slnxFile, argument, "--dump"));

            Assert.IsTrue(TestHelper.Compare(dumpFile, expectedFile,
                            Path.Combine("Test", "Stimuli", "Projects"),
                            Path.Combine("Sources", "Slnx")
                            ));
        }
    }
}