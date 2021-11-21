using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace SlnLauncher.Test
{
    public static class TestHelper
    {
        const string StimulFolder = "Stimuli";
        const string ExpectedFolder = "Expected";
        const string ResultsFolder = "Results";

        internal static string[] GetArguments(string slnxFile, params string[] additionalArgs)
        {
            var args = new List<string>();

            args.Add("-o-");
            args.Add("-q");

            var moreArgs = additionalArgs?.Where(x => x != null);
            if (moreArgs?.Count() > 0)
            {
                args.AddRange(moreArgs);
            }
            
            args.Add(GetStimulPathFor(slnxFile));
            return args.ToArray();
        }

        /// <summary>
        /// Compare files line by line, ignoring the line ending.
        /// </summary>
        /// <param name="resultFile">Full path to the result file</param>
        /// <param name="expectedFile">Relative path to the expected file.</param>
        /// <returns>Returns true if the file match, false otherwise</returns>
        public static bool Compare(string resultFile, string expectedFile, params string[] skip)
        {
            //try
            {
                var resultLines = File.ReadAllLines(resultFile);
                var expectedLines = File.ReadAllLines(GetExpectedPathFor(expectedFile));
                if (resultLines.Length == expectedLines.Length)
                {
                    var lines = resultLines.Length;
                    int i;
                    for (i = 0; i < lines; i++)
                    {
                        if (resultLines[i] != expectedLines[i])
                        {
                            if (!skip.Any(x => resultLines[i].Contains(x)))
                            {
                                Console.WriteLine($"Error at line {i}");
                                break;
                            }
                        }
                    }
                    if (lines == i)
                    {
                        return true;
                    }
                }
            }
            //catch
            //{
            //}
            return false;
        }

        internal static string GetStimulPathFor(string file)
        {
            return GetFolderFor(file, StimulFolder);
        }

        internal static string GeResultPathFor(string file)
        {
            return GetFolderFor(file, ResultsFolder);
        }

        internal static string GeResultsPath()
        {
            return GetFolder(ResultsFolder);
        }

        internal static string GetExpectedPathFor(string file)
        {
            return GetFolderFor(file, ExpectedFolder);
        }

        private static string GetFolder(string testFolder)
        {
            var key = NuGetClientHelper.NuGetPackage.EscapeStringAsEnvironmentVariableAsKey(typeof(TestHelper).Assembly.GetName().Name);
            return Path.Combine($"{Environment.ExpandEnvironmentVariables($"%{key}%")}", "..", testFolder);
        }

        private static string GetFolderFor(string path, string testFolder)
        {
            return Path.Combine(GetFolder(testFolder), path);
        }
    }
}
