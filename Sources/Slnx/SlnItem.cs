using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Slnx
{
    public abstract class SlnItem
    {
        public const string CsProjectTypeGuid = "FAE04EC0-301F-11D3-BF4B-00C04F79EFBC";
        public const string SolutionFolderProjectTypeGuid = "2150E333-8FDC-42A3-9474-1A3956D46DE8";

        protected string _typeGuid;
        protected string _projectGuid;
        protected string _fullPath;
        protected string _name;
        protected string _container;

        public abstract string TypeGuid
        {
            get;
        }

        public abstract string ProjectGuid
        {
            get;
        }

        public abstract string FullPath
        {
            get;
        }

        public abstract string Container
        {
            get;
        }

        public abstract string Name
        {
            get;
        }

        public abstract string GetBuildConfiguration();

        public bool IsContainer
        {
            get { return TypeGuid == SolutionFolderProjectTypeGuid; }
        }
        
        protected string FormatContainer(string container)
        {
            if (container != null)
            {
                container = string.Join("/", container.Trim().Replace('\\', '/').Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries));
            }
            return container;
        }
    }
}
