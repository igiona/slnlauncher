using System.IO;
using Slnx.Interfaces;

namespace SlnLauncher.Test
{
    internal class TestFileWriter : IFileWriter
    {
        public TestFileWriter()
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
            return TestHelper.GetResultPathFor(Path.GetFileName(path));
        }
    }
}
