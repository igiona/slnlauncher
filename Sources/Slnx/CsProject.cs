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

        const string KeyAsMsBuildProjectVariableTemplate = @"$({0})";       
        const string ProjectReferenceIncludeTemplate = @"$({0})\{1}.{2}";
        readonly string AssemblyReferenceConditionTemplate = string.Format("$({0}) != 1", NugetHelper.NugetPackage.GetDebugEnvironmentVariableKey("{0}"));

        const string AssemblyReferenceTag = "Reference";
        const string ProjectReferenceTag = "ProjectReference";
        const string ConditionAttributeTag = "Condition";
        const string ProjectAttributeTag = "Project";
        const string ImportTag = "Import";
        public const string ImportDebugProjectName = "nuget.debug";
        static readonly string ImportDebugCondition = $"Exists('{ImportDebugProjectName}')";

        static Regex _guidRegex = new Regex(GuidPattern);
        static Regex _platformRegex = new Regex(PlatformPattern);
        static Regex _projectRefRegex = new Regex(ProjectReferencePattern);

        XmlDocument _xml;
        string _projectOriginalContent;
        bool _isTestProject = false;
        List<Generated.AssemblyReference> _assemblyReferences = null;
        List<Generated.ProjectReference> _projectReferences = null;
        Logger _logger = Logger.Instance;

        public CsProject(string fullpath, string container, bool isPackable)
        {
            _logger.Debug($"Processing project: {fullpath}");

            bool projectContentModified = false;
            IsPackable = isPackable;

            _typeGuid = CsProjectTypeGuid.ToUpper();

            _fullPath = Path.GetFullPath(fullpath);

            if (!File.Exists(FullPath))
                throw new Exception(string.Format("The project '{0}' does not exist!", FullPath));

            _name = Path.GetFileNameWithoutExtension(FullPath);
            _isTestProject = Name.EndsWith(".Test");

            _container = FormatContainer(container);
            if (_container == null && IsTestProject) //Test project, add the Test container under the default container
            {
                _container = FormatContainer(string.Format("Test"));
            }

            _projectOriginalContent = File.ReadAllText(FullPath);

            _xml = new XmlDocument();
            _xml.LoadXml(_projectOriginalContent);
            var projectSdk = _xml.DocumentElement.GetAttribute("Sdk");

            if (projectSdk.StartsWith("Microsoft.NET.Sdk"))
            {
                LegacyProjectStyle = false;
                _projectGuid = Guid.NewGuid().ToString();
                Platform = PlatformType.AnyCpu; //?

                Framework = TryGetFramework("TargetFramework");
            }
            else
            {
                LegacyProjectStyle = true;
                _logger.Warn($"The current solution contains a legacy project {Name}, the Debug feature might not work as expected!");

                var projectNewContent = _projectOriginalContent;
                Framework = TryGetFramework("TargetFrameworkVersion");

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
            EnvironmentVariableDebugKey = NugetHelper.NugetPackage.GetDebugEnvironmentVariableKey(EnvironmentVariableKey);
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

        public override bool IsTestProject
        {
            get { return _isTestProject; }
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

        public List<Generated.AssemblyReference> AssemblyReferences
        {
            get { return _assemblyReferences; }
        }

        public List<Generated.ProjectReference> ProjectReferences
        {
            get { return _projectReferences; }
        }

        public bool LegacyProjectStyle
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

        ///TODO: return string list => .exe .dll based on outputtype element in project
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

        public void TryFixProjectFile(IEnumerable<NugetPackage> packages)
        {
            _logger.Debug($"Fixing project: {Name}");

            _assemblyReferences = GatherAndFixAssemblyReferences(packages);
            _projectReferences = GatherAndFixProjectReferences();
            if (!LegacyProjectStyle)
            {
                FixDebugImport();
            }
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

        private void FixDebugImport()
        {
            //var xmlSer = new XmlSerializer(typeof(Generated.ProjectReference));
            var ret = new List<Generated.ProjectReference>();
            XmlNode importNode = null;
            foreach (XmlNode r in _xml.GetElementsByTagName(ImportTag))
            {
                if (r.Attributes.GetNamedItem(ProjectAttributeTag)?.Value == ImportDebugProjectName)
                {
                    importNode = r;
                    break;
                }
            }
            if (importNode == null)
            {
                importNode = _xml.CreateElement(ImportTag);
                importNode.Attributes.Append(_xml.CreateAttribute(ProjectAttributeTag));
                importNode.Attributes.Append(_xml.CreateAttribute(ConditionAttributeTag));
                _xml.DocumentElement.PrependChild(importNode);
            }
            importNode.Attributes[ProjectAttributeTag].Value = ImportDebugProjectName;
            importNode.Attributes[ConditionAttributeTag].Value = ImportDebugCondition;
        }

        private List<Generated.AssemblyReference> GatherAndFixAssemblyReferences(IEnumerable<NugetPackage> packages)
        {
            var xmlSer = new XmlSerializer(typeof(Generated.AssemblyReference));
            var ret = new List<Generated.AssemblyReference>();
            foreach (XmlNode r in _xml.GetElementsByTagName(AssemblyReferenceTag))
            {
                var assemblyRef = (Generated.AssemblyReference)xmlSer.Deserialize(new StringReader(StripOuterXmlNamespace(r)));
                if (!string.IsNullOrEmpty(assemblyRef.HintPath))
                {
                    var candidatePackageName = assemblyRef.Include.Split(',').First();
                    var match = (packages.Where((x) => x.Id == candidatePackageName).Count() > 0);

                    if (!match)
                    {
                        var candidateAssmblyName = Path.GetFileName(assemblyRef.HintPath);
                        var candidatePackage = packages.Where(x => x.Libraries.Where(y => y.EndsWith(candidateAssmblyName)).FirstOrDefault() != null).FirstOrDefault();
                        if (candidatePackage != null)
                        {
                            match = true;
                            candidatePackageName = candidatePackage.Id;
                        }
                    }

                    if (match)
                    {
                        var candidatePackageKey = NugetPackage.EscapeStringAsEnvironmentVariableAsKey(candidatePackageName);
                        var candidatePackageMsBuilVar = string.Format(KeyAsMsBuildProjectVariableTemplate, candidatePackageKey);
                        var assemblyRoot = Path.GetDirectoryName(assemblyRef.HintPath);
                        assemblyRef.HintPath = assemblyRef.HintPath.Replace(assemblyRoot, candidatePackageMsBuilVar);
                        assemblyRef.Condition = string.Format(AssemblyReferenceConditionTemplate, candidatePackageKey);
                        r["HintPath"].InnerText = assemblyRef.HintPath;

                        var conditionAttr = _xml.CreateAttribute(ConditionAttributeTag);
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

        private List<Generated.ProjectReference> GatherAndFixProjectReferences()
        {
            var xmlSer = new XmlSerializer(typeof(Generated.ProjectReference));
            var ret = new List<Generated.ProjectReference>();
            foreach (XmlNode r in _xml.GetElementsByTagName(ProjectReferenceTag))
            {
                var projectRef = (Generated.ProjectReference)xmlSer.Deserialize(new StringReader(StripOuterXmlNamespace(r)));
                if (!string.IsNullOrEmpty(projectRef.Include))
                {
                    var candidateProjectName = Path.GetFileNameWithoutExtension(projectRef.Include);
                    var candidatePackageKey = NugetPackage.EscapeStringAsEnvironmentVariableAsKey(candidateProjectName);
                    projectRef.Include = string.Format(ProjectReferenceIncludeTemplate, candidatePackageKey, candidateProjectName, FileExtension);
                    r.Attributes["Include"].InnerText = projectRef.Include;
                    ret.Add(projectRef);
                }
            }
            return ret;
        }

        private string TryGetFramework(string tag)
        {
            var element = _xml.GetElementsByTagName(tag);

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

        private string StripOuterXmlNamespace(XmlNode node)
        {
            return System.Text.RegularExpressions.Regex.Replace(
            node.OuterXml, @"(xmlns:?[^=]*=[""][^""]*[""])", "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Multiline);
        }
    }
}
