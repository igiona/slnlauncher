﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Serialization;
using System.IO;
using Microsoft.Win32;
using System.Text;
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
        static string _pythonEnvVarsPath = null;
        static Logger _logger = null;
        
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
            var quiteExecution = false;
            var autoNuget = true;
            string nuspecDir = null;            
            var dump = false;
            string slnxFile = null;
            _pythonEnvVarsPath = null;

            OptionSet p = new OptionSet()
              .Add("q|quite", "If set (-q/-q+) no popups will be shown in case of exceptions. [Default: not set]", v => quiteExecution = v != null)
              .Add("<>", "SlnX file path", v => slnxFile = v)
              .Add("o|openSln", "If set (-o/-o+) opens the generated Sln file. If not set (-o-), the generated Sln will not be opened. [Default: set]", v => _openSolution = v != null)
              .Add("d|dump", "If set (-d/-d+) it dumps all project paths and environment variables in dump.txt located in the SlnX location . [Default: not set]", v => dump = v != null)
              .Add("py=|pythonModule=", "Path for the python module. If set the specified python module containing all defined environment variables is created. [Default: not set]", v => _pythonEnvVarsPath = v)
              .Add("msb|msbuildModule", "If set (-msb/-msb+) a MSBuild module containing all defined environment variables is created in the SlnX location. [Default: not set]", v => _createMsBuild = v != null)
              .Add("log", "If set (-log/-log+), a log file location in the EXE directory will be created. [Default: false]", v => { if (v != null) { _logger.SetLog(Path.Combine(typeof(Program).Assembly.Location + ".log"), LogLevel.Debug); } })
              .Add("ns=|nuspec=", "If set (-ns/-ns+), creates the NuGet package for the current solution in the specified directory. [Default: not set]", v => nuspecDir = v)
              .Add("ng|nuget", "If set (-ng/-ng+), the defined NuGet packages will be automatically downloaded. [Default: true]", v => autoNuget = v != null);

            try
            {
                p.Parse(argv);
                _logger.Info("Application started with parameters: {0}", string.Join("\n", argv));

                if (slnxFile == null)
                    throw new ArgumentException(string.Format("Invalid parameters, no SlnX file specified.\n\n\t{0}", string.Join("\n\t", argv)));
                
                var slnxUserFile = string.Format("{0}{1}", slnxFile, SlnxHandler.SlnxUserExtension);

                SlnXType slnxUser = null;

                if (File.Exists(slnxUserFile))
                {
                    slnxUser = SlnxHandler.ReadSlnx(slnxUserFile);
                }

                var slnx = new SlnxHandler(slnxFile, slnxUser);

                if (_createMsBuild)
                {
                    CreateMsBuildPropertiesTarget(slnx);
                }

                if (!string.IsNullOrEmpty(_pythonEnvVarsPath))
                {
                    CreatePythonnModule(slnx, Path.Combine(slnx.SlnxDirectory, _pythonEnvVarsPath));
                }

                if (dump)
                {
                    Dump(slnx);
                }

                if (autoNuget)
                {
                    DownloadPackages(slnx, quiteExecution);
                }
                
                if (!string.IsNullOrEmpty(nuspecDir))
                {
                    nuspecDir = Path.GetFullPath(Environment.ExpandEnvironmentVariables(nuspecDir));
                    NugetHelper.NuspecGenerator.Generate(nuspecDir, slnx.Nuget);
                }
                
                MakeSln(slnx);

                if (_openSolution)
                {
                    OpenSln(slnx.SlnPath);
                }
            }
            catch (Exception ex)
            {
                string exText = string.Join("\n", new AggregateException(ex).InnerExceptions.Select(x => x.Message));
                _logger.Error(exText);
                _logger.Error(ex.StackTrace);

                if (!quiteExecution)
                    MessageBox.Show(string.Format("Run the command with the option --log for more information\n\n{0}", exText), "Error");
                else
                    throw;
            }
        }

        static void DownloadPackages(SlnxHandler slnx, bool quite)
        {
            _logger.Info("Downloading NuGet packages...");
            ProgressDialog progress = null;

            System.Threading.Thread th = null;

            if (!quite)
            {
                th = new System.Threading.Thread(
                () =>
                    {
                        progress = new ProgressDialog("Loading packages...", slnx.Packages.Count());
                        progress.ShowDialog();
                    }
                );
            }
            if (th != null) th.IsBackground = true;
            th?.SetApartmentState(System.Threading.ApartmentState.STA);
            th?.Start();
            while (th != null && progress == null) System.Threading.Thread.Sleep(100);
            NugetHelper.NugetHelper.InstallPackages(slnx.Packages, false, (message) => 
                {
                    _logger.Info("Package {0} successefully installed", message.ToString());
                    progress?.IncrementProgress();
                });
            _logger.Info("Done!");
            progress?.Close();
            th?.Join();
        }

        static List<string> GetAllKeys(SlnxHandler slnx)
        {
            List<SlnItem> projects = slnx.Projects.Where(x => x.Item != null).Select(x => x.Item).ToList();
            var keys = projects.Where((x) => !x.IsContainer).Select((x) => ((CsProject)x).EnvironmentVariableKey).ToList();
            keys.AddRange(slnx.EnvironementVariables.Keys);
            keys.AddRange(slnx.Packages.Select(x => x.EnvironmentVariableKey));
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
                    var value = Environment.ExpandEnvironmentVariables(Environment.GetEnvironmentVariable(key));
                    f.WriteLine("        <{0}>{1}</{0}>", key, value);
                }
                f.WriteLine("\n        <MsBuildGeneratedProperties Condition=\" '$(MsBuildGeneratedProperties)' == '' \">");

                foreach (var key in keys)
                {
                    var value = Environment.ExpandEnvironmentVariables(Environment.GetEnvironmentVariable(key));
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

                foreach (var p in slnx.Projects.Where(x => x.Item != null && !x.Item.IsContainer))
                {
                    f.WriteLine("{0,-40} => {1}", p.Item.Name, p.FullPath);
                }

                f.WriteLine("------------------------------------\n");
                f.WriteLine("Environment variables:\n");

                var keys = GetAllKeys(slnx);

                foreach (var key in keys)
                {
                    var value = Environment.ExpandEnvironmentVariables(Environment.GetEnvironmentVariable(key));
                    f.WriteLine("{0} = {1}", key, value);
                }
            }
            _logger.Info("Done!");
        }

        static void CreatePythonnModule(SlnxHandler slnx, string outDir)
        {
            _logger.Info("Creating Python module in {0}", outDir);
            List<SlnItem> projects = slnx.Projects.Where(x => x.Item != null).Select(x => x.Item).ToList();

            using (var f = new StreamWriter(Path.Combine(outDir, "SetEnvVars.py")))
            {
                var keys = GetAllKeys(slnx);

                f.WriteLine("import os\n");

                foreach (var key in keys)
                {
                    var value = Environment.ExpandEnvironmentVariables(Environment.GetEnvironmentVariable(key));
                    f.WriteLine("os.environ['{0}'] = r'{1}'", key, value);
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
            List<SlnItem> projects = slnx.Projects.Where(x => x.Item != null).Select(x => x.Item).ToList();
            string outFile = slnx.SlnPath;
            _logger.Info("Creating solution file: {0}", outFile);

            if (Path.GetExtension(outFile).ToLower() !=  SlnxHandler.SlnExtension)
                throw new Exception(string.Format("The configured sln file is not support. Only '{0}' file are supported\n\n\tFile='{1}'", SlnxHandler.SlnExtension, outFile));

            StringBuilder slnSb = new StringBuilder();
            StringBuilder projectListSb = new StringBuilder();
            StringBuilder buildConfigSb = new StringBuilder();
            StringBuilder containerConfigSb = new StringBuilder();

            foreach (var p in projects)
            {
                var path = p.FullPath;
                if (p.IsContainer)
                    path = p.Name;

                projectListSb.Append(p.ToString());

                var buildCfg = p.GetBuildConfiguration();
                if (buildCfg != null)
                    buildConfigSb.Append(buildCfg);

                var container = projects.Where((x) => x.IsContainer && x.FullPath == p.Container).ToList();
                if (container.Count > 0)
                    containerConfigSb.AppendFormat("\n		{{{0}}} = {{{1}}}", p.ProjectGuid, container[0].ProjectGuid);
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
            File.WriteAllText(outFile, slnSb.ToString());
            _logger.Info("Done!");
        }
    }
}
