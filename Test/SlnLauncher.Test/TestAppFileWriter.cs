using System.IO;
using Slnx.Interfaces;

namespace SlnLauncher.Test
{
    internal class TestAppFileWriter : AppBaseFileWriter
    {
        internal const string FolderName = "TestApp";
        internal string SlnxName = Path.Combine(FolderName, "TestApp.slnx");
        private bool _appendSubFolder = false;
        public TestAppFileWriter(bool appendSubFolder = false) : base(FolderName)
        {
            _appendSubFolder = appendSubFolder;
        }

        protected override string GetPath(string path)
        {
            if (_appendSubFolder)
            {
                var filePartialPath = Path.Combine(_folderName, Path.GetFileName(Path.GetDirectoryName(path)), Path.GetFileName(path));
                return TestHelper.GetResultPathFor(filePartialPath);
            }
            return base.GetPath(path);
        }
    }
}
