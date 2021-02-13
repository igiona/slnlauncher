using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
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

        const string DebugEnvironmentVariableKeyTemplate = "{0}_debug";
        const string KeyAsMsBuildProjectVariableTemplate = @"$({0})";       
        const string ProjectReferenceIncludeTemplate = @"$({0})\{1}.{2}";
        readonly string AssemblyReferenceConditionTemplate = string.Format("$({0}) != 1", string.Format(DebugEnvironmentVariableKeyTemplate, "{0}"));

        const string AssemblyReferenceTag = "Reference";
        const string ProjectReferenceTag = "ProjectReference";

        static Regex _guidRegex = new Regex(GuidPattern);
        static Regex _platformRegex = new Regex(PlatformPattern);
        static Regex _projectRefRegex = new Regex(ProjectReferencePattern);

        XmlDocument _xml;
        string _projectOriginalContent;

        public CsProject(string fullpath, string container, string defaultContainer, bool isPackable)
        {
            bool projectContentModified = false;
            IsPackable = isPackable;

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

            _projectOriginalContent = File.ReadAllText(FullPath);

            _xml = new XmlDocument();
            _xml.LoadXml(_projectOriginalContent);

            string framework = null;
            var projectSdk = _xml.DocumentElement.GetAttribute("Sdk");

            if (projectSdk == "Microsoft.NET.Sdk")
            {
                _projectGuid = Guid.NewGuid().ToString();
                Platform = PlatformType.AnyCpu; //?

                Framework = TryGetFramework(_xml, "TargetFramework");
            }
            else
            {
                var projectNewContent = _projectOriginalContent;
                Framework = TryGetFramework(_xml, "TargetFrameworkVersion");

                var m = _guidRegex.Match(projectNewContent);
                if (m.Success)
                {
                    _projectGuid = m.Groups["guid"].Value.ToUpper();
                }
                else
                {
                    throw new Exception(string.Format("Invalid GUID in project {0}", FullPath));
                }

                m = _platformRegex.Match(projectNewContent);
                Platform = PlatformType.AnyCpu;
                if (m.Success)
                {
                    var p = m.Groups["platform"].Value.ToLower();
                    if (p.Contains("any"))
                        Platform = PlatformType.AnyCpu;
                    else if (p.Contains("mix"))
                        Platform = PlatformType.Mixed;
                }
                else
                {
                    throw new Exception(string.Format("The project '{0}' does not contain a valid Platform tag!", FullPath));
                }

                m = _projectRefRegex.Match(projectNewContent);
                while (m.Success)
                {
                    var p = m.Groups["reference"].Value;
                    var pName = Path.GetFileNameWithoutExtension(p);
                    var expectedRef = string.Format(ProjectReferenceIncludeTemplate, NugetPackage.EscapeStringAsEnvironmentVariableAsKey(pName), pName, FileExtension);
                    if (p != expectedRef) //Invalid project reference. Update it.
                    {
                        projectNewContent = projectNewContent.Replace(p, expectedRef);
                        projectContentModified = true;
                    }
                    m = m.NextMatch();
                }
                if (projectContentModified)
                {
                    File.WriteAllText(FullPath, projectNewContent);
                }
            }

            EnvironmentVariableKey = NugetPackage.EscapeStringAsEnvironmentVariableAsKey(Name);
            EnvironmentVariableDebugKey = string.Format(DebugEnvironmentVariableKeyTemplate, EnvironmentVariableKey);
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

        public string FullDir
        {
            get { return Path.GetDirectoryName(FullPath); }
        }
        
        public override string Container
        {
            get { return _container; }
        }

        public override string Name
        {
            get { return _name; }
        }

        public string Framework
        {
            get;
            private set;
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

        public string EnvironmentVariableDebugKey
        {
            get;
            private set;
        }

        public bool IsPackable
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

        public string GetAssemblyPath(string targetConfiguration)
        {
            return Path.Combine(FullDir, "bin", targetConfiguration, Framework, string.Format("{0}.dll", Name));
        }

        public string GetPdbPath(string targetConfiguration)
        {
            return Path.Combine(FullDir, "bin", targetConfiguration, Framework, string.Format("{0}.pdb", Name));
        }

        public override string ToString()
        {
            return string.Format("\nProject(\"{{{0}}}\") = \"{1}\", \"{2}\", \"{{{3}}}\"\nEndProject", TypeGuid, Name, FullPath, ProjectGuid);
        }

        public List<Generated.AssemblyReference> GatherAndFixAssemblyReferences(IEnumerable<NugetPackage> packages)
        {
            var xmlSer = new XmlSerializer(typeof(Generated.AssemblyReference));
            var ret = new List<Generated.AssemblyReference>();
            foreach (XmlNode r in _xml.GetElementsByTagName(AssemblyReferenceTag))
            {
                var assemblyRef = (Generated.AssemblyReference)xmlSer.Deserialize(new StringReader(r.OuterXml));
                if (!string.IsNullOrEmpty(assemblyRef.HintPath))
                {
                    var candidatePackageName = assemblyRef.Include.Split(',').First();
                    if (packages.Where((x) => x.Id == candidatePackageName).Count() > 0)
                    {
                        var candidatePackageKey = NugetPackage.EscapeStringAsEnvironmentVariableAsKey(candidatePackageName);
                        var candidatePackageMsBuilVar = string.Format(KeyAsMsBuildProjectVariableTemplate, candidatePackageKey);
                        var assemblyRoot = Path.GetDirectoryName(assemblyRef.HintPath);
                        assemblyRef.HintPath = assemblyRef.HintPath.Replace(assemblyRoot, candidatePackageMsBuilVar);
                        assemblyRef.Condition = string.Format(AssemblyReferenceConditionTemplate, candidatePackageKey);
                        r["HintPath"].InnerText = assemblyRef.HintPath;

                        var conditionAttr = _xml.CreateAttribute("Condition");
                        if (r.Attributes.GetNamedItem(conditionAttr.Name) == null)
                        {
                            r.Attributes.Append(conditionAttr);
                        }
                        r.Attributes[conditionAttr.Name].Value = assemblyRef.Condition;

                        ret.Add(assemblyRef);
                    }
                }
            }

            return ret;
        }

        public List<Generated.ProjectReference> GatherAndFixProjectReferences()
        {
            var xmlSer = new XmlSerializer(typeof(Generated.ProjectReference));
            var ret = new List<Generated.ProjectReference>();
            foreach (XmlNode r in _xml.GetElementsByTagName(ProjectReferenceTag))
            {
                var projectRef = (Generated.ProjectReference)xmlSer.Deserialize(new StringReader(r.OuterXml));
                if (!string.IsNullOrEmpty(projectRef.Include))
                {
                    var candidateProjectName = Path.GetFileNameWithoutExtension(projectRef.Include);
                    var candidatePackageKey = string.Format(KeyAsMsBuildProjectVariableTemplate, NugetPackage.EscapeStringAsEnvironmentVariableAsKey(candidateProjectName));
                    projectRef.Include = string.Format(ProjectReferenceIncludeTemplate, candidatePackageKey, candidateProjectName, FileExtension);

                    ret.Add(projectRef);
                }
            }
            return ret;
        }

        public void SaveCsProjectToFile()
        {
            string projectNewContent = XDocument.Parse(_xml.OuterXml).ToString();
            if (projectNewContent != _projectOriginalContent)
            {
                File.WriteAllText(FullPath, projectNewContent);
                _projectOriginalContent = projectNewContent;
            }
        }

        private string TryGetFramework(XmlDocument xml, string tag)
        {
            var element = xml.GetElementsByTagName(tag);

            if (element.Count == 1)
            {
                var frameworks = element.Item(0).InnerText;
                var frameworksList = frameworks?.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries);
                if (frameworksList?.Length == 1)
                {
                    return frameworksList.First();
                }
                else if (frameworksList?.Length > 1)
                {
                    throw new Exception(string.Format("Multi framework compilation found in the project {1}. Currently not supported.", tag, FullPath));
                }
                else
                {
                    throw new Exception(string.Format("Invalid framework value for {0} in the project {1}.", tag, FullPath));
                }
            }
            else if (element.Count == 0)
            {
                throw new Exception(string.Format("Could not find the element {0} in the project {1}.", tag, FullPath));
            }
            else
            {
                throw new Exception(string.Format("Multiple definition of the {0} element in the project {1}.", tag, FullPath));
            }
        }
    }
}
