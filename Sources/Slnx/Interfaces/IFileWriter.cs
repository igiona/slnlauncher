using System;
using System.Collections.Generic;
using System.Text;

namespace Slnx.Interfaces
{
    public interface IFileWriter
    {
        void WriteAllText(string path, string text);

        void AppendAllText(string path, string text);

        public bool FileExists(string filePath);
        
        public void DeleteFile(string filePath);
    }
}
