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

        protected override string GetPath(string path)
        {
            var filePartialPath = Path.Combine(_folderName, Path.GetFileName(Path.GetDirectoryName(path)), Path.GetFileName(path));
            return TestHelper.GetResultPathFor(filePartialPath);
        }
    }
}
