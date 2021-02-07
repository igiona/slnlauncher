using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Slnx
{
    public class Project : IComparable, Interfaces.IBranchable
    {
        string _internalFullPath;

        public Project(string fullpath, string container, bool ignoreBranches)
        {
            int i;

            FullPath = fullpath;
            _internalFullPath = fullpath.Replace("\\", "/");

            var dirs = _internalFullPath.Split('/');

            for (i = 0; i < dirs.Length; i++)
            {
                if (dirs[i].ToLower() == "trunk")
                {
                    Branch = dirs[i];
                    break;
                }
                else if (dirs[i].ToLower() == "branches" || dirs[i].ToLower() == "tags")
                {
                    Branch = string.Format("{0}/{1}", dirs[i], dirs[i + 1]);
                    break;
                }
            }

            if (fullpath.ToLower().EndsWith(CsProject.DotExtension))
            {
                if (!(ignoreBranches || Branch != null))
                {
                    throw new Exception(string.Format("No trunk/branch found in the path '{0}' !", fullpath));
                }

                CommonRootName = dirs[i - 1];
                var defaultContainer = CommonRootName;
                if (ignoreBranches)
                {
                    defaultContainer = "";
                    CommonRootName = null; //It is not possible to determinate a common root if no branch is present
                }

                if (IsLocalFile)
                {
                    Item = new CsProject(fullpath, container, defaultContainer, Branch);
                }
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

        public string Branch
        {
            get;
            private set;
        }

        public string CommonRootName
        {
            get;
            private set;
        }

        public string BranchableDirectory
        {
            get
            {
                if (Branch != null)
                    return FullPath.Substring(0, _internalFullPath.IndexOf(Branch) + Branch.Length);
                return null;
            }
        }

        public bool IsLocalFile
        {
            get
            {
                return !FullPath.StartsWith("http://") && !FullPath.StartsWith("https://");
            }
        }

        /// <summary>
        /// Compares the project based on the Branchable folder property.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(object other)
        {
            if (other is Interfaces.IBranchable)
                return string.Compare(BranchableDirectory, ((Interfaces.IBranchable)other).BranchableDirectory);
            return -1;
        }
    }
}
