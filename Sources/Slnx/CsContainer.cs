using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.IO;

namespace Slnx
{
    public class CsContainer : SlnItem
    {
        
        public CsContainer(string name, string container)
        {
            _typeGuid = SolutionFolderProjectTypeGuid.ToUpper();
            _name = name;
            _container = FormatContainer(container);

            if (!string.IsNullOrEmpty(Container))
                _fullPath = string.Format("{0}/{1}", Container, Name);
            else
                _fullPath = name;

            _projectGuid = Guid.NewGuid().ToString().ToUpper();
        }

        public override string TypeGuid
        {
            get { return _typeGuid; }
        }

        public override string ProjectGuid
        {
            get { return _projectGuid; }
        }

        public override string FullPath
        {
            get { return _fullPath; }
        }

        public override string Container
        {
            get { return _container; }
        }

        public override string Name
        {
            get { return _name; }
        }

        public override bool IsTestProject
        {
            get { return false; }
        }

        public override string GetBuildConfiguration()
        {
            return null;
        }

        public override string ToString()
        {
            return string.Format("\nProject(\"{{{0}}}\") = \"{1}\", \"{2}\", \"{{{3}}}\"\nEndProject", TypeGuid, Name, Name, ProjectGuid);
        }
    }
}
