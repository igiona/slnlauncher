using System.IO;
using Slnx.Interfaces;

namespace SlnLauncher.Test
{
    internal class TestAppFileWriter : AppBaseFileWriter
    {
        internal const string FolderName = "TestApp";
        internal string SlnxName = Path.Combine(FolderName, "TestApp.slnx");

        public TestAppFileWriter() : base(FolderName)
        {
        }
    }
}
