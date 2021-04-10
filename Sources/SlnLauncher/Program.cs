using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Serialization;
using System.IO;
using Microsoft.Win32;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Reflection;
using Slnx;
using Slnx.Generated;
using NDesk.Options;
using System.Runtime.InteropServices;
using System.Diagnostics;

// Important note:
//  The NuGet Client code requires to know its version.
//  Normally, it's able to achieve this on its own using via a kind of:
//            var assemblyVersion = typeof(NuGet.Common.ClientVersionUtility).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
//  
// When the NuGet.Common gets repacked in the SlnLauncher EXE, the NuClient relies on the SlnLauncher EXE attribute.
// For this reason it is MANDATORY to adjust the attribute "AssemblyInformationalVersion" according to the NuGet.Common.dll version in the AssemblyInfo.cs of the SlnLauncher project.
// For example adding:
// [assembly: AssemblyInformationalVersion("4.5.3.0")]

namespace SlnLauncher
{
    static class Program
    {
        const int _reqParameterN = 1;

        static bool _openSolution = false;
        static bool _createMsBuild = false;
        static bool _logEnabled = false;
        static string _pythonEnvVarsPath = null;
        static string _batchEnvVarsPath = null;
        static Logger _logger = null;
        static LogLevel _logLevel = LogLevel.Info;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] argv)
        {
            _logger = Logger.Instance;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            _openSolution = true;
            _createMsBuild = false;
            var _printVersion = false;
            var quiteExecution = false;
            var autoUpdateNugetDependencies = true;
            var nugetForceMinVersion = true;
            var loadUserFile = true;
            string nuspecDir = null;            
            var dump = false;
            string slnxFile = null;
            _pythonEnvVarsPath = null;

            OptionSet p = new OptionSet()
              .Add("v|version", "Prints the tool version in the standard output and exits.", v => _printVersion = v != null)
              .Add("q|quite", "If set (-q/-q+) no popups will be shown in case of exceptions. [Default: not set]", v => quiteExecution = v != null)
              .Add("<>", "SlnX file path", v => slnxFile = v)
              .Add("o|openSln", "If set (-o/-o+) opens the generated Sln file. If not set (-o-), the generated Sln will not be opened. [Default: set]", v => _openSolution = v != null)
              .Add("u|user", "If set (-u/-u+) it loads an eventually present .user file. [Default: set]", v => loadUserFile = v != null)
              .Add("d|dump", "If set (-d/-d+) it dumps all project paths and environment variables in dump.txt located in the SlnX location . [Default: not set]", v => dump = v != null)
              .Add("py=|pythonModule=", "Path for the python module. If set the specified python module containing all defined environment variables is created. [Default: not set]", v => _pythonEnvVarsPath = v)
              .Add("b=|batchModule=", "Path for the batch module. If set the specified batch module containing all defined environment variables is created. [Default: not set]", v => _batchEnvVarsPath = v)
              .Add("msb|msbuildModule", "If set (-msb/-msb+) a MSBuild module containing all defined environment variables is created in the SlnX location. [Default: not set]", v => _createMsBuild = v != null)
              .Add("log", "If set (-log/-log+), a log file location in the SlnX directory (or EXE if that path is invalid) will be created. [Default: false]", v => _logEnabled = v != null)
              .Add(string.Format("lv=|logVerbosity=", "Set the log level of verbosity. Valid values {0}. [Default: {1}]", string.Join(",", Enum.GetNames<LogLevel>()), _logLevel), v => _logLevel = ParseLogLevel(v))
              .Add("ns=|nuspec=", "Output path for the NuGet package created based on the current solution. [Default: not set]", v => nuspecDir = v)
              .Add("nd|nugetDependencies", "If set (-nd/-nd+), the dependencies of the provided packages will be also automatically downloaded. [Default: true]", v => autoUpdateNugetDependencies = v != null)
              .Add("nf|nugetForceMinVersion", "If set (-nf/-nf+), the tool will check that dependencies fullfill the min-version provided (not allowing newer versions). [Default: true]", v => nugetForceMinVersion = v != null);
            
            try
            {
                p.Parse(argv);
                if (_printVersion)
                {
                    Console.WriteLine("SlnLauncher v{0}", typeof(SlnxHandler).Assembly.GetName().Version.ToString(3));
                    return;
                }

                if (slnxFile == null)
                    throw new ArgumentException(string.Format("Invalid parameters, no SlnX file specified.\n\n\t{0}", string.Join("\n\t", argv)));

                slnxFile = Path.GetFullPath(slnxFile);
                if (File.Exists(slnxFile))
                {
                    Environment.CurrentDirectory = Path.GetDirectoryName(slnxFile);
                }
                if (_logEnabled)
                {
                    _logger.SetLog(Path.Join(Environment.CurrentDirectory, Path.GetFileNameWithoutExtension(typeof(Program).Assembly.Location) + ".log"), _logLevel);
                    NugetHelper.NugetHelper.SetLogger(_logger);
                }
                _logger.Info("Application started with parameters: {0}", string.Join("\n", argv));

                var slnxUserFile = string.Format("{0}{1}", slnxFile, SlnxHandler.SlnxUserExtension);

                SlnXType slnxUser = null;

                if (loadUserFile && File.Exists(slnxUserFile))
                {
                    slnxUser = SlnxHandler.ReadSlnx(slnxUserFile);
                }

                var slnx = new SlnxHandler(slnxFile, slnxUser, null);
                var originalPackageList = new List<NugetHelper.NugetPackage>(slnx.Packages);

                DownloadPackages(slnx, quiteExecution, autoUpdateNugetDependencies);                
                
                if (_createMsBuild)
                {
                    CreateMsBuildPropertiesTarget(slnx);
                }

                if (!string.IsNullOrEmpty(_pythonEnvVarsPath))
                {
                    CreatePythonnModule(slnx, Path.Combine(slnx.SlnxDirectory, _pythonEnvVarsPath));
                }
                if (!string.IsNullOrEmpty(_batchEnvVarsPath))
                {
                    CreateBatchModule(slnx, Path.Combine(slnx.SlnxDirectory, _batchEnvVarsPath));
                }
                
                if (dump)
                {
                    Dump(slnx);
                }

                var ignoreDependencyCheck = !autoUpdateNugetDependencies;
                _logger.Info($"Running dependency check with force min-version match set to {nugetForceMinVersion}, and ignore dependency is {ignoreDependencyCheck}");
                NugetHelper.NugetHelper.CheckPackagesConsistency(slnx.Packages.ToList(), nugetForceMinVersion, ignoreDependencyCheck);

                _logger.Info($"Checking debug packages consistency...");

                foreach (var debugPackage in slnx.DebugSlnxItems.Keys)
                {
                    foreach (var package in slnx.Packages)
                    {
                        if (package.Dependencies.Where(x => x.PackageDependency.Id == debugPackage.Id).Any())
                        {
                            _logger.Warn($"{package} depends on the package {debugPackage.Id} which is selected for debugging. This might cause runtime issues! Consider marking it for debugging as well.");
                        }
                    }
                }

                _logger.Info($"Check if all packages that are bind via .NET ImplementationAssemblies (lib directory) are specified in the SlnX file");
                foreach (var package in slnx.Packages.Where((x) => x.PackageType == NugetHelper.NugetPackageType.DotNetImplementationAssembly))
                {
                    if (!originalPackageList.Where((x) => x.Id == package.Id).Any())
                    {
                        _logger.Info($"The .NET implementation package {package} has been installed as dependency. Consider define it explicitly. Execute a dump to analyse dependency graph.");
                    }
                }
                
                _logger.Info($"Check if all packages that are bind via .NET CompileTimeAssemblies (ref directory) are specified in the SlnX file");
                foreach (var package in slnx.Packages.Where((x) => x.PackageType == NugetHelper.NugetPackageType.DotNetCompileTimeAssembly))
                {
                    if (originalPackageList.Where((x) => x.Id == package.Id).FirstOrDefault() == null)
                    {
                        _logger.Info($"The .NET compilte time package {package} has been installed as dependency.");
                    }
                }

                _logger.Info($"Fixing project files");
                slnx.TryFixProjectFiles();

                if (!string.IsNullOrEmpty(nuspecDir))
                {
                    nuspecDir = Path.GetFullPath(slnx.SafeExpandEnvironmentVariables(nuspecDir));
                    if (nuspecDir == slnx.SlnxDirectory)
                    {
                        throw new Exception($"The provided nuspec directory is the same as the slnx folder. Please specify a sub folder.");
                    }
                    if (!Directory.Exists(nuspecDir))
                    {
                        Directory.CreateDirectory(nuspecDir);
                    }
                    else
                    {
                        if (Directory.EnumerateFileSystemEntries(nuspecDir).Any())
                        {
                            throw new Exception($"The provided nuspec directory is not empty: '{nuspecDir}'");
                        }
                    }
                    var nuspec = slnx.GetNugetPackageInformation();
                    if (nuspec != null)
                    {
                        NugetHelper.NuspecGenerator.Generate(nuspecDir, nuspec);
                    }
                    else
                    {
                        throw new Exception("Missing or invalid nuget content information in the provided SlnX file.");
                    }
                }
                
                MakeSln(slnx);
                MakeAndCleanNugetDebugFile(slnx);

                if (_logger.MaxLogLevelDetected >= LogLevel.Warning)
                {
                    if (!quiteExecution)
                    {
                        var baseMsg = $"Warning(s) detectd. This could cause runtime issues.\nIt's highly suggested to";
                        if (_logEnabled)
                        {
                            MessageBox.Show($"{baseMsg} review them in the log file: {_logger.LogPath}", "Warning");
                        }
                        else
                        {
                            MessageBox.Show($"{baseMsg} re-run the application with log tuned on.", "Warning");
                        }
                    }
                }

                if (_openSolution)
                {
                    OpenSln(slnx.SlnPath);
                }
            }
            catch (Exception ex)
            {
                string exText = "";
                var stackTrace = ex.StackTrace;
                while (ex != null)
                {
                    exText = string.Join("\n", ex.Message);
                    ex = ex.InnerException;
                }
                _logger.Error(exText);
                _logger.Error(stackTrace);

                if (!quiteExecution)
                    MessageBox.Show(string.Format("Run the command with the option --log for more information\n\n{0}", exText), "Error");
                else
                    throw;
            }
        }

        static IEnumerable<NugetHelper.NugetPackage> PerformPackageDownloadProcess(IEnumerable<NugetHelper.NugetPackage> packages, bool quite, bool autoUpdateDependencies, string formTitle)
        {
            ProgressDialog progress = null;
            System.Threading.Thread th = null;

            if (!quite)
            {
                th = new System.Threading.Thread(
                () =>
                {
                    progress = new ProgressDialog(formTitle, packages.Count());
                    progress.ShowDialog();
                }
                );
            }
            if (th != null) th.IsBackground = true;
            th?.SetApartmentState(System.Threading.ApartmentState.STA);
            th?.Start();
            while (th != null && progress == null) System.Threading.Thread.Sleep(100);

            var ret = NugetHelper.NugetHelper.InstallPackages(packages.ToList(), autoUpdateDependencies, (message) =>
            {
                _logger.Debug("Package {0} successefully installed", message.ToString());
                progress?.IncrementProgress();
            }).ToList();

            _logger.Info("Done!");
            progress?.Close();
            th?.Join();

            return ret;
        }

        static void DownloadPackages(SlnxHandler slnx, bool quite, bool autoUpdateDependencies)
        {
            _logger.Info("Downloading required NuGet packages...");

            slnx.Packages = PerformPackageDownloadProcess(slnx.Packages, quite, autoUpdateDependencies, "Loading packages...");

            if (slnx.DebugSlnxItems.Count != 0)
            {
                _logger.Info("Downloading NuGet packages marked as debug...");
                _logger.Trace("Need to download the package to properly gather the Libraries list. The dependencies are ignored to avoid package versions issues.");

                foreach (var nugetPackage in slnx.DebugSlnxItems.Keys)
                {
                    var installed = PerformPackageDownloadProcess(new[] { nugetPackage }, quite, false, $"Loading debug package {nugetPackage} without dependencies...");
                    nugetPackage.AddLibraries(installed.First().Libraries);
                }
            }
        }

        static List<string> GetAllKeys(SlnxHandler slnx)
        {
            var keys = slnx.Projects.Select((x) => x.EnvironmentVariableKey).ToList();
            keys.AddRange(slnx.EnvironementVariables.Keys);
            keys.AddRange(slnx.Packages.SelectMany(x => x.EnvironmentVariableKeys));
            keys.AddRange(slnx.Packages.Where(x => x.EnvironmentVariableAdditionalKey != null).Select(x => x.EnvironmentVariableAdditionalKey));
            return keys;
        }

        static void CreateMsBuildPropertiesTarget(SlnxHandler slnx)
        {
            string outDir = slnx.SlnxDirectory;
            _logger.Info("Creating MS Build targets in {0}", outDir);
            using (var f = new StreamWriter(Path.Combine(outDir, "MsBuildGeneratedProperties.targets")))
            {
                var keys = GetAllKeys(slnx);

                f.WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
                f.WriteLine("<Project ToolsVersion=\"4.0\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">");
                f.WriteLine("    <PropertyGroup>");
                foreach (var key in keys)
                {
                    var value = slnx.SafeExpandEnvironmentVariables(Environment.GetEnvironmentVariable(key));
                    f.WriteLine("        <{0}>{1}</{0}>", key, value);
                }
                f.WriteLine("\n        <MsBuildGeneratedProperties Condition=\" '$(MsBuildGeneratedProperties)' == '' \">");

                foreach (var key in keys)
                {
                    var value = slnx.SafeExpandEnvironmentVariables(Environment.GetEnvironmentVariable(key));
                    f.WriteLine("            {0}={1};", key, value);
                }

                f.WriteLine("        </MsBuildGeneratedProperties>");
                f.WriteLine("    </PropertyGroup>");
                f.WriteLine("</Project>");
            }
            _logger.Info("Done!");
        }

        static void Dump(SlnxHandler slnx)
        {
            string outDir = slnx.SlnxDirectory;
            _logger.Info("Dumping SlnX info in {0}", outDir);

            using (var f = new StreamWriter(Path.Combine(outDir, "dump.txt")))
            {
                f.WriteLine("CS Projects:\n");
                foreach (var p in slnx.Projects)
                {
                    f.WriteLine("{0,-40} => {1}", p.Name, p.FullPath);
                }

                f.WriteLine("\nCS Projects imported for debugging:\n");
                foreach (var p in slnx.ProjectsImportedFromDebugSlnx)
                {
                    f.WriteLine("{0,-40} => {1}", p.Name, p.FullPath);
                }

                f.WriteLine("------------------------------------\n");
                f.WriteLine("NuGet packages:\n");
                foreach (var p in slnx.Packages)
                {
                    f.WriteLine("{0,-40} => {1} [{2}]", p, p.FullPath, p.PackageType);
                    foreach (var d in p.Dependencies)
                    {
                        f.WriteLine("    | {0,-20} => {1} [{2}]", d.PackageDependency.Id, d.PackageDependency.VersionRange, p.PackageType);
                    }
                }

                f.WriteLine("\nNuGet packages required by the projects imported for debugging:\n");
                foreach (var p in slnx.PackagesImportedFromDebugSlnx)
                {
                    f.WriteLine("{0,-40} => {1} [{2}]", p, p.FullPath, p.PackageType);
                    foreach (var d in p.Dependencies)
                    {
                        f.WriteLine("    | {0,-20} => {1} [{2}]", d.PackageDependency.Id, d.PackageDependency.VersionRange, p.PackageType);
                    }
                }

                f.WriteLine("------------------------------------\n");
                f.WriteLine("Environment variables:\n");

                var keys = GetAllKeys(slnx);

                foreach (var key in keys)
                {
                    var envVar = Environment.GetEnvironmentVariable(key);
                    string value = null;
                    if (envVar != null)
                    {
                        value = slnx.SafeExpandEnvironmentVariables(envVar);
                    }
                    f.WriteLine("{0} = {1}", key, value);
                }
            }
            _logger.Info("Done!");
        }

        static void CreatePythonnModule(SlnxHandler slnx, string outDir)
        {
            _logger.Info("Creating Python module in {0}", outDir);

            using (var f = new StreamWriter(Path.Combine(outDir, "SetEnvVars.py")))
            {
                var keys = GetAllKeys(slnx);

                f.WriteLine("import os\n");

                foreach (var key in keys)
                {
                    var value = slnx.SafeExpandEnvironmentVariables(Environment.GetEnvironmentVariable(key));
                    f.WriteLine("os.environ['{0}'] = r'{1}'", key, value);
                }
            }
            _logger.Info("Done!");
        }

        static void CreateBatchModule(SlnxHandler slnx, string outDir)
        {
            _logger.Info("Creating Batch module in {0}", outDir);

            using (var f = new StreamWriter(Path.Combine(outDir, "SetEnvVars.bat")))
            {
                var keys = GetAllKeys(slnx);

                foreach (var key in keys)
                {
                    var value = slnx.SafeExpandEnvironmentVariables(Environment.GetEnvironmentVariable(key));
                    f.WriteLine("set {0}={1}", key, value);
                }
            }
            _logger.Info("Done!");
        }

        static void OpenSln(string sln)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                new Process
                {
                    StartInfo = new ProcessStartInfo($"{sln}")
                    {
                        UseShellExecute = true
                    }
                }.Start();
                //Process.Start(new ProcessStartInfo("cmd", $"/c start {sln}"));
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", sln);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", sln);
            }
        }

        static void MakeSln(SlnxHandler slnx)
        {
            string outFile = slnx.SlnPath;
            if (Path.GetExtension(outFile).ToLower() != SlnxHandler.SlnExtension)
            {
                throw new Exception(string.Format("The configured sln file is not support. Only '{0}' file are supported\n\n\tFile='{1}'", SlnxHandler.SlnExtension, outFile));
            }
            _logger.Info($"Creating solution file: {outFile}");

            var projects = slnx.Projects.ToList();
            projects.AddRange(slnx.ProjectsImportedFromDebugSlnx);
            _logger.Trace($"Found {projects.Count()} projects.");
            _logger.Trace("Inspecting and creating containers");

            var projectsAndContainers = new List<SlnItem>();
            foreach (var p in projects)
            {
                projectsAndContainers.Add(p);

                if (p.Container != null)
                {
                    var containers = p.Container.Split('/');

                    string parent = null;
                    string currentFullPath = null;
                    foreach (var c in containers)
                    {
                        if (string.IsNullOrEmpty(c)) continue;

                        if (parent == null)
                        {
                            currentFullPath = c;
                        }
                        else
                        {
                            currentFullPath = string.Format("{0}/{1}", currentFullPath, c);
                        }

                        if (projectsAndContainers.Where((x) => x.IsContainer && x.FullPath == currentFullPath).Count() == 0) //Need to create the container
                        {
                            projectsAndContainers.Add(new CsContainer(c, parent));
                        }

                        parent = currentFullPath;
                    }
                }
            }

            StringBuilder slnSb = new StringBuilder();
            StringBuilder projectListSb = new StringBuilder();
            StringBuilder buildConfigSb = new StringBuilder();
            StringBuilder containerConfigSb = new StringBuilder();

            foreach (var p in projectsAndContainers)
            {
                var path = p.FullPath;
                if (p.IsContainer)
                    path = p.Name;

                projectListSb.Append(p.ToString());

                var buildCfg = p.GetBuildConfiguration();
                if (buildCfg != null)
                    buildConfigSb.Append(buildCfg);

                var container = projectsAndContainers.Where((x) => x.IsContainer && x.FullPath == p.Container).ToList();
                if (container.Count > 0)
                {
                    containerConfigSb.AppendFormat("\n		{{{0}}} = {{{1}}}", p.ProjectGuid, container[0].ProjectGuid);
                }
            }

            //Header
            slnSb.Append(@"Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 16
VisualStudioVersion = 16.0.30907.101
MinimumVisualStudioVersion = 10.0.40219.1");
            //Project list
            slnSb.Append(projectListSb);

            //Build config
            slnSb.Append(@"
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Debug|Mixed Platforms = Debug|Mixed Platforms
		Debug|x86 = Debug|x86
		Release|Any CPU = Release|Any CPU
		Release|Mixed Platforms = Release|Mixed Platforms
		Release|x86 = Release|x86
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution");
            slnSb.Append(buildConfigSb);

            //Containers config
            slnSb.Append(@"
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
	GlobalSection(NestedProjects) = preSolution");

            slnSb.Append(containerConfigSb);

            //End
            slnSb.Append(@"
	EndGlobalSection
	GlobalSection(ExtensibilityGlobals) = postSolution");
	slnSb.AppendFormat(@"
		SolutionGuid = {{{0}}}
	EndGlobalSection
EndGlobal
", Guid.NewGuid().ToString());
            WriteAllText(outFile, slnSb.ToString());
            _logger.Info("Done!");
        }

        static XmlDocument TryGetDebugXmlDoc(CsProject proj, NugetHelper.NugetPackage package)
        {
            if (proj.AssemblyReferences != null)
            {
                foreach (var r in proj.AssemblyReferences)
                {
                    if (package.Libraries.Any(x => Path.GetFileName(x) == Path.GetFileName(r.HintPath)))
                    {
                        var newDocument = new XmlDocument();
                        var root = newDocument.CreateNode(XmlNodeType.Element, "Project", null);
                        newDocument.AppendChild(root);
                        return newDocument;
                    }
                }
            }
            return null;
        }

        static void MakeAndCleanNugetDebugFile(SlnxHandler slnx, SlnxHandler mainSlnx = null)
        {
            foreach (string f in Directory.EnumerateFiles(slnx.SlnxDirectory, CsProject.ImportDebugProjectName, new EnumerationOptions() { RecurseSubdirectories = true }))
            {
                File.Delete(f);
            }

            var debugInfo = new Dictionary<CsProject, XmlDocument>();
            var debugItems = mainSlnx?.DebugSlnxItems ?? slnx.DebugSlnxItems;

            foreach (var item in debugItems)
            {
                var nugetPackage = item.Key;
                foreach (var p in slnx.Projects)
                {
                    if (mainSlnx != null)
                    {
                        p.TryFixProjectFileAndGatherReferences(mainSlnx.AllPackages);
                    }

                    var debugDoc = TryGetDebugXmlDoc(p, nugetPackage);
                    if (debugDoc != null)
                    {
                        if (!debugInfo.ContainsKey(p))
                        {
                            debugInfo[p] = debugDoc;
                        }
                        AppendDebugElement(debugInfo[p], p, item);
                    }
                }
                if (mainSlnx == null)
                {
                    MakeAndCleanNugetDebugFile(item.Value, slnx);
                }
            }

            foreach(var di in debugInfo)
            {
                string prettyContent = XDocument.Parse(di.Value.OuterXml).ToString();
                WriteAllText(Path.Join(di.Key.FullDir, CsProject.ImportDebugProjectName), prettyContent);
            }
        }

        static void AppendDebugElement(XmlDocument nugetDebugXml, CsProject referencingProject, KeyValuePair<NugetHelper.NugetPackage, SlnxHandler> item)
        {
            var propertyGroup = nugetDebugXml.CreateNode(XmlNodeType.Element, "PropertyGroup", null);
            nugetDebugXml.DocumentElement.AppendChild(propertyGroup);
            propertyGroup.InnerXml = string.Format("<{0}>1</{0}>", NugetHelper.NugetPackage.GetDebugEnvironmentVariableKey(item.Key.Id));

            var itemGroup = nugetDebugXml.CreateNode(XmlNodeType.Element, "ItemGroup", null);
            nugetDebugXml.DocumentElement.AppendChild(itemGroup);
            itemGroup.InnerXml = "";
            foreach (var referencedProject in item.Value.Projects.Where(x => !x.IsTestProject))
            {
                if (referencingProject.AssemblyReferences.Any(r => referencedProject.Name == Path.GetFileNameWithoutExtension(r.HintPath)))
                {
                    itemGroup.InnerXml = string.Format("{0}<ProjectReference Include=\"{1}\"/>", itemGroup.InnerXml, referencedProject.FullPath);
                }
            }
        }

        /// <summary>
        /// File.WriteAllText writes to the filesystem cache.
        /// The file is written to the disk by the OS at a later stage.
        /// This can be a problem, if the generated output is required by some other SW.
        /// This method makes use of the StreamWriter, which behaves differently.
        /// </summary>
        static void WriteAllText(string path, string text)
        {
            using (var f = new StreamWriter(path))
            {
                f.Write(text);
            }
        }

        static LogLevel ParseLogLevel(string v)
        {
            LogLevel ret = _logLevel;
            Enum.TryParse<LogLevel>(v, out ret);
            return ret;
        }
    }
}
