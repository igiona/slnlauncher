using System.IO;
using Slnx.Interfaces;

namespace SlnLauncher.Test
{
    internal class AppBaseFileWriter : IFileWriter
    {
        protected string _folderName;

        public AppBaseFileWriter(string folderName)
        {
            _folderName = folderName;
        }

        public bool FileExists(string filePath)
        {
            filePath = GetPath(filePath);
            return File.Exists(filePath);
        }

        public virtual void DeleteFile(string filePath)
        {
            filePath = GetPath(filePath);

            if (!filePath.Contains("TestSubFolder") || File.Exists(filePath))
            {
                File.Delete(filePath);
            }
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
            var destinationPath = GetPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
            using (var f = new StreamWriter(destinationPath, append))
            {
                f.Write(text);
            }
        }

        protected virtual string GetPath(string path)
        {
            var filePartialPath = Path.Combine(_folderName, Path.GetFileName(path));
            return TestHelper.GetResultPathFor(filePartialPath);
        }
    }
}
