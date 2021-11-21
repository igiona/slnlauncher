using System;
using System.Collections.Generic;
using System.Text;

namespace Slnx.Interfaces
{
    public interface IFileWriter
    {
        void WriteAllText(string path, string text);
    }
}
