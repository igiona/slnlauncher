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
using Slnx.Interfaces;
using NuGet.Frameworks;
using System.Reflection.Metadata;

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
    public static class Program
    {
        const int _reqParameterN = 1;

        static bool _openSolution = false;
        static bool _createMsBuild = false;
        static bool _logEnabled = false;
        static string _pythonEnvVarsPath = null;
        static string _batchEnvVarsPath = null;
        static string _psEnvVarsPath = null;
        static Logger _logger = null;
        static LogLevel _logLevel = LogLevel.Info;
        static IFileWriter _fileWriter = null ;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        public static void Main(string[] argv)
        {
            Main(argv, new OutputFileWriter());
        }

        [STAThread]
        public static void Main(string[] argv, IFileWriter fileWriter)
        {
            Logger.DestroyInstance();

            _fileWriter = fileWriter ?? throw new ArgumentNullException(nameof(fileWriter));
            _logger = new Logger(fileWriter);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            _openSolution = true;
            _createMsBuild = false;
            var _printVersion = false;
            var _printHelp = false;
            var quiteExecution = false;
            var autoUpdateNuGetDependencies = true;
            var nugetForceMinVersion = true;
            var loadUserFile = true;
            bool offlineMode = false;
            string nuspecDir = null;
            var dump = false;
            string slnxFile = null;
            _pythonEnvVarsPath = null;

            OptionSet p = new OptionSet()
              .Add("h|help", "Prints the list of command and exits.", v => _printHelp = v != null)
              .Add("v|version", "Prints the tool version in the standard output and exits.", v => _printVersion = v != null)
              .Add("q|quite", "If set (-q/-q+) no popup will be shown in case of exceptions. [Default: not set]", v => quiteExecution = v != null)
              .Add("<>", "SlnX file path", v => slnxFile = v)
              .Add("o|openSln", "If set (-o/-o+) opens the generated Sln file. If not set (-o-), the generated Sln will not be opened. [Default: set]", v => _openSolution = v != null)
              .Add("u|user", "If set (-u/-u+) it loads an eventually present .user file. [Default: set]", v => loadUserFile = v != null)
              .Add("d|dump", "If set (-d/-d+) it dumps all project paths and environment variables in dump.txt located in the SlnX location . [Default: not set]", v => dump = v != null)
              .Add("py=|pythonModule=", "Path for the python module. If set the specified python module containing all defined environment variables is created. [Default: not set]", v => _pythonEnvVarsPath = v)
              .Add("b=|batchModule=", "Path to the batch module. If set the specified batch module containing all defined environment variables is created. [Default: not set]", v => _batchEnvVarsPath = v)
              .Add("ps=|powershellModule=", "Path to the power-shell module. If set the specified power-shell module containing all defined environment variables is created. [Default: not set]", v => _psEnvVarsPath = v)
              .Add("msb|msbuildModule", "If set (-msb/-msb+) a MSBuild module containing all defined environment variables is created in the SlnX location. [Default: not set]", v => _createMsBuild = v != null)
              .Add("log", "If set (-log/-log+), a log file location in the SlnX directory (or EXE if that path is invalid) will be created. [Default: false]", v => _logEnabled = v != null)
              .Add("lv=|logVerbosity=", string.Format("Set the log level of verbosity. Valid values {0}. [Default: {1}]", string.Join(",", Enum.GetNames<LogLevel>()), _logLevel), v => _logLevel = ParseLogLevel(v))
              .Add("ns=|nuspec=", "Output path for the NuGet package created based on the current solution. [Default: not set]", v => nuspecDir = v)
              .Add("nd|nugetDependencies", "If set (-nd/-nd+), the dependencies of the provided packages will be also automatically downloaded. [Default: true]", v => autoUpdateNuGetDependencies = v != null)
              .Add("offline", "If set (-offline/-offline+), The current SlnX packagesPath attribute will be used as source for all packages. [Default: false]", v => offlineMode = v != null)
              .Add("nf|nugetForceMinVersion", "If set (-nf/-nf+), the tool will check that dependencies fulfill the min-version provided (not allowing newer versions). [Default: true]", v => nugetForceMinVersion = v != null);
            
            try
            {
                p.Parse(argv);
                if (_printVersion)
                {
                    Console.WriteLine("SlnLauncher v{0}", typeof(SlnxHandler).Assembly.GetName().Version.ToString(3));
                    return;
                }
                if (_printHelp)
                {
                    Console.WriteLine("SlnLauncher v{0}", typeof(SlnxHandler).Assembly.GetName().Version.ToString(3));
                    p.WriteOptionDescriptions(Console.Out);
                    return;
                }

                if (slnxFile == null)
                    throw new ArgumentException(string.Format("Invalid parameters, no SlnX file specified.\n\n\t{0}", string.Join("\n\t", argv)));

                slnxFile = Path.GetFullPath(Environment.ExpandEnvironmentVariables(slnxFile));
                if (File.Exists(slnxFile))
                {
                    Environment.CurrentDirectory = Path.GetDirectoryName(slnxFile);
                }
                if (_logEnabled)
                {
                    _logger.SetLog(Path.Join(Environment.CurrentDirectory, Path.GetFileNameWithoutExtension(typeof(Program).Assembly.Location) + ".log"), _logLevel);
                    NuGetClientHelper.NuGetClientHelper.SetLogger(_logger);
                }
                _logger.Info("Application started with parameters: {0}", string.Join("\n", argv));

                var slnxUserFile = string.Format("{0}{1}", slnxFile, SlnxHandler.SlnxUserExtension);

                SlnXType slnxUser = null;

                if (loadUserFile && File.Exists(slnxUserFile))
                {
                    slnxUser = SlnxHandler.ReadSlnx(slnxUserFile);
                }

                var slnx = new SlnxHandler(slnxFile, slnxUser, _fileWriter, null);
                var originalPackageList = new List<NuGetClientHelper.NuGetPackage>(slnx.Packages);
                bool errorOccured = false;
                try
                {
                    DownloadPackages(slnx, quiteExecution, autoUpdateNuGetDependencies, offlineMode);

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
                    if (!string.IsNullOrEmpty(_psEnvVarsPath))
                    {
                        CreatePowerShellModule(slnx, Path.Combine(slnx.SlnxDirectory, _psEnvVarsPath));
                    }

                    var ignoreDependencyCheck = !autoUpdateNuGetDependencies;
                    _logger.Info($"Running dependency check with force min-version match set to {nugetForceMinVersion}, and ignore dependency is {ignoreDependencyCheck}");
                    NuGetClientHelper.NuGetClientHelper.CheckPackagesConsistency(slnx.Packages.ToList(), nugetForceMinVersion, ignoreDependencyCheck);

                    _logger.Info($"Checking debug packages consistency...");

                    foreach (var debugPackage in slnx.DebugSlnxItems.Keys)
                    {
                        foreach (var package in slnx.Packages)
                        {
                            if (package.Dependencies.Where(x => x.PackageDependency.Id == debugPackage.Identity.Id).Any())
                            {
                                _logger.Warn($"{package} depends on the package {debugPackage.Identity.Id} which is selected for debugging. This might cause runtime issues! Consider marking it for debugging as well.");
                            }
                        }
                    }

                    _logger.Info($"Check if all packages that are bind via .NET ImplementationAssemblies (lib directory) are specified in the SlnX file");
                    foreach (var package in slnx.Packages.Where((x) => x.PackageType == NuGetClientHelper.NuGetDotNetPackageType.DotNetImplementationAssembly))
                    {
                        if (!originalPackageList.Where((x) => x.Identity.Id == package.Identity.Id).Any())
                        {
                            _logger.Info($"The .NET implementation package {package} has been installed as dependency. Consider define it explicitly. Execute a dump to analyze dependency graph.");
                        }
                    }

                    _logger.Info($"Check if all packages that are bind via .NET CompileTimeAssemblies (ref directory) are specified in the SlnX file");
                    foreach (var package in slnx.Packages.Where((x) => x.PackageType == NuGetClientHelper.NuGetDotNetPackageType.DotNetCompileTimeAssembly))
                    {
                        if (originalPackageList.Where((x) => x.Identity.Id == package.Identity.Id).FirstOrDefault() == null)
                        {
                            _logger.Info($"The .NET compile time package {package} has been installed as dependency.");
                        }
                    }

                    MakeSln(slnx);
                    CleanGeneratedFiles(slnx);
                    slnx.CreateGenereatedFiles();

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
                        var nuspec = slnx.GetNuGetPackageInformation();
                        if (nuspec != null)
                        {
                            NuGetClientHelper.NuspecGenerator.Generate(nuspecDir, nuspec);
                        }
                        else
                        {
                            throw new Exception("Missing or invalid nuget content information in the provided SlnX file.");
                        }
                    }
                }
                catch
                {
                    errorOccured = true;
                    throw;
                }
                finally
                {
                    if (dump)
                    {
                        Dump(slnx, errorOccured);
                    }
                }

                if (_logger.LogLevelDetected(LogLevel.Warning))
                {
                    if (!quiteExecution)
                    {
                        var baseMsg = $"Warning(s) detected. This could cause runtime issues.\nIt's highly suggested to";
                        string message = "";
                        if (_logEnabled)
                        {
                            message = $"{baseMsg} review them in the log file: {_logger.LogPath}";
                        }
                        else
                        {
                            message = $"{baseMsg} re-run the application with log turned on.";
                        }
                        new InfoBox("Warning", message, System.Drawing.SystemIcons.Warning.ToBitmap()).ShowDialog();
                    }
                }
                _logger.Info($"Done!");

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
                {
                    string message;
                    if (_logEnabled)
                    {
                        message = $"Inspect the log for more information.\nLog file: {_logger.LogPath}\n\n{exText}";
                    }
                    else
                    {
                        message = $"Re-run the application with log turned on (--log) for more information\n\n{exText}";
                    }
                    new InfoBox("Error", message, System.Drawing.SystemIcons.Error.ToBitmap()).ShowDialog();
                }
                else
                    throw;
            }
        }

        static IEnumerable<NuGetClientHelper.NuGetPackage> PerformPackageDownloadProcess(IEnumerable<NuGetClientHelper.NuGetPackageInfo> packages, NuGetFramework requestedFramework, bool quite, bool autoUpdateDependencies, string formTitle)
        {
            ProgressDialog progress = null;
            System.Threading.Thread th = null;

            if (!quite)
            {
                th = new System.Threading.Thread(
                () =>
                {
                    progress = new ProgressDialog(formTitle, packages.Count());
                    progress.TopLevel = true;
                    progress.TopMost = true;
                    progress.ShowDialog();
                }
                );
            }
            if (th != null) th.IsBackground = true;
            th?.SetApartmentState(System.Threading.ApartmentState.STA);
            th?.Start();
            while (th != null && progress == null) System.Threading.Thread.Sleep(100);

            var ret = NuGetClientHelper.NuGetClientHelper.InstallPackages(packages.ToList(), autoUpdateDependencies, (message) =>
            {
                _logger.Debug("Package {0} successfully installed", message.ToString());
                progress?.IncrementProgress();
            },
            requestedFramework
            ).ToList();

            progress?.Close();
            th?.Join();

            return ret;
        }

        static void DownloadPackages(SlnxHandler slnx, bool quite, bool autoUpdateDependencies, bool offlineMode)
        {
            _logger.Info("Downloading required NuGet packages...");
            Uri nugetCacheUri = new Uri(slnx.PackagesPath);
            
            if (offlineMode)
            {
                _logger.Info($"Offline mode, using {slnx.PackagesPath} as package source.");

                foreach (var packageInfo in slnx.PackagesInfo)
                {
                    packageInfo.SetSource(nugetCacheUri);
                    if (packageInfo.DependencySources?.Count > 0)
                    {
                        packageInfo.DependencySources.Clear();
                        packageInfo.DependencySources.Add(nugetCacheUri);
                    }
                }
            }

            var frameworks = slnx.Projects.Select(p => NuGetFramework.ParseFolder(p.Framework));
            var requestedFramework = new FrameworkReducer().ReduceDownwards(frameworks).SingleOrDefault();
            if (requestedFramework == null)
            {
                throw new Exception($"It has not been possible to find a single common framework among the C# project specified in the SlnX file");
            }
            slnx.Packages = PerformPackageDownloadProcess(slnx.PackagesInfo, requestedFramework, quite, autoUpdateDependencies, "Loading packages...");

            if (slnx.DebugSlnxItems.Count != 0)
            {
                _logger.Info("Downloading NuGet packages marked as debug...");
                _logger.Debug("Need to download the package to properly gather the Libraries list. The dependencies are ignored to avoid package versions issues.");

                foreach ((var packageInfo, var debugSlnx) in slnx.DebugSlnxItems)
                {
                    if (offlineMode)
                    {
                        packageInfo.SetSource(nugetCacheUri);
                    }
                    var installed = PerformPackageDownloadProcess(new[] { packageInfo }, requestedFramework, quite, true, $"Loading debug package {packageInfo} without dependencies...");
                    debugSlnx.Packages = installed;
                    slnx.DebugPackages.Add(installed.First()); //Keep a reference to the debug package
                }
            }
        }

        static List<string> GetAllKeys(SlnxHandler slnx)
        {
            var keys = new List<string>();
            keys.AddRange(slnx.Projects.SelectMany(x => x.EnvironmentVariableKeys));
            keys.AddRange(slnx.EnvironementVariables.Keys);
            keys.AddRange(slnx.Packages.SelectMany(x => x.EnvironmentVariableKeys));
            keys.AddRange(slnx.Packages.Where(x => x.EnvironmentVariableAdditionalKey != null).Select(x => x.EnvironmentVariableAdditionalKey));
            return keys;
        }

        static void CreateMsBuildPropertiesTarget(SlnxHandler slnx)
        {
            string outDir = slnx.SlnxDirectory;
            _logger.Info("Creating MS Build targets in {0}", outDir);
            var content = new StringBuilder();
            var keys = GetAllKeys(slnx);

            content.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            content.AppendLine("<Project ToolsVersion=\"4.0\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">");
            content.AppendLine("    <PropertyGroup>");
            foreach (var key in keys)
            {
                var value = slnx.SafeExpandEnvironmentVariables(Environment.GetEnvironmentVariable(key));
                content.AppendLine($"        <{key}>{value}</{key}>");
            }
            content.AppendLine("\n        <MsBuildGeneratedProperties Condition=\" '$(MsBuildGeneratedProperties)' == '' \">");

            foreach (var key in keys)
            {
                var value = slnx.SafeExpandEnvironmentVariables(Environment.GetEnvironmentVariable(key));
                content.AppendLine($"            {key}={value};");
            }

            content.AppendLine("        </MsBuildGeneratedProperties>");
            content.AppendLine("    </PropertyGroup>");
            content.AppendLine("</Project>");

            _fileWriter.WriteAllText(Path.Combine(outDir, "MsBuildGeneratedProperties.targets"), content.ToString());
        }

        static string PrintPackageInfo(string title, IEnumerable<NuGetClientHelper.NuGetPackageIdentity> packages)
        {
            var content = new StringBuilder();
            content.AppendLine($"    | {title}");
            foreach (var refPackage in packages)
            {
                content.AppendLine($"        | {refPackage}");
            }
            return content.ToString();
        }

        static void Dump(SlnxHandler slnx, bool errorOccured)
        {
            const int firstColWidth = 40;
            const int sublineFirstColWidth = 20;

            string outDir = slnx.SlnxDirectory;
            _logger.Info("Dumping SlnX info in {0}", outDir);
            var content = new StringBuilder();

            if (errorOccured)
            {
                var errorDisclaimer = "Dumping is performed although there was an exception during the loading operations. The dump might be partial, invalid or inaccurate.";
                _logger.Info(errorDisclaimer);
                content.AppendLine($"/!\\ DISCLAIMER {errorDisclaimer}{Environment.NewLine}{Environment.NewLine}");
            }

            content.AppendLine($"CS Projects:{Environment.NewLine}");
            foreach (var p in slnx.Projects ?? Enumerable.Empty<CsProject>())
            {
                content.AppendLine($"{p.Name,-firstColWidth} => {p.FullPath}");

                if (p.PackageReferences.Count > 0)
                {
                    content.AppendLine(PrintPackageInfo("NuGet packages from SlnX", p.PackageReferences.Select(x => x.Identity)));
                }

                if (p.PackageReferencesFromAssemblies.Count > 0)
                {
                    content.AppendLine(PrintPackageInfo("NuGet packages from assemblies", p.PackageReferencesFromAssemblies.Select(x => x.Identity)));
                }

                if (p.PackageReferencesInFile.Count > 0)
                {
                    content.AppendLine(PrintPackageInfo("NuGet packages from .csProj (not yet used, informational only", p.PackageReferencesInFile));
                }
            }

            content.AppendLine($"{Environment.NewLine}CS Projects imported for debugging:{Environment.NewLine}");
            foreach (var p in slnx.ProjectsImportedFromDebugSlnx ?? Enumerable.Empty<CsProject>())
            {
                content.AppendLine($"{p.Name,-firstColWidth} => {p.FullPath}");
            }

            content.AppendLine($"------------------------------------{Environment.NewLine}");
            content.AppendLine($"NuGet packages:{Environment.NewLine}");
            foreach (var p in slnx.Packages ?? Enumerable.Empty<NuGetClientHelper.NuGetPackage>())
            {
                content.AppendLine($"{p,-firstColWidth} => {p.FullPath} [{p.PackageType}]");
                foreach (var d in p.Dependencies)
                {
                    content.AppendLine($"    | {d.PackageDependency.Id,-sublineFirstColWidth} => {d.PackageDependency.VersionRange} [{p.PackageType}]");
                }
            }

            content.AppendLine($"{Environment.NewLine}NuGet packages required by the projects imported for debugging:{Environment.NewLine}");
            foreach (var p in slnx.PackagesImportedFromDebugSlnx ?? Enumerable.Empty<NuGetClientHelper.NuGetPackage>())
            {
                content.AppendLine($"{p,-firstColWidth} => {p.FullPath} [{p.PackageType}]");
                foreach (var d in p.Dependencies)
                {
                    content.AppendLine($"    | {d.PackageDependency.Id,-sublineFirstColWidth} => {d.PackageDependency.VersionRange} [{p.PackageType}]");
                }
            }

            content.AppendLine($"------------------------------------{Environment.NewLine}");
            content.AppendLine($"Environment variables:{Environment.NewLine}");

            var keys = GetAllKeys(slnx);

            foreach (var key in keys)
            {
                var envVar = Environment.GetEnvironmentVariable(key);
                string value = null;
                if (envVar != null)
                {
                    value = slnx.SafeExpandEnvironmentVariables(envVar);
                }
                content.AppendLine($"{key} = {value}");
            }
            _fileWriter.WriteAllText(Path.Combine(outDir, "dump.txt"), content.ToString());
        }

        static void CreatePythonnModule(SlnxHandler slnx, string outDir)
        {
            _logger.Info("Creating Python module in {0}", outDir);
            var content = new StringBuilder();
            var keys = GetAllKeys(slnx);

            content.AppendLine($"import os{Environment.NewLine}");

            foreach (var key in keys)
            {
                var value = slnx.SafeExpandEnvironmentVariables(Environment.GetEnvironmentVariable(key));
                content.AppendLine($"os.environ['{key}'] = r'{value}'");
            }
            _fileWriter.WriteAllText(Path.Combine(outDir, "SetEnvVars.py"), content.ToString());
        }

        static void CreateBatchModule(SlnxHandler slnx, string outDir)
        {
            _logger.Info("Creating Batch module in {0}", outDir);
            var content = new StringBuilder();
            var keys = GetAllKeys(slnx);

            foreach (var key in keys)
            {
                var value = slnx.SafeExpandEnvironmentVariables(Environment.GetEnvironmentVariable(key));
                content.AppendLine($"set {key}={value}");
            }
            _fileWriter.WriteAllText(Path.Combine(outDir, "SetEnvVars.bat"), content.ToString());
        }

        static void CreatePowerShellModule(SlnxHandler slnx, string outDir)
        {
            _logger.Info("Creating PS module in {0}", outDir);
            var content = new StringBuilder();
            var keys = GetAllKeys(slnx);

            foreach (var key in keys)
            {
                var value = slnx.SafeExpandEnvironmentVariables(Environment.GetEnvironmentVariable(key));
                content.AppendLine($"[Environment]::SetEnvironmentVariable(\"{key}\", \"{value}\")");
            }
            _fileWriter.WriteAllText(Path.Combine(outDir, "SetEnvVars.ps1"), content.ToString());
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
                throw new Exception($"The configured sln file is not support. Only '{SlnxHandler.SlnExtension}' file are supported{Environment.NewLine}{Environment.NewLine}\tFile='{outFile}'");
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
            _fileWriter.WriteAllText(outFile, slnSb.ToString());
        }

        static List<SlnxHandler> GetAllSlnxHanlders(SlnxHandler mainSlnx)
        {
            var slnxToProcess = new List<SlnxHandler>();
            slnxToProcess.Add(mainSlnx);
            if (mainSlnx.DebugSlnxItems != null)
                slnxToProcess.AddRange(mainSlnx.DebugSlnxItems.Values);
            return slnxToProcess;
        }

        static void CleanGeneratedFiles(SlnxHandler mainSlnx)
        {
            foreach (var slnx in GetAllSlnxHanlders(mainSlnx))
            {
                _logger.Info($"Cleaning generated files for {slnx.SlnxName}");
                foreach (var pattern in new[] { CsProject.ImportDebugProjectName, CsProject.ImportPacakageReferencesProjectName })
                {
                    foreach (string f in Directory.EnumerateFiles(slnx.ProjectsSearchPath, pattern, new EnumerationOptions() { RecurseSubdirectories = true }))
                    {
                        _fileWriter.DeleteFile(f);
                    }
                }
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
