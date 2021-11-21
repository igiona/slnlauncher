using System.IO;
using Slnx.Interfaces;

namespace SlnLauncher.Test
{
    internal class TestAppFileWriter : IFileWriter
    {
        internal const string FolderName = "TestApp";

        public TestAppFileWriter()
        {
        }

        public bool FileExists(string filePath)
        {
            filePath = GetPath(filePath);
            return File.Exists(filePath);
        }

        public void DeleteFile(string filePath)
        {
            filePath = GetPath(filePath);
            File.Delete(filePath);
        }

        public void AppendAllText(string path, string text)
        {
            WriteAllText(path, text, true);
        }

        public void WriteAllText(string path, string text)
        {
            WriteAllText(path, text, false);
        }

        public void WriteAllText(string path, string text, bool append)
        {
            using (var f = new StreamWriter(GetPath(path), append))
            {
                f.Write(text);
            }
        }

        private string GetPath(string path)
        {
            var filePartialPath = Path.Combine(FolderName, Path.GetFileName(path));
            return TestHelper.GeResultPathFor(filePartialPath);
        }
    }
}
