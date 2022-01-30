using System.IO;
using Slnx.Interfaces;

namespace SlnLauncher.Test
{
    internal class DebugTestAppNugetRefFileWriter : AppBaseFileWriter
    {
        internal const string FolderName = "DebugTestAppNugetRef";
        internal string SlnxName = Path.Combine(FolderName, "DebugTestAppNugetRef.slnx");

        public DebugTestAppNugetRefFileWriter() : base (FolderName)
        {
        }
    }
}
