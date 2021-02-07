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

        string _slnxPath;
        string _slnxDirectory;
        string _slnxFile;
        string _slnxName;
        SlnXType _slnx;
        Logger _logger = Logger.Instance;

        Dictionary<string, string> _environmentVariables = new Dictionary<string, string>();
        Dictionary<string, List<PackageType>> _packageBundles = new Dictionary<string, List<PackageType>>();
        List<SlnxHandler> _imports = new List<SlnxHandler>();
        List<Project> _projects = null;
        Dictionary<string, NugetPackage> _packages = new Dictionary<string, NugetPackage>();

        public SlnxHandler(string fName) : this(fName, null)
        {
        }

        public SlnxHandler(string fName, SlnXType userSettings)
        {
            _slnxPath = Path.GetFullPath(fName);
            Assert(File.Exists(_slnxPath), "The provided SlnX file '{0}' is not a file or it doesn't exists", _slnxPath);
            _slnxDirectory = Path.GetDirectoryName(_slnxPath);
            _slnxFile = Path.GetFileName(fName);
            _slnxName = Path.GetFileNameWithoutExtension(fName);
            _environmentVariables["_slnx_"] = _slnxDirectory;

            _slnx = ReadSlnx(fName);

            ExtendDictionary(_environmentVariables, _slnx.env, true);
            ExtendDictionary(_packageBundles, _slnx.bundle, true);

            TryApplyUserPreferences(_environmentVariables, userSettings); //Eventually apply user settings

            SetAll(_environmentVariables); // to allow import having env variable in the path

            ExpandAll(_environmentVariables); // to allow import having env variable with special keys in the path

            ReadImports(_environmentVariables, _packageBundles, _packages);

            TryApplyUserPreferences(_environmentVariables, userSettings); //re-Apply, to override value from the imports

            ExpandAll(_environmentVariables); // to apply imports & user env vars 

            FindProjects(_slnx.project, ProjectsSearchPath, _slnx.skip);
            ExtendDictionary(_packages, _slnx.package, true);
        }

        public string ProjectsSearchPath
        {
            get
            {
                if (_slnx?.searchPath != null)
                    return Environment.ExpandEnvironmentVariables(_slnx?.searchPath);
                return null;
            }
        }

        public string PackagesPath
        {
            get
            {
                if (_slnx?.packagesPath != null)
                    return Environment.ExpandEnvironmentVariables(_slnx?.packagesPath);
                return Path.Combine(SlnxDirectory, DefaultPackagesFolderName);
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

        public string SlnxName
        {
            get { return _slnxName; }
        }

        public string SlnPath
        {
            get
            {
                var slnFile = string.Format("{0}\\{1}{2}", Path.GetDirectoryName(SlnxPath), Path.GetFileNameWithoutExtension(SlnxPath), SlnExtension);
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

        public IEnumerable<NugetPackage> Packages
        {
            get
            {
                return _packages.Values;
            }
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

        void FindProjects(ProjectType[] requestedGlobalSettingsProjects, string searchPath, string skip)
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

                InspectProjects(requestedGlobalSettingsProjects, knownProjects, skip);
            }
        }

        void InspectProjects(ProjectType[] requestedProjectsXmlType, IEnumerable<string> knownProjects, string skip)
        {
            List<string> skipList = new List<string>();
            if (!string.IsNullOrEmpty(skip))
            {
                foreach (var s in skip.Split(';'))
                    skipList.Add(Environment.ExpandEnvironmentVariables(s));
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

                var p = new Project(knownProject[0], requestedProject.container);
                _projects.Add(p);

                if (p.Item != null)
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
                            _projects.Add(new Project(c, parent));

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
        private void ReadImports(Dictionary<string, string> env, Dictionary<string, List<PackageType>> bundle, Dictionary<string, NugetPackage> packages)
        {
            //Evaluate eventually defined import(s)
            if (_slnx.import != null)
            {
                foreach (var import in _slnx.import)
                {
                    Assert(string.IsNullOrEmpty(import.path) ^ string.IsNullOrEmpty(import.bundle), "path and bundle are exclusive attributes in an import element");
                    if (!string.IsNullOrEmpty(import.path)) //File import
                    {
                        var slnxImportFile = Path.GetFullPath(Environment.ExpandEnvironmentVariables(import.path));

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
                        ExtendDictionary(packages, bundle[import.bundle].ToArray(), false);
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
        private void TryApplyUserPreferences(Dictionary<string, string> env, SlnXType slnxUser)
        {
            if (slnxUser != null)
            {
                if (slnxUser.env != null)
                {
                    foreach (var e in slnxUser.env)
                    {
                        if (env.ContainsKey(e.name))
                        {
                            env[e.name] = e.Value; //Override the local value with the user one
                            Environment.SetEnvironmentVariable(e.name, e.Value); //Override the already set value with the user one
                        }
                    }
                }
            }
        }
        
        private void ExpandAll(Dictionary<string, string> env)
        {
            foreach (var e in env)
            {
                try
                {
                    var value = Environment.GetEnvironmentVariable(e.Key); //e.Value;

                    //System.Diagnostics.Debug.WriteLine("{0}={1}", e.Key, Environment.ExpandEnvironmentVariables(value));
                    Environment.SetEnvironmentVariable(e.Key, Environment.ExpandEnvironmentVariables(value));
                }
                catch (Exception ex)
                {
                    throw new Exception(string.Format("Error expanding the variable: {0} = {1}\n", e.Key, e.Value) , ex);
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

        private void ExtendDictionary(Dictionary<string, NugetPackage> packages, PackageType[] importedValues, bool overrideValues)
        {
            if (importedValues != null)
            {
                foreach (var e in importedValues)
                {
                    if (!packages.ContainsKey(e.id) || overrideValues)
                    {
                        packages[e.id] = new NugetPackage(e.id, e.version, e.targetFramework, e.source, e.var, IsDotNet(e), PackagesPath);
                    }
                }
            }
        }

        /// <summary>
        /// If field not specified, assume that it is a .NET lib
        /// </summary>
        private static bool IsDotNet(PackageType p)
        {
            return !p.IsDotNetLibSpecified || p.IsDotNetLib; 
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
