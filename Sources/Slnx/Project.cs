using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Slnx
{
    public class Project
    {
        string _internalFullPath;

        public Project(string fullpath, string container)
        {
            FullPath = fullpath;
            _internalFullPath = fullpath.Replace("\\", "/");

            if (fullpath.ToLower().EndsWith(CsProject.DotExtension))
            {
                Item = new CsProject(fullpath, container, null);
            }
            else
            {
                Item = new CsContainer(fullpath, container);
                FullPath = Item.FullPath;
            }
        }

        public SlnItem Item
        {
            get;
            private set;
        }

        public string FullPath
        {
            get;
            private set;
        }

        public string FullDir
        {
            get { return Path.GetDirectoryName(FullPath); }
        }
    }
}
