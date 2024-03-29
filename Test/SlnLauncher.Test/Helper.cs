﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;

namespace SlnLauncher.Test
{
    public static class TestHelper
    {
        const string LineRegexTag = "$#$";
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
        /// It also ignore the lines that contains one or more element of the skip parameter.
        /// </summary>
        /// <param name="resultFile">Full path to the result file</param>
        /// <param name="expectedFile">Relative path to the expected file.</param>
        /// <returns>Returns true if the file match, false otherwise</returns>
        public static bool Compare(string resultFile, string expectedFile, params string[] skip)
        {
            return Compare(resultFile, expectedFile, null, skip);
        }

        /// <summary>
        /// Compare files line by line, ignoring the line ending.
        /// It also ignore the lines that contains one or more element of the skip parameter.
        /// Additionally, on each line it ignores also all charaters which are written before the "|" (timestamp)
        /// </summary>
        /// <param name="resultFile">Full path to the result file</param>
        /// <param name="expectedFile">Relative path to the expected file.</param>
        /// <returns>Returns true if the file match, false otherwise</returns>
        public static bool CompareLogFile(string resultFile, string expectedFile, params string[] skip)
        {
            return Compare(resultFile, expectedFile, "|", skip);
        }

        private static bool Compare(string resultFile, string expectedFile, string skipUpTo, params string[] skip)
        {
            if (!File.Exists(resultFile))
            {
                NUnit.Framework.TestContext.WriteLine($"Missing result file {resultFile}");
            }
            if (!File.Exists(expectedFile))
            {
                NUnit.Framework.TestContext.WriteLine($"Missing expected file {expectedFile}");
            }

            var resultLines = File.ReadAllLines(resultFile);
            var expectedLines = File.ReadAllLines(GetExpectedPathFor(expectedFile));
            if (resultLines.Length == expectedLines.Length)
            {
                var lines = resultLines.Length;
                int i;
                for (i = 0; i < lines; i++)
                {
                    var resLine = resultLines[i];
                    var expLine = expectedLines[i];
                    if (expLine.StartsWith(LineRegexTag))
                    {
                        if (!Regex.IsMatch(resLine, expLine.Substring(LineRegexTag.Length)))
                        {
                            break;
                        }
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(skipUpTo))
                        {
                            var skipChars = resLine.IndexOf(skipUpTo);
                            if (skipChars != -1) { resLine = resLine.Substring(skipChars); }
                            skipChars = expLine.IndexOf(skipUpTo);
                            if (skipChars != -1) { expLine = expLine.Substring(skipChars); }
                        }

                        if (resLine != expLine)
                        {
                            if (!skip.Any(x => resLine.Contains(x)))
                            {
                                NUnit.Framework.TestContext.WriteLine($"Error at line {i + 1} in {resultFile} vs {expectedFile}");
                                break;
                            }
                        }
                    }
                }
                if (lines == i)
                {
                    return true;
                }
            }
            return false;
        }

        public static void AreFilesEqual(string resultFile, string expectedFile, params string[] skip)
        {
            var res = Compare(resultFile, expectedFile, skip);
            if (!res)
            {
                NUnit.Framework.TestContext.WriteLine($"Expected file {expectedFile} does not match with {resultFile}");
            }
            NUnit.Framework.Assert.IsTrue(res);
        }

        internal static string GetStimulPathFor(string file)
        {
            return GetFolderFor(file, StimulFolder);
        }

        internal static string GetResultPathFor(string file)
        {
            return GetFolderFor(file, ResultsFolder);
        }

        internal static string GetResultsPath()
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
