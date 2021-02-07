using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Slnx
{
    public class SlnxFile
    {
        string _internalFullPath;

        public SlnxFile(string fullpath)
        {
            FullPath = fullpath;
            _internalFullPath = fullpath.Replace("\\", "/");

            if (!fullpath.ToLower().EndsWith(SlnxHandler.SlnxExtension)) { throw new Exception(string.Format("Invalid SlnX file '{0}' !", fullpath)); }
        }

        public string FullPath
        {
            get;
            private set;
        }
    }
}
