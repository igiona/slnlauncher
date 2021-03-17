using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Serialization;
using System.Text.RegularExpressions;
using Slnx.Generated;
using NugetHelper;

namespace Slnx
{
    public class SlnxHandler
    {
        public const string SlnxExtension = ".slnx";
        public const string SlnxUserExtension = ".user";
        public const string SlnExtension = ".sln";
        public const string DefaultPackagesFolderName = "pack";
        public const string SpecialKeyFormat = "$({0})";

        string _slnxPath;
        string _slnxDirectory;
        string _slnxFile;
        string _slnxName;
        SlnXType _slnx;
        Logger _logger = Logger.Instance;

        Dictionary<string, string> _specialSlnxKeys = new Dictionary<string, string>();
        Dictionary<string, string> _environmentVariables = new Dictionary<string, string>();
        Dictionary<string, List<PackageType>> _packageBundles = new Dictionary<string, List<PackageType>>();
        List<SlnxHandler> _imports = new List<SlnxHandler>();
        List<Project> _projects = null;
        List<NugetPackage> _packages = new List<NugetPackage>();
        Dictionary<string, string> _packagesToDebug = new Dictionary<string, string>();
        Dictionary<string, SlnxHandler> _debugSlnxItems = new Dictionary<string, SlnxHandler>();

        public SlnxHandler(string fName, string debugPackageId = null) : this(fName, null, debugPackageId)
        {
        }

        public SlnxHandler(string fName, SlnXType userSettings, string debugPackageId)
        {
            if (!string.IsNullOrEmpty(debugPackageId))
            {
                userSettings = null; //Ignore use settings in case of an import via <debug package.../>
            }
            _slnxPath = Path.GetFullPath(fName);
            Assert(File.Exists(_slnxPath), "The provided SlnX file '{0}' is not a file or it doesn't exists", _slnxPath);
            _slnxDirectory = Path.GetDirectoryName(_slnxPath);
            _slnxFile = Path.GetFileName(fName);
            _slnxName = Path.GetFileNameWithoutExtension(fName);
            _specialSlnxKeys["slnx"] = _slnxDirectory;

            _slnx = ReadSlnx(fName);

            AppendSpecialKeys(false);
            ExtendDictionary(_environmentVariables, _slnx.env, true);
            ExtendDictionary(_packageBundles, _slnx.bundle, true);

            TryApplyUserEnvironmentValues(userSettings); //Eventually apply user settings

            SetAll(_environmentVariables); // to allow import having env variable in the path

            ExpandAll(_environmentVariables); // to allow import having env variable with special keys in the path

            ReadImports(_environmentVariables, _packageBundles, _packages);

            TryApplyUserEnvironmentValues(userSettings); //re-Apply, to override value from the imports

            ExpandAll(_environmentVariables); // to apply imports & user env vars 

            string enforcedContainer = null;
            if (!string.IsNullOrEmpty(debugPackageId))
            {
                enforcedContainer = $"_DEBUG/{debugPackageId}";
            }

            FindProjects(_slnx.project, ProjectsSearchPath, _slnx.skip, enforcedContainer);
            ExtendList(_packages, _slnx.package);

            if (string.IsNullOrEmpty(debugPackageId))
            {
                EvalueteDebugPackages(_slnx.debug);
                EvalueteDebugPackages(userSettings?.debug);

                foreach (var item in _packagesToDebug)
                {
                    var slnxItem = new SlnxHandler(item.Value, item.Key);
                    _debugSlnxItems[item.Key] = slnxItem;

                    foreach (var candidate in Packages)
                    {
                        var known = Packages.Where(x => x.Id == candidate.Id).FirstOrDefault();

                        if (known == null)
                        {
                            _logger.Warn($"The package {candidate.Id} required by the package {item.Key} selected for debug, is not presend in the current SlnX file {_slnxName}");
                        }
                        else
                        {
                            Assert(known.MinVersion == candidate.MinVersion &&
                                   known.TargetFramework == candidate.TargetFramework &&
                                   known.PackageType == candidate.PackageType,
                                   $"The provided package {candidate} does not match the already known one {known}");
                        }
                    }
                }
            }
        }

        public string ProjectsSearchPath
        {
            get
            {
                return SafeExpandAndTrimEnvironmentVariables(_slnx?.searchPath);
            }
        }

        public string PackagesPath
        {
            get
            {
                return SafeExpandAndTrimEnvironmentVariables(_slnx?.packagesPath, Path.Combine(SlnxDirectory, DefaultPackagesFolderName));
            }
        }

        public string SlnxDirectory
        {
            get { return _slnxDirectory; }
        }

        public string SlnxPath
        {
            get { return _slnxPath; }
        }

        public string SlnxFolder
        {
            get { return Path.GetDirectoryName(SlnxPath); }
        }

        public string SlnxName
        {
            get { return _slnxName; }
        }

        public string SlnPath
        {
            get
            {
                var slnFile = string.Format("{0}\\{1}{2}", SlnxFolder, Path.GetFileNameWithoutExtension(SlnxPath), SlnExtension);
                if (!string.IsNullOrEmpty(_slnx?.sln))
                    slnFile = _slnx.sln;
                return slnFile;
            }
        }

        public IEnumerable<Project> Projects
        {
            get
            {
                return _projects;
            }
        }

        public IEnumerable<CsProject> CsProjects
        {
            get
            {
                return Projects.Where(x => x.Item is CsProject).Select(x => x.Item as CsProject);
            }
        }

        public IEnumerable<NugetPackage> Packages
        {
            get
            {
                return _packages;
            }
            set
            {
                _packages.Clear();
                _packages.AddRange(value);
            }
        }

        public IEnumerable<Project> DebugProjects
        {
            get
            {
                return _debugSlnxItems.Values.SelectMany(x => x.Projects.Where(p => !p.Item.IsTestProject));
            }
        }

        public IEnumerable<NugetPackage> DebugPackages
        {
            get
            {
                return _debugSlnxItems.Values.SelectMany(x => x.Packages);
            }
        }

        public Dictionary<string, SlnxHandler> DebugSlnxItems
        {
            get { return _debugSlnxItems; }
        }

        public Dictionary<string, string> EnvironementVariables
        {
            get
            {
                return _environmentVariables;
            }
        }

        public static SlnXType ReadSlnx(string slnxFile)
        {
            var xmlSer = new XmlSerializer(typeof(SlnXType));
            SlnXType slnx;
            using (StreamReader streamReader = new StreamReader(slnxFile))
            {
                slnx = (SlnXType)xmlSer.Deserialize(streamReader);
            }
            return slnx;
        }

        public static void WriteSlnx(string slnxFile, SlnXType slnx)
        {
            var xmlSer = new XmlSerializer(typeof(SlnXType));
            using (var streamWriter = new StreamWriter(slnxFile))
            {
                xmlSer.Serialize(streamWriter, slnx);
            }
        }

        public void TryFixProjectFiles()
        {
            _logger.Info($"Trying to fix the Assembly and Project of the known projects");
            if (Packages.Count() == 0)
            {
                _logger.Warn($"No NuGet package found. If this is not correct, it might be because this method was called before installing the NuGet packages.");
            }

            foreach (var csProj in CsProjects)
            {
                _logger.Info($"Trying to fix the Assembly and Project reference of {csProj.Name}");
                csProj.TryFixProjectFile(Packages);
                csProj.SaveCsProjectToFile();
            }
        }

        void FindProjects(ProjectType[] requestedGlobalSettingsProjects, string searchPath, string skip, string enforcedContainer)
        {
            _projects = new List<Project>();
            if (requestedGlobalSettingsProjects != null)
            {
                IEnumerable<string> knownProjects = null;

                if (searchPath.StartsWith("http://") || searchPath.StartsWith("https://"))
                {
                    IEnumerable<string> listing = null;
                    _logger.Info("Listing {0}", searchPath);
                    ExecuteLongSvnOperation(() => { /*listing = FSL.Utilities.Subversion.SvnAccess.Instance.ListRepository(searchPath, true, true);*/ });
                    _logger.Info("Done");

                    knownProjects = listing?.Where((x) => x.EndsWith(".csproj"));
                }
                else if (Directory.Exists(searchPath))
                {
                    knownProjects = Directory.GetFiles(searchPath, "*.csproj", SearchOption.AllDirectories);
                }
                Assert(knownProjects != null, "Unable to find the path/url: '{0}'", searchPath);

                InspectProjects(requestedGlobalSettingsProjects, knownProjects, skip, enforcedContainer);
            }
        }

        void InspectProjects(ProjectType[] requestedProjectsXmlType, IEnumerable<string> knownProjects, string skip, string enforcedContainer)
        {
            List<string> skipList = new List<string>();
            if (!string.IsNullOrEmpty(skip))
            {
                foreach (var s in skip.Split(';'))
                    skipList.Add(SafeExpandEnvironmentVariables(s));
            }

            List<ProjectType> requestedProjects = new List<ProjectType>();

            //Expand all wildcards
            foreach (var reqProjXmlType in requestedProjectsXmlType)
            {
                if (reqProjXmlType.name.Contains("*")) //Check wildcards
                {
                    var wildCardMatches = knownProjects.Where((x) => FilterProjectByName(x, reqProjXmlType.name, skipList)).ToList();
                    var wildCardProjects = wildCardMatches.ConvertAll<ProjectType>((name) =>
                                                {
                                                    var p = new ProjectType();
                                                    p.name = Path.GetFileNameWithoutExtension(name);
                                                    p.container = reqProjXmlType.container;
                                                    p.Value = reqProjXmlType.Value;
                                                    return p;
                                                }
                                            );

                    requestedProjects.AddRange(wildCardProjects);
                }
                else
                {
                    requestedProjects.Add(reqProjXmlType);
                }
            }

            //Check projects's existance and ambiguity (also for wildcard imported projects)
            foreach (var requestedProject in requestedProjects)
            {
                var requestedCsProjName = string.Format("{0}.{1}", requestedProject.name, CsProject.FileExtension);

                var knownProject = knownProjects.Where((x) => FilterProjectByName(x, requestedCsProjName, skipList)).ToList();

                if (knownProject.Count == 0)
                    throw new Exception(string.Format("Project '{0}' not found!", requestedProject.name));
                if (knownProject.Count > 1)
                    throw new Exception(string.Format("Project '{0}' is ambiguous!\n\n{1}", requestedProject.name, string.Join("\n\n", knownProject)));

                var container = requestedProject.container;
                if (!string.IsNullOrEmpty(enforcedContainer))
                {
                    container = enforcedContainer;
                }

                var p = new Project(knownProject[0], container, !requestedProject.packableSpecified || requestedProject.packable);
                _projects.Add(p);

                if (p.Item?.Container != null)
                {
                    var containers = p.Item.Container.Split('/');

                    string parent = null;
                    string currentFullPath = null;
                    foreach (var c in containers)
                    {
                        if (string.IsNullOrEmpty(c)) continue;

                        if (parent == null)
                            currentFullPath = c;
                        else
                            currentFullPath = string.Format("{0}/{1}", currentFullPath, c);

                        if (_projects.Where((x) => x.Item != null && x.Item.IsContainer && x.FullPath == currentFullPath).Count() == 0) //Need to create the container
                            _projects.Add(new Project(c, parent, false));

                        parent = currentFullPath;
                    }
                }
            }
        }

        bool FilterProjectByName(string path, string requestedCsProjName, List<string> skipList)
        {
            var normalizedPath = path.Replace('/', '\\');

            var projectCandidate = normalizedPath.Split('\\').LastOrDefault();
            var projectMatch = new Regex(Regex.Escape(requestedCsProjName).Replace(@"\*", ".*")).IsMatch(projectCandidate);
            //projectMatch = path.EndsWith(requestedCsProjName);

            if (projectMatch && skipList != null)
            {
                foreach (var skip in skipList)
                {
                    if (path.Contains(skip))
                        return false;
                }
            }
            return projectMatch;
        }

        /// <summary>
        /// The imported environment variables do NOT override eventually already present values.
        /// Projects or other settings cannot be imported !
        /// </summary>
        private void ReadImports(Dictionary<string, string> env, Dictionary<string, List<PackageType>> bundle, List<NugetPackage> packages)
        {
            //Evaluate eventually defined import(s)
            if (_slnx.import != null)
            {
                foreach (var import in _slnx.import)
                {
                    Assert(string.IsNullOrEmpty(import.path) ^ string.IsNullOrEmpty(import.bundle), "path and bundle are exclusive attributes in an import element");
                    if (!string.IsNullOrEmpty(import.path)) //File import
                    {
                        var slnxImportFile = Path.GetFullPath(SafeExpandAndTrimEnvironmentVariables(import.path));

                        if (!File.Exists(slnxImportFile))
                            throw new Exception(string.Format("SLNX import not found, file path: {0}", slnxImportFile));

                        var slnx = SlnxHandler.ReadSlnx(slnxImportFile);
                        var imported = new SlnxHandler(slnxImportFile);
                        _imports.Add(imported);

                        ExtendDictionary(env, slnx.env, false);
                        ExtendDictionary(bundle, slnx.bundle, false);
                    }
                    else //if (!string.IsNullOrEmpty(import.bundle))
                    {
                        Assert(_packageBundles.ContainsKey(import.bundle), "Missing bundle with key '{0}'", import.bundle);
                        ExtendList(packages, bundle[import.bundle].ToArray());
                    }
                }
            }
        }

        /// <summary>
        /// The .user defined value (from an eventually found .slnx.user), overrides already present values (regardless if imported, local or already set).
        /// If the .user defined value is not present in the parent file, it is discarded.
        /// </summary>
        /// <param name="env"></param>
        /// <param name="slnxFile"></param>
        /// <returns></returns>
        private void TryApplyUserEnvironmentValues(SlnXType slnxUser)
        {
            if (slnxUser?.env != null)
            {
                foreach (var e in slnxUser.env)
                {
                    if (_environmentVariables.ContainsKey(e.name))
                    {
                        _environmentVariables[e.name] = e.Value; //Override the local value with the user one
                        Environment.SetEnvironmentVariable(e.name, e.Value); //Override the already set value with the user one
                    }
                }
            }
        }

        private void EvalueteDebugPackages(DebugType[] debug)
        {
            if (debug != null)
            {
                foreach (var d in debug)
                {
                    var slnxToImport = SafeExpandAndTrimEnvironmentVariables(d.Value);
                    if (slnxToImport == null)
                    {
                        throw new Exception($"The provided debug element doesn't have a value.");
                    }
                    slnxToImport = Path.GetFullPath(slnxToImport);

                    if (slnxToImport == null || !File.Exists(slnxToImport))
                    {
                        throw new Exception($"The provided debug SlnX file '{slnxToImport}' doesn't exists.");
                    }

                    var packageToDebug = Path.GetFileNameWithoutExtension(slnxToImport);
                    if (d.package != null)
                    {
                        packageToDebug = SafeExpandAndTrimEnvironmentVariables(d.package);
                    }

                    if (_packagesToDebug.ContainsKey(packageToDebug))
                    {
                        if (_packagesToDebug[packageToDebug] != slnxToImport)
                        {
                            throw new Exception($"The provided debug SlnX file for the package {packageToDebug} is duplicate.\n{_packagesToDebug[packageToDebug]} and {slnxToImport}");
                        }
                    }
                    else
                    {
                        _packagesToDebug.Add(packageToDebug, slnxToImport);
                    }
                }
            }
        }

        public string SafeExpandEnvironmentVariables(string value, string defaultValue = null)
        {
            if (value == null)
                return defaultValue;
            var ret = Environment.ExpandEnvironmentVariables(value);
            foreach (var s in _specialSlnxKeys)
            {
                var formattedKey = string.Format(SpecialKeyFormat, s.Key);
                if (ret.Contains(formattedKey))
                {
                    ret = ret.Replace(formattedKey, s.Value);
                }
            }
            return ret;
        }

        public string SafeExpandAndTrimEnvironmentVariables(string value, string defaulValue = null)
        {
            return SafeExpandEnvironmentVariables(value?.Trim(), defaulValue);
        }

        private void ExpandAll(Dictionary<string, string> env)
        {
            foreach (var e in env)
            {
                try
                {
                    var value = Environment.GetEnvironmentVariable(e.Key);
                    //System.Diagnostics.Debug.WriteLine("{0}={1}", e.Key, Environment.ExpandEnvironmentVariables(value));
                    Environment.SetEnvironmentVariable(e.Key, SafeExpandEnvironmentVariables(value));
                }
                catch (Exception ex)
                {
                    throw new Exception(string.Format("Error expanding the variable: {0} = {1}\n", e.Key, e.Value), ex);
                }
            }
        }

        private void AppendSpecialKeys(bool overrideValues)
        {
            foreach (var e in _specialSlnxKeys)
            {
                if (_environmentVariables.ContainsKey(e.Key))
                {
                    if (overrideValues)
                        _environmentVariables[e.Key] = e.Value; //Keep only the last entry in case of multiple definition of the same variable.
                }
                else
                {
                    _environmentVariables.Add(e.Key, SafeExpandEnvironmentVariables(e.Value));
                }
            }
        } 

        private void ExtendDictionary(Dictionary<string, string> env, EnvType[] importEnvValues, bool overrideValues)
        {
            if (importEnvValues != null)
            {
                foreach (var e in importEnvValues)
                {
                    if (env.ContainsKey(e.name))
                    {
                        if (overrideValues)
                            env[e.name] = e.Value; //Keep only the last entry in case of multiple definition of the same variable.
                    }
                    else
                    {
                        env.Add(e.name, e.Value);
                    }
                }
            }
        }
        
        private void ExtendDictionary(Dictionary<string, List<PackageType>> bundle, BundleType[] importedValues, bool overrideValues)
        {
            if (importedValues != null)
            {
                foreach (var e in importedValues)
                {
                    if (!bundle.ContainsKey(e.name) || overrideValues)
                    {
                        bundle[e.name] = e.package.ToList();
                    }
                }
            }
        }

        private void ExtendList(List<NugetPackage> packages, PackageType[] importedValues)
        {
            if (importedValues != null)
            {
                foreach (var e in importedValues)
                {
                    var candidate = new NugetPackage(SafeExpandEnvironmentVariables(e.id),
                                                     SafeExpandEnvironmentVariables(e.version), 
                                                     SafeExpandEnvironmentVariables(e.targetFramework), 
                                                     SafeExpandEnvironmentVariables(e.source),
                                                     SafeExpandEnvironmentVariables(e.var), 
                                                     IsDotNet(e), PackagesPath);

                    var alreadyPresent = packages.Where((x) => x.Id == candidate.Id);
                    if (alreadyPresent.Count() == 0)
                    {
                        packages.Add(candidate);
                    }
                }
            }
        }

        public Nuspec GetNugetPackageInformation()
        {
            Nuspec nuspec = null;
            var nuget = _slnx.nuget;

            if (nuget != null)
            {
                var id = SafeExpandAndTrimEnvironmentVariables(nuget.id, SlnxName);
                var excludePackages = nuget.excludePackagesSpecified && nuget.excludePackages;
                var excludeProjects = nuget.excludeProjectsSpecified && nuget.excludeProjects;
                var versionString = SafeExpandAndTrimEnvironmentVariables(nuget.id, null);
                var targetConfig = SafeExpandAndTrimEnvironmentVariables(nuget.targetConfig, "Release");
                var readmeFile = SafeExpandAndTrimEnvironmentVariables(nuget.readme, null);
                string additionalInformation = null;
                var additionalInformationList = nuget.info?.Any?.Select(x => x.OuterXml);

                if (additionalInformationList != null)
                {
                    additionalInformation = SafeExpandEnvironmentVariables(string.Join(Environment.NewLine, additionalInformationList));
                }

                nuspec = new Nuspec(id, versionString, readmeFile, additionalInformation);

                if (!excludeProjects)
                {
                    foreach (var p in CsProjects)
                    {
                        if (p.IsPackable)
                        {
                            nuspec.AddLibraryFile(p.Framework, p.GetAssemblyPath(targetConfig));
                            var pdb = p.GetPdbPath(targetConfig);
                            if (File.Exists(pdb))
                            {
                                nuspec.AddLibraryFile(p.Framework, pdb);
                            }
                        }
                    }
                }

                if (!excludeProjects)
                {
                    foreach (var p in Packages)
                    {
                        nuspec.AddDependeciesPacket(p);
                    }
                }

                if (nuget.content != null)
                {
                    foreach (var assemblyRef in nuget.content)
                    {
                        var m = new Microsoft.Extensions.FileSystemGlobbing.Matcher();
                        var path = Path.GetFullPath(SafeExpandAndTrimEnvironmentVariables(assemblyRef.Value));
                        var basePath = Path.GetDirectoryName(path).Split("*").First();
                        var filter = path.Remove(0, basePath.Length).Replace("\\", "/");
                        var escapedBasePath = basePath.Replace("\\", "/");
                        m.AddInclude(string.Join("*", filter));

                        var filtered = Microsoft.Extensions.FileSystemGlobbing.MatcherExtensions.GetResultsInFullPath(m, escapedBasePath);

                        Assert(filtered?.Count() > 0, $"The provided content assembly-path in the <nuget> element did not match any file.\n{assemblyRef.Value}");

                        foreach (var f in filtered)
                        {
                            Assert(assemblyRef.targetFramework != null, $"The targetFramework attribute in the assembly element is not set. The value is: {assemblyRef.Value}");
                            nuspec.AddLibraryFile(assemblyRef.targetFramework, f);
                        }
                    }
                }
            }
            return nuspec;
        }

        /// <summary>
        /// If field not specified, assume that it is a .NET lib.
        /// This function assumes that the provided package is either "other" or a .NET implementation assembly.
        /// </summary>
        private static NugetPackageType IsDotNet(PackageType p)
        {
            if (!p.IsDotNetLibSpecified || p.IsDotNetLib)
            {
                return NugetPackageType.DotNetImplementationAssembly;
            }
            return NugetPackageType.Other;
        }

        /// <summary>
        /// Set all environment variable as they are.
        /// Set only if not existing yet, to allow the build server to override values.
        /// </summary>
        /// <param name="env"></param>
        static void SetAll(Dictionary<string, string> env)
        {
            foreach (var e in env)
            {
                if (Environment.GetEnvironmentVariable(e.Key) == null)
                    Environment.SetEnvironmentVariable(e.Key, e.Value);
            }
        }        

        //This application runs in a ST apartment state, this seems to cause issue on long operation in the SVN client.
        //For this reason, these kind of operation have to be execute in a MT apartment state
        private void ExecuteLongSvnOperation(Action op)
        {
            Exception threadEx = null;
            System.Threading.Thread th = new System.Threading.Thread(() =>
            {
                try
                {
                    op();
                }
                catch (Exception e)
                {
                    threadEx = e;
                }
            });
            th.IsBackground = true;
            th.SetApartmentState(System.Threading.ApartmentState.MTA);
            th.Start();
            th.Join();
            if (threadEx != null)
            {
                throw new Exception(string.Format("Unable to execute SVN operation. See inner exception for more details", threadEx));
            }
        }

        private void Assert(bool condition, string msg,  params object[] args)
        {
            if (!condition)
            {
                throw new Exception(string.Format(msg, args));
            }
        }

        public bool ParseBoolean(string strValue, bool? defaultValue = null)
        {
            bool parsedValue = false;
            if (ParseBoolean(strValue, out parsedValue))
            {
                return parsedValue;
            }
            if (defaultValue.HasValue)
                return defaultValue.Value;
            throw new FormatException(string.Format("Error parsing value as boolean {0}", strValue));
        }

        private bool ParseBoolean(string value, out bool parsedValue)
        {
            if (bool.TryParse(value, out parsedValue))
            {
                return true;
            }

            int intVal;
            if (int.TryParse(value, out intVal))
            {
                parsedValue = intVal != 0;
                return true;
            }
            return false;
        }
    }
}
