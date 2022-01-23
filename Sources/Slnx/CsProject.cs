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
using NuGetClientHelper;
using Slnx.Interfaces;

namespace Slnx
{
    public static class PlatformEnumExtension
    {
        private static readonly string[] PlatformTypeNames = { "x86", "x64", "Any CPU", "Mixed Platforms" };
        public static string ToPrettyString(this CsProject.PlatformType e)
        {
            return PlatformTypeNames[(int)e];
        }
    }

    public class CsProject : SlnItem
    {
        public enum PlatformType
        {
            x86,
            x64,
            AnyCPU,
            Mixed
        }

        public const string FileExtension = "csproj";
        public const string DotExtension = "." + FileExtension;
        
        const string KeyAsMsBuildProjectVariableTemplate = @"$({0})";       
        const string ProjectReferenceIncludeTemplate = @"$({0})\{1}.{2}";
        readonly string AssemblyReferenceConditionTemplate = string.Format("$({0}) != 1", NuGetClientHelper.NuGetPackage.GetDebugEnvironmentVariableKey("{0}"));

        const string AssemblyReferenceElementTag = "Reference";
        const string ProjectReferenceElementTag = "ProjectReference";
        const string ConditionAttributeTag = "Condition";
        const string ProjectAttributeTag = "Project";
        const string PackageReferenceElementTag = "PackageReference";
        const string TargetFrameworkElementTag = "TargetFramework";
        const string IsPackablekElementTag = "IsPackable";
        const string PlatformsElementTag = "Platforms";
        const string PlatformElementAttributeTag = "PlatformTarget";
        const string ImportElementTag = "Import";
        const string IncludeAttributeTag = "Include";
        const string HintPathAttributeTag = "HintPath";

        public const string ImportDebugProjectName = "nuget.debug";
        static readonly string ImportDebugCondition = $"Exists('{ImportDebugProjectName}')";

        public const string ImportPacakageReferencesProjectName = "nuget.refs";
        static readonly string ImportPackageReferencesCondition = $"Exists('{ImportPacakageReferencesProjectName }')";

        XmlDocument _xml;
        string _projectOriginalContent;
        bool _isTestProject = false;
        List<Generated.AssemblyReference> _assemblyReferences = null;
        List<Generated.ProjectReference> _projectReferences = null;
        List<NuGetPackage> _packageReferences = new List<NuGetPackage>();
        Logger _logger = Logger.Instance;
        IFileWriter _fileWriter = null;

        public CsProject(string fullpath, string container, IFileWriter writer)
        {
            _logger.Debug($"Processing project: {fullpath}");
            _fileWriter = writer;
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
                _projectGuid = Guid.NewGuid().ToString();

                PlatformType platformTargetTmp;
                PlatformTarget = PlatformType.AnyCPU;
                if (TryGetPlatformTarget(out platformTargetTmp))
                {
                    PlatformTarget = platformTargetTmp;
                }

                string platformsTmp;
                Platforms = PlatformType.AnyCPU.ToPrettyString();
                if (TryGetPlatforms(out platformsTmp))
                {
                    Platforms = platformsTmp;
                }

                Framework = GetFramework();
                IsPackable = GetIsPackable();
                InFilePackageReferences = GetInFilePackageReferences();
            }
            else
            {
                _logger.Error($"The current solution contains a legacy project: {Name}, which are not supported anymore!");
                throw new Exception("Legacy project are not supported anymore by the slnlauncher (>=v3.0.0). Upgrade you project (.NET Framework/Core) to the new SDK style.");
            }

            var environmentVariableKey = NuGetPackage.EscapeStringAsEnvironmentVariableAsKey(Name);
            var environmentVariableFrameworkKey = NuGetClientHelper.NuGetPackage.GetFrameworkEnvironmentVariableKey(environmentVariableKey);
            EnvironmentVariableKeys = new List<string>() { environmentVariableKey, environmentVariableFrameworkKey };
            Environment.SetEnvironmentVariable(environmentVariableKey, Path.GetDirectoryName(FullPath));
            Environment.SetEnvironmentVariable(environmentVariableFrameworkKey, Framework);
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

        public PlatformType PlatformTarget
        {
            get;
            private set;
        }

        public string Platforms
        {
            get;
            private set;
        }

        public IEnumerable<string> EnvironmentVariableKeys
        {
            get;
            private set;
        }

        public bool IsPackable
        {
            get;
            private set;
        }

        public IReadOnlyList<Generated.AssemblyReference> AssemblyReferences
        {
            get { return _assemblyReferences; }
        }

        public IReadOnlyList<Generated.ProjectReference> ProjectReferences
        {
            get { return _projectReferences; }
        }

        /// <summary>
        /// List of package references present in the cs-project file
        /// </summary>
        public IReadOnlyList<NuGetPackageIdentity> InFilePackageReferences
        {
            get;
            private set;
        }

        /// <summary>
        /// Fully resolved package references (derivate assembly references or package references).
        /// Could be extended by other classes.
        /// </summary>
        public List<NuGetPackage> PackageReferences
        {
            get { return _packageReferences; }
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
		{{{0}}}.Release|x86.Build.0 = Release|{1}", ProjectGuid, Platforms);
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

        public void TryFixProjectFileAndGatherReferences(IEnumerable<NuGetPackage> packages)
        {
            _logger.Debug($"Fixing project: {Name}");

            _assemblyReferences = GetAndFixAssemblyReferences(packages);
            _projectReferences = GetAndFixProjectReferences();
            FixDebugImport();
            FixNugetReferenceImport();
        }

        public void SaveCsProjectToFile()
        {
            string projectNewContent = XDocument.Parse(_xml.OuterXml).ToString();
            if (projectNewContent != _projectOriginalContent)
            {
                _fileWriter.WriteAllText(FullPath, projectNewContent);
                _projectOriginalContent = projectNewContent;
            }
        }

        private XmlNode GetOrAppendImportNodeByProject(string project)
        {
            foreach (XmlNode r in _xml.GetElementsByTagName(ImportElementTag))
            {
                if (r.Attributes.GetNamedItem(ProjectAttributeTag)?.Value == project)
                {
                    return r;
                }
            }
            var importNode = _xml.CreateElement(ImportElementTag);
            importNode.Attributes.Append(_xml.CreateAttribute(ProjectAttributeTag));
            importNode.Attributes.Append(_xml.CreateAttribute(ConditionAttributeTag));
            _xml.DocumentElement.PrependChild(importNode);
            return importNode;
        }

        private void FixNugetReferenceImport()
        {
            XmlNode importNode = GetOrAppendImportNodeByProject(ImportPacakageReferencesProjectName);
            importNode.Attributes[ProjectAttributeTag].Value = ImportPacakageReferencesProjectName;
            importNode.Attributes[ConditionAttributeTag].Value = ImportPackageReferencesCondition;
        }

        private void FixDebugImport()
        {
            XmlNode importNode = GetOrAppendImportNodeByProject(ImportDebugProjectName);
            importNode.Attributes[ProjectAttributeTag].Value = ImportDebugProjectName;
            importNode.Attributes[ConditionAttributeTag].Value = ImportDebugCondition;
        }

        private List<Generated.AssemblyReference> GetAndFixAssemblyReferences(IEnumerable<NuGetPackage> packages)
        {
            var xmlSer = new XmlSerializer(typeof(Generated.AssemblyReference));
            var ret = new List<Generated.AssemblyReference>();
            foreach (XmlNode r in _xml.GetElementsByTagName(AssemblyReferenceElementTag))
            {
                var assemblyRef = (Generated.AssemblyReference)xmlSer.Deserialize(new StringReader(StripOuterXmlNamespace(r)));
                
                if (!string.IsNullOrEmpty(assemblyRef.HintPath)) // && !assemblyRef.HintPath.StartsWith("$"))
                {
                    var candidatePackageName = assemblyRef.Include.Split(',').First();
                    NuGetPackage candidatePackage = packages.Where((x) => x.Identity.Id == candidatePackageName).FirstOrDefault();

                    if (candidatePackage == null) //The assembly name might not match the package name
                    {
                        var candidateAssmblyName = Path.GetFileName(assemblyRef.HintPath);
                        candidatePackage = packages.Where(x => x.Libraries.Where(y => y.EndsWith(candidateAssmblyName)).FirstOrDefault() != null).FirstOrDefault();
                    }

                    if (candidatePackage != null) //The current project references candidatePackage
                    {
                        PackageReferences.Add(candidatePackage);
                        var candidatePackageKey = NuGetPackage.EscapeStringAsEnvironmentVariableAsKey(candidatePackage.Identity.Id);
                        var candidatePackageMsBuilVar = string.Format(KeyAsMsBuildProjectVariableTemplate, candidatePackageKey);
                        var assemblyRoot = Path.GetDirectoryName(assemblyRef.HintPath);
                        if (string.IsNullOrEmpty(assemblyRoot))
                        {
                            assemblyRef.HintPath = Path.Combine(candidatePackageMsBuilVar, assemblyRef.HintPath);
                        }
                        else
                        {
                            assemblyRef.HintPath = assemblyRef.HintPath.Replace(assemblyRoot, candidatePackageMsBuilVar);
                        }
                        assemblyRef.Condition = string.Format(AssemblyReferenceConditionTemplate, candidatePackageKey);
                        r[HintPathAttributeTag].InnerText = assemblyRef.HintPath;

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

        private List<Generated.ProjectReference> GetAndFixProjectReferences()
        {
            var xmlSer = new XmlSerializer(typeof(Generated.ProjectReference));
            var ret = new List<Generated.ProjectReference>();
            foreach (XmlNode r in _xml.GetElementsByTagName(ProjectReferenceElementTag))
            {
                var projectRef = (Generated.ProjectReference)xmlSer.Deserialize(new StringReader(StripOuterXmlNamespace(r)));
                if (!string.IsNullOrEmpty(projectRef.Include))
                {
                    var candidateProjectName = Path.GetFileNameWithoutExtension(projectRef.Include);
                    var candidatePackageKey = NuGetPackage.EscapeStringAsEnvironmentVariableAsKey(candidateProjectName);
                    projectRef.Include = string.Format(ProjectReferenceIncludeTemplate, candidatePackageKey, candidateProjectName, FileExtension);
                    r.Attributes[IncludeAttributeTag].InnerText = projectRef.Include;
                    ret.Add(projectRef);
                }
            }
            return ret;
        }

        private List<NuGetPackageIdentity> GetInFilePackageReferences()
        {
            var xmlSer = new XmlSerializer(typeof(Generated.PackageReference));
            var ret = new List<NuGetPackageIdentity>();
            foreach (XmlNode r in _xml.GetElementsByTagName(PackageReferenceElementTag))
            {
                var packageRef = (Generated.PackageReference)xmlSer.Deserialize(new StringReader(StripOuterXmlNamespace(r)));
                if (!string.IsNullOrEmpty(packageRef.Include) && !string.IsNullOrEmpty(packageRef.Version))
                {
                    if (!ret.Any(x => x.Id.ToLower() == packageRef.Include.ToLower()))
                    {
                        ret.Add(new NuGetPackageIdentity(packageRef.Include, packageRef.Version));
                    }
                    else
                    {
                        throw new Exceptions.DuplicatePackageReferenceException($"Duplicate {PackageReferenceElementTag} element with id {packageRef.Include} in the project {Name}");
                    }
                }
                else
                {
                    throw new Exceptions.InvalidPackageReferenceException($"Error in project {Name}, the element {r.OuterXml} doesn't have the Include or the Version attributes properly set.");
                }
            }
            return ret;
        }

        /// <summary>
        /// Tries to find the content of the PlatformTarget.
        /// Mulitple defintion of the element overwrite the previously detected value
        /// </summary>
        /// <param name="platform"></param>
        /// <returns>True if a valid match was found, false otherwise</returns>
        private bool TryGetPlatformTarget(out PlatformType platform)
        {
            platform = PlatformType.AnyCPU;

            var elements = _xml.GetElementsByTagName(PlatformElementAttributeTag);
            if (elements.Count > 0)
            {
                var validMatchFound = false;
                foreach (XmlNode e in elements)
                {
                    var p = e.InnerText?.Trim();
                    if (!string.IsNullOrEmpty(p))
                    {
                        if (Enum.TryParse<PlatformType>(p, out platform))
                        {
                            validMatchFound = true;
                        }
                    }
                }
                return validMatchFound;
            }
            return false;
        }

        /// <summary>
        /// Tries to find the content of the Platforms.
        /// Mulitple defintion of the element overwrite the previously detected value
        /// </summary>
        /// <param name="platform"></param>
        /// <returns>True if a valid match was found, false otherwise</returns>
        private bool TryGetPlatforms(out string platform)
        {
            platform = null;

            var elements = _xml.GetElementsByTagName(PlatformsElementTag);
            if (elements.Count > 0)
            {
                foreach (XmlNode e in elements)
                {
                    platform = e.InnerText?.Trim();
                }
                return true;
            }
            return false;
        }

        private bool GetIsPackable(bool defaultValue = true)
        {
            var ret = defaultValue;
            var elements = _xml.GetElementsByTagName(IsPackablekElementTag);
            foreach(XmlNode e in elements)
            {
                bool.TryParse(e.InnerText, out ret); //Mulitple defintion of the element overwrite the previous value
            }
            return ret;
        }

        private string GetFramework()
        {
            var tag = TargetFrameworkElementTag;
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
