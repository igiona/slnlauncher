using System.IO;
using Slnx.Interfaces;

namespace SlnLauncher.Test
{
    internal class DebugTestAppAssemblyRefFileWriter : AppBaseFileWriter
    {
        internal const string FolderName = "DebugTestAppAssemblyRef";
        internal string SlnxName  = Path.Combine(FolderName, "DebugTestAppAssemblyRef.slnx");

        internal DebugTestAppAssemblyRefFileWriter() : base (FolderName)
        {
        }
    }
}
