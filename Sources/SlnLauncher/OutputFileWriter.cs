using System.IO;
using Slnx.Interfaces;

namespace SlnLauncher
{
    internal class OutputFileWriter : IFileWriter
    {
        public OutputFileWriter()
        {
        }

        /// <summary>
        /// File.WriteAllText writes to the filesystem cache.
        /// The file is written to the disk by the OS at a later stage.
        /// This can be a problem, if the generated output is required by some other SW.
        /// This method makes use of the StreamWriter, which behaves differently.
        /// </summary>
        public void WriteAllText(string path, string text)
        {
            using (var f = new StreamWriter(path))
            {
                f.Write(text);
            }
        }
    }
}
