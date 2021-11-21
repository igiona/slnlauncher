using System.IO;
using Slnx.Interfaces;

namespace SlnLauncher
{
    internal class OutputFileWriter : IFileWriter
    {
        public OutputFileWriter()
        {
        }

        public bool FileExists(string filePath)
        {
            return File.Exists(filePath);
        }

        public void DeleteFile(string filePath)
        {
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

        /// <summary>
        /// File.WriteAllText writes to the filesystem cache.
        /// The file is written to the disk by the OS at a later stage.
        /// This can be a problem, if the generated output is required by some other SW.
        /// This method makes use of the StreamWriter, which behaves differently.
        /// </summary>
        private void WriteAllText(string path, string text, bool append)
        {
            using (var f = new StreamWriter(path, append))
            {
                f.Write(text);
            }
        }
    }
}
