using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.IO;
using NugetHelper;

namespace Slnx
{
    public class CsProject : SlnItem
    {
        private static readonly string[] PlatformTypeNames = { "x86", "Any CPU", "Mixed Platforms" };
        public enum PlatformType
        {
            x86,
            AnyCpu,
            Mixed
        }

        public const string FileExtension = "csproj";
        public const string DotExtension = "." + FileExtension;
        const string GuidPattern = @"<ProjectGuid>{(?<guid>.*)}<\/ProjectGuid>";
        const string PlatformPattern = @"<Platform .*>(?<platform>.*)<\/Platform>";
        const string ProjectReferencePattern = "<ProjectReference Include=\"(?<reference>.*)\">";
        const string ProjectReferenceTemplate = @"$({0})\{1}.{2}";

        static Regex _guidRegex = new Regex(GuidPattern);
        static Regex _platformRegex = new Regex(PlatformPattern);
        static Regex _projectRefRegex = new Regex(ProjectReferencePattern);

        public CsProject(string fullpath, string container, string defaultContainer)
        {
            bool projectContentModified = false;

            _typeGuid = CsProjectTypeGuid.ToUpper();

            _fullPath = Path.GetFullPath(fullpath);

            if (!File.Exists(FullPath))
                throw new Exception(string.Format("The project '{0}' does not exist!", FullPath));

            _name = Path.GetFileNameWithoutExtension(FullPath);

            _container = FormatContainer(container);
            if (_container == null) //No specific container specified 
            {
                if (Name.EndsWith(".Test") && container == null) //Test project, add the Test container under the default container
                {
                    _container = FormatContainer(string.Format("{0}/Test", defaultContainer));
                }
                else
                {
                    _container = FormatContainer(defaultContainer);
                }
            }

            var projectContent = File.ReadAllText(FullPath);
            var m = _guidRegex.Match(projectContent);
            if (m.Success)
            {
                _projectGuid = m.Groups["guid"].Value.ToUpper(); 
                
            }
            else
            {
                _projectGuid = Guid.NewGuid().ToString();
            }

            m = _platformRegex.Match(projectContent);
            Platform = PlatformType.AnyCpu;
            if (m.Success)
            {
                var p = m.Groups["platform"].Value.ToLower();
                if (p.Contains("any"))
                    Platform = PlatformType.AnyCpu;
                else if (p.Contains("mix"))
                    Platform = PlatformType.Mixed;
            }
            //else
            //throw new Exception(string.Format("The project '{0}' does not contain a valid Platform tag!", FullPath));

            m = _projectRefRegex.Match(projectContent);
            while (m.Success)
            {
                var p = m.Groups["reference"].Value;
                var pName = Path.GetFileNameWithoutExtension(p);
                var expectedRef = string.Format(ProjectReferenceTemplate, NugetPackage.EscapeStringAsEnvironmentVariableAsKey(pName), pName, FileExtension);
                if (p != expectedRef) //Invalid project reference. Update it.
                {
                    projectContent = projectContent.Replace(p, expectedRef);
                    projectContentModified = true;
                }
                m = m.NextMatch();
            }

            if (projectContentModified)
            {
                File.WriteAllText(FullPath, projectContent);
            }

            EnvironmentVariableKey = NugetPackage.EscapeStringAsEnvironmentVariableAsKey(Name);
            Environment.SetEnvironmentVariable(EnvironmentVariableKey, Path.GetDirectoryName(FullPath));
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

        public PlatformType Platform
        {
            get;
            private set;
        }

        public string PlatformString
        {
            get { return PlatformTypeNames[(int)Platform]; }
        }
        
        public string EnvironmentVariableKey
        {
            get;
            private set;
        }

        public override string GetBuildConfiguration()
        {
            return string.Format(@"
		{{{0}}}.Debug|Any CPU.ActiveCfg = Debug|{1}
		{{{0}}}.Debug|Any CPU.Build.0 = Debug|{1}
		{{{0}}}.Debug|Mixed Platforms.ActiveCfg = Debug|{1}
		{{{0}}}.Debug|Mixed Platforms.Build.0 = Debug|{1}
		{{{0}}}.Debug|x86.ActiveCfg = Debug|{1}
		{{{0}}}.Debug|x86.Build.0 = Debug|{1}
		{{{0}}}.Release|Any CPU.ActiveCfg = Release|{1}
		{{{0}}}.Release|Any CPU.Build.0 = Release|{1}
		{{{0}}}.Release|Mixed Platforms.ActiveCfg = Release|{1}
		{{{0}}}.Release|Mixed Platforms.Build.0 = Release|{1}
		{{{0}}}.Release|x86.ActiveCfg = Release|{1}
		{{{0}}}.Release|x86.Build.0 = Release|{1}", ProjectGuid, PlatformString);
        }

        public override string ToString()
        {
            return string.Format("\nProject(\"{{{0}}}\") = \"{1}\", \"{2}\", \"{{{3}}}\"\nEndProject", TypeGuid, Name, FullPath, ProjectGuid);
        }
    }
}
