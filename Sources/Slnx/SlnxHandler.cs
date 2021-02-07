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
        string _svnSearchPath;
        string _slnxDirectory;
        string _slnxFile;
        string _slnxName;
        SlnXType _slnx;
        Logger _logger;

        Dictionary<string, string> _environmentVariables = new Dictionary<string, string>();
        Dictionary<string, List<PackageType>> _packageBundles = new Dictionary<string, List<PackageType>>();
        Dictionary<string, string> _knownVariables = new Dictionary<string, string>();
        List<SlnxHandler> _imports = new List<SlnxHandler>();
        List<Project> _projects = null;
        Dictionary<string, NugetPackage> _packages = new Dictionary<string, NugetPackage>();

        public SlnxHandler(string fName, bool ignoreBranches = false) : this(fName, null, null, ignoreBranches)
        {
        }

        public SlnxHandler(string fName, string svnSearchPath) : this(fName, svnSearchPath, null, false)
        {
        }

        public SlnxHandler(string fName, SlnXType userSettings, bool ignoreBranches = false) : this(fName, null, userSettings, ignoreBranches)
        {
        }

        public SlnxHandler(string fName, string svnSearchPath, SlnXType userSettings, bool ignoreBranches = false)
        {
            if (!(!ignoreBranches || (ignoreBranches && string.IsNullOrEmpty(svnSearchPath))))
            {
                throw new ArgumentOutOfRangeException("Ignoring branches with the SVN search path set is not allowed. It will lead to many ambigous projects (trunk & tags & branches)");
            }
            
            _slnxPath = Path.GetFullPath(fName);
            Assert(File.Exists(_slnxPath), "The provided SlnX file '{0}' is not a file or it doesn't exists", _slnxPath);
            _slnxDirectory = Path.GetDirectoryName(_slnxPath);
            _slnxFile = Path.GetFileName(fName);
            _slnxName = Path.GetFileNameWithoutExtension(fName);
            _knownVariables["$slnx"] = _slnxDirectory;
            IgnoreBranches = ignoreBranches;

            _slnx = ReadSlnx(fName);

            AutoUpdateSources = ParseBoolean(_slnx?.autoupdate, true);

            ExtendDictionary(_environmentVariables, _slnx.env, true);
            ExtendDictionary(_packageBundles, _slnx.bundle, true);

            TryApplyUserPreferences(_environmentVariables, userSettings); //Eventually apply user settings

            SetAll(_environmentVariables); // to allow import having env variable in the path

            ExpandAll(_environmentVariables); // to allow import having env variable with special keys in the path

            ReadImports(_environmentVariables, _packageBundles, _packages);

            TryApplyUserPreferences(_environmentVariables, userSettings); //re-Apply, to override value from the imports

            ExpandAll(_environmentVariables); // to apply imports & user env vars 

            var searchPath = LocalSearchPath;
            if (!string.IsNullOrEmpty(svnSearchPath))
            {
                _svnSearchPath = Environment.ExpandEnvironmentVariables(svnSearchPath);
                searchPath = SvnSearchPath;
            }

            FindProjects(_slnx.project, searchPath, _slnx.skip, "trunk", ignoreBranches);
            ExtendDictionary(_packages, _slnx.package, true);
        }

        public string LocalSearchPath
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

        public string SvnSearchPath
        {
            get { return _svnSearchPath; }
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

        public bool IgnoreBranches
        {
            get;
            private set;
        }

        public bool AutoUpdateSources
        {
            get;
            private set;
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

        /// <summary>
        /// List of path (to be used only when running locally!) of required branches.
        /// The returned list contains both projects and SlnX files to be branched.
        /// </summary>
        public IEnumerable<Interfaces.IBranchable> NeededBranches
        {
            get
            {
                return ListAllRequiredBranches(true);
            }
        }

        /// <summary>
        /// List of path (to be used only when running locally!) of required tags.
        /// The returned list contains both projects and SlnX files to be tagged.
        /// </summary>
        public IEnumerable<Interfaces.IBranchable> NeededTags
        {
            get
            {
                return ListAllRequiredBranches(false);
            }
        }

        public IEnumerable<Project> NeededSvnBranches
        {
            get
            {
                SortedSet<Project> co = new SortedSet<Project>();

                foreach (var import in _imports)
                {
                    foreach (var c in import.NeededSvnBranches)
                    {
                        co.Add(c);
                    }
                }

                foreach (var p in Projects.Where((x) => x.BranchableDirectory != null))
                {
                    if (!p.IsLocalFile)
                    {
                        co.Add(p);
                    }
                }
                return co;
            }
        }
        
        public void CheckoutApplication()
        {
            CheckoutApplication(LocalSearchPath);
        }

        public void CheckoutApplication(string checkoutRoot)
        {
            Assert(!IgnoreBranches, "Unable to checkout the application if the ignore branches flag is set.");
            _logger.Info("Application checkout in: {0}", checkoutRoot);

            if (!Directory.Exists(checkoutRoot))
            {
                Directory.CreateDirectory(checkoutRoot);
            }

            foreach(var svnRoot in NeededSvnBranches)
            {
                var checkoutFolder = Path.Combine(checkoutRoot, svnRoot.CommonRootName, svnRoot.Branch.Replace('/', '\\'));
                _logger.Info("Checking/Updating {0}", checkoutFolder);
                if (!Directory.Exists(checkoutFolder))
                {
                    ExecuteLongSvnOperation(() => { /*SvnAccess.Instance.CheckOut(svnRoot.BranchableDirectory, checkoutFolder);*/ });
                }
                else if (AutoUpdateSources)
                {
                    ExecuteLongSvnOperation(() => { /*SvnAccess.Instance.Update(checkoutFolder);*/ });
                }
                _logger.Info("Done!");
            }
            _logger.Info("Application successfully checked out.");
        }

        public void TagApplication(string tagName)
        {
            BranchTagApplication(tagName, true);
        }

        public void BranchApplication(string tagName)
        {
            BranchTagApplication(tagName, false);
        }

        private SortedSet<Interfaces.IBranchable> ListAllRequiredBranches(bool allowTaggedElements)
        {
            SortedSet<Interfaces.IBranchable> branches = new SortedSet<Interfaces.IBranchable>();
            if (allowTaggedElements || !SlnxPath.ToLower().Contains("tags"))
                branches.Add(new SlnxFile(SlnxPath));

            foreach (var import in _imports)
            {
                foreach (var b in import.NeededBranches)
                {
                    if (allowTaggedElements || !b.BranchableDirectory.ToLower().Contains("tags"))
                        branches.Add(b);
                }
            }

            foreach (var p in Projects.Where((x) => x.Branch != null))
            {
                Assert(System.IO.Directory.Exists(p.BranchableDirectory), "Branchable directory {0} for the project {1} not found", p.BranchableDirectory, p.FullPath);

                if (allowTaggedElements || !p.BranchableDirectory.ToLower().Contains("tags"))
                {
                    branches.Add(p);
                }
            }
            return branches;
        }

        /// <summary>
        /// It tags/branches all the necessary projects roots.
        /// </summary>
        /// <param name="tagName">The tag plain name (without the "tags" or "branchs" marking!). It needs to be a valid folder name</param>
        private void BranchTagApplication(string tagName, bool isTag)
        {
            throw new NotImplementedException();
            /*
            Assert(!IgnoreBranches, "Unable to branch the application if the ignore branches flag is set.");
            Assert(SvnSearchPath == null, "It is not possible to tag/branch the application if the project are searched in a SVN localtion.");

            string tagDir = isTag ? "tags" : "branches";

            tagName = tagName.Replace(" ", "");
            foreach(var c in Path.GetInvalidFileNameChars())
            {
                Assert(!tagName.Contains(c), "The caharacter '{0}' in the tag/branch name is invalid", c);
            }

            var requiredElements = isTag ? NeededTags : NeededBranches;

            foreach (var toBranch in requiredElements)
            {
                //if (SvnAccess.Instance.HasUncommittedChanges(branchDir))
                //{
                //    //Todo LOG ?
                //}
                string tagSource = SvnAccess.Instance.GetRepositoryPath(toBranch.BranchableDirectory); 
                var tagDest = GetSvnTagDirectory(toBranch.BranchableDirectory, tagDir, tagName);
                ExecuteLongSvnOperation(() => { SvnAccess.Instance.Copy(tagSource, tagDest, string.Format("[{0}] Automatic tag/branch {1}", SlnxName, tagName)); });

                if (toBranch is SlnxFile) //Need to patch this file !
                {
                    var tmpDir = HIMS.Services.Core.PredefinedFolders.GetAndEnsureLocalPath("SlnXCheckouts");
                    var tmpPath = Path.Combine(tmpDir, tagName, Path.GetFileName(toBranch.FullPath));

                    try
                    {
                        ExecuteLongSvnOperation(() => { SvnAccess.Instance.CheckOutFile(tagDest, tmpDir); });
                        var slnx = ReadSlnx(tmpPath);
                        slnx.autoupdate = "true"; //Override in case of a wrong check-in
                        //slnx.import = 
                        if (slnx.project != null)
                        {
                            foreach (var p in slnx.project)
                            {
                                p.branch = string.Format("{0}\\{1}", tagDir, tagName);
                            }
                        }
                        //if (slnx.import != null)
                        //{
                        //    foreach (var i in slnx.import)
                        //    {
                        //        i.path = ??
                        //    }
                        //}
                        WriteSlnx(tmpPath, slnx);
                        ExecuteLongSvnOperation(() => { SvnAccess.Instance.CommitChanges(tmpPath, string.Format("[{0}] Automatic SlnX tag/branch-fix on {1}", SlnxName, tagName)); });
                    }
                    finally
                    {
                        Directory.Delete(tmpDir, true);
                    }
                }
            }*/
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

        private string GetSvnTagDirectory(string dir, string tagDir, string tagName)
        {
            Assert(Directory.Exists(dir), "The directory to branch does not exist locally: {0}", dir);

            int endIndex = -1;
            foreach (var v in new[] { "trunk", "tags", "branches" })
            {
                endIndex = dir.IndexOf(v);
                if (endIndex >= 0) break;
            }
            Assert(endIndex >= 0, "The directory to branch does is invalid (no tags/trunk/branches markup): {0}", dir);
            var root = dir.Substring(0, endIndex);
            var svnRoot = ""/*SvnAccess.Instance.GetRepositoryPath(root)*/;
            return string.Format("{0}{1}/{2}", svnRoot, tagDir, tagName);
        }

        void FindProjects(ProjectType[] requestedGlobalSettingsProjects,
            string searchPath, string skip, string defaultBranch,
            bool ignoreBranches)
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

                InspectProjects(requestedGlobalSettingsProjects, knownProjects, skip, defaultBranch, ignoreBranches);
            }
        }

        string TrimBranchName(string requestedBranch, string defaultBranch, bool ignoreBranches)
        {
            if (!ignoreBranches)
            {
                if (!string.IsNullOrEmpty(requestedBranch))
                {
                    return requestedBranch.Trim();
                }
                if (string.IsNullOrEmpty(requestedBranch))
                    return defaultBranch;
            }
            return null;
        }

        void InspectProjects(ProjectType[] requestedGlobalSettingsProjects,
            IEnumerable<string> knownProjects, string skip, string defaultBranch, bool ignoreBranches)
        {
            List<string> skipList = new List<string>();
            if (!string.IsNullOrEmpty(skip))
            {
                foreach (var s in skip.Split(';'))
                    skipList.Add(Environment.ExpandEnvironmentVariables(s));
            }

            List<ProjectType> requestedProjects = new List<ProjectType>();

            //Expand all wildcards
            foreach (var requestedProject in requestedGlobalSettingsProjects)
            {
                var requestedBranch = TrimBranchName(requestedProject.branch, defaultBranch, ignoreBranches);
               
                if (requestedProject.name.Contains("*")) //Check wildcards
                {
                    var wildCardMatches = knownProjects.Where((x) => FilterProjectByBranchAndName(x, requestedProject.name, requestedBranch, skipList)).ToList();
                    var wildCardProjects = wildCardMatches.ConvertAll<ProjectType>((name) =>
                                                {
                                                    var p = new ProjectType();
                                                    p.name = Path.GetFileNameWithoutExtension(name);
                                                    p.container = requestedProject.container;
                                                    p.branch = requestedProject.branch;
                                                    p.Value = requestedProject.Value;
                                                    return p;
                                                }
                                            );

                    requestedProjects.AddRange(wildCardProjects);
                }
                else
                {
                    requestedProjects.Add(requestedProject);
                }
            }

            //Check projects's existance and ambiguity (also for wildcard imported projects)
            foreach (var requestedProject in requestedProjects)
            {
                var requestedBranch = TrimBranchName(requestedProject.branch, defaultBranch, ignoreBranches);
                var requestedCsProjName = string.Format("{0}.{1}", requestedProject.name, CsProject.FileExtension);

                var knownProject = knownProjects.Where((x) => FilterProjectByBranchAndName(x, requestedCsProjName, requestedBranch, skipList)).ToList();

                if (knownProject.Count == 0)
                    throw new Exception(string.Format("Project '{0}' not found in any branch with name '{1}' !", requestedProject.name, requestedBranch));
                if (knownProject.Count > 1)
                    throw new Exception(string.Format("Project '{0}' is ambiguous!\n\n{1}", requestedProject.name, string.Join("\n\n", knownProject)));

                var p = new Project(knownProject[0], requestedProject.container, ignoreBranches);
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
                            _projects.Add(new Project(c, parent, ignoreBranches));

                        parent = currentFullPath;
                    }
                }
            }
        }

        bool FilterProjectByBranchAndName(string path, string requestedCsProjName, string branch, List<string> skipList)
        {
            var normalizedPath = path.Replace('/', '\\');
            var normalizedBranch = branch?.Replace('/', '\\');

            var branchMatch = (string.IsNullOrEmpty(normalizedBranch) || normalizedPath.ToLower().Contains(normalizedBranch.ToLower()));
            var projectMatch = false;

            if (branchMatch)
            {
                var projectCandidate = normalizedPath.Split('\\').LastOrDefault();
                projectMatch = new Regex(Regex.Escape(requestedCsProjName).Replace(@"\*", ".*")).IsMatch(projectCandidate);
                //projectMatch = path.EndsWith(requestedCsProjName);

                if (projectMatch && skipList != null)
                {
                    foreach (var skip in skipList)
                    {
                        if (path.Contains(skip))
                            return false;
                    }
                }
            }
            return branchMatch && projectMatch;
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
                if (slnxUser.autoupdate != null)
                {
                    try
                    {
                        AutoUpdateSources = ParseBoolean(slnxUser.autoupdate, true);
                    }
                    catch (FormatException) { }
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
                    else if (_knownVariables.ContainsKey(e.Value))
                    {
                        env.Add(e.name, _knownVariables[e.Value]);
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
                        packages[e.id] = new NugetPackage(e.id, e.version, e.targetFramework, e.source, e.var, e.IsDotNetLib, PackagesPath);
                    }
                }
            }
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
