﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Serialization;
using System.Text.RegularExpressions;
using Slnx.Generated;
using NugetHelper;
using Ganss.IO;

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
        List<CsProject> _projects = null;
        List<NugetPackage> _packages = new List<NugetPackage>();
        Dictionary<string, string> _packagesToDebug = new Dictionary<string, string>();
        Dictionary<NugetPackage, SlnxHandler> _debugSlnxItems = new Dictionary<NugetPackage, SlnxHandler>();

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

            if (string.IsNullOrEmpty(debugPackageId)) //Main SlnX
            {
                EvalueteDebugPackages(_slnx.debug);
                EvalueteDebugPackages(userSettings?.debug);

                //Read debug SlnX
                foreach (var item in _packagesToDebug)
                {
                    var debugSourcePakckage = Packages.Where(x => x.Id == item.Key).FirstOrDefault();
                    Assert(debugSourcePakckage != null, $"The package {item.Key} is marked for debug, but it is not present as nuget package in the main SlnX file.");

                    var slnxItem = new SlnxHandler(item.Value, item.Key);
                    _debugSlnxItems[debugSourcePakckage] = slnxItem;
                    _packages.Remove(debugSourcePakckage);

                    foreach (var candidate in slnxItem.Packages)
                    {
                        if (_packagesToDebug.ContainsKey(candidate.Id))
                        {
                            //This package is under debug, no version check needed.
                            continue;
                        }

                        var known = Packages.Where(x => x.Id == candidate.Id).FirstOrDefault();
                        if (known == null)
                        {
                            _logger.Warn($"The package {candidate} required by the SlnX {item.Key} selected for debug, is not present in the current SlnX file {_slnxName}{SlnxExtension}");
                        }
                        else
                        {
                            Assert(known.MinVersion == candidate.MinVersion &&
                                   known.TargetFramework == candidate.TargetFramework &&
                                   known.PackageType == candidate.PackageType,
                                   $"The package {candidate} required by the SlnX {item.Key} selected for debug, does not match the already known one {known}");
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

        public IEnumerable<CsProject> Projects
        {
            get
            {
                return _projects;
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

        /// <summary>
        /// Return a IEnumerable containing all knwon packages, included the one marked for debugging.
        /// </summary>
        public IEnumerable<NugetPackage> AllPackages
        {
            get
            {
                var allPackages = Packages.ToList();
                if (DebugSlnxItems.Count() != 0)
                {
                    allPackages.AddRange(DebugSlnxItems.Keys);
                }
                return allPackages;
            }
        }

        public IEnumerable<CsProject> ProjectsImportedFromDebugSlnx
        {
            get
            {
                return _debugSlnxItems.Values.SelectMany(x => x.Projects.Where(p => !p.IsTestProject));
            }
        }

        public IEnumerable<NugetPackage> PackagesImportedFromDebugSlnx
        {
            get
            {
                return _debugSlnxItems.Values.SelectMany(x => x.Packages);
            }
        }

        public Dictionary<NugetPackage, SlnxHandler> DebugSlnxItems
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
                _logger.Info($"No NuGet package found. If this is not correct, it might be because this method was called before installing the NuGet packages.");
            }

            foreach (var csProj in Projects)
            {
                _logger.Info($"Trying to fix the Assembly and Project reference of {csProj.Name}");
                
                csProj.TryFixProjectFileAndGatherReferences(AllPackages);
                csProj.SaveCsProjectToFile();
            }
        }

        void FindProjects(ProjectType[] requestedGlobalSettingsProjects, string searchPath, string skip, string enforcedContainer)
        {
            _projects = new List<CsProject>();
            if (requestedGlobalSettingsProjects != null)
            {
                IEnumerable<string> knownProjects = null;

                if (Directory.Exists(searchPath))
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
                    container = string.Join("/", enforcedContainer, container);
                }

                var p = new CsProject(knownProject[0], container);
                _projects.Add(p);
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
                    {
                        return false;
                    }
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
                        {
                            throw new Exception(string.Format("SLNX import not found, file path: {0}", slnxImportFile));
                        }

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
                                                     SafeExpandEnvironmentVariables(e.dependencySources)?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries),
                                                     SafeExpandEnvironmentVariables(e.var), 
                                                     IsDotNet(e), PackagesPath,
                                                     !e.dependenciesForceMinVersionSpecified || e.dependenciesForceMinVersion);

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
                var versionString = SafeExpandAndTrimEnvironmentVariables(nuget.version, null);
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
                    foreach (var p in Projects)
                    {
                        if (p.IsPackable)
                        {
                            nuspec.AddLibraryElements(p.Framework, p.GetAssemblyPath(targetConfig));
                            var pdb = p.GetPdbPath(targetConfig);
                            if (File.Exists(pdb))
                            {
                                nuspec.AddLibraryElements(p.Framework, pdb);
                            }
                        }
                    }
                }

                if (!excludePackages)
                {
                    foreach (var package in Packages)
                    {
                        var packageIsReferenced = Projects
                            .Where(refProject => refProject.IsPackable)
                            .Any(refProject => 
                                    refProject.PackageReferences.Any(refPackage => refPackage.Id == package.Id)
                            );

                        if (packageIsReferenced)
                        {
                            nuspec.AddDependeciesPacket(package);
                        }
                        else
                        {
                            Logger.Instance.Info($"The package {package} has no reference in packable projects. It will be excluded from the NuSpec dependencies.");
                        }
                    }
                }

                if (nuget.content != null)
                {
                    foreach (var item in nuget.content)
                    {
                        var value = SafeExpandAndTrimEnvironmentVariables(item.Value);
                        Assert(!string.IsNullOrEmpty(value), $"The value of the item element is not set");
                        Assert(item.targetFramework != null, $"The targetFramework attribute in the item element is not set. The value of the element is: {item.Value}");

                        var filtered = Glob.Expand(value);

                        Assert(filtered.Any(), $"The provided content item-path in the <nuget> element did not match any file.\n{item.Value}");

                        foreach (var f in filtered)
                        {
                            nuspec.AddLibraryElements(item.targetFramework, f.FullName);
                        }
                    }
                }
            }
            return nuspec;
        }

        /*private IEnumerable<string> Glob(string globPath, bool recursive)
        {
            var m = new Microsoft.Extensions.FileSystemGlobbing.Matcher();
            var path = Path.GetFullPath(SafeExpandAndTrimEnvironmentVariables(globPath));
            var basePath = Path.GetDirectoryName(path).Replace("\\", "/");
            var escapedPath = path.Replace("\\", "/");

            var basePathElments = new List<string>();
            foreach (var s in basePath.Split("/"))
            {
                if (s.Contains("*"))
                {
                    break;
                }
                basePathElments.Add(s);
            }
            var escapedBasePath = string.Join("/", basePathElments);
            var filter = escapedPath.Remove(0, escapedBasePath.Length).Replace("\\", "/");
            if (recursive)
            {*/
                //filter = string.Format($"{filter}/**/*.*");
            /*}
            m.AddInclude(filter);

            return Microsoft.Extensions.FileSystemGlobbing.MatcherExtensions.GetResultsInFullPath(m, escapedBasePath);
        }*/

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
