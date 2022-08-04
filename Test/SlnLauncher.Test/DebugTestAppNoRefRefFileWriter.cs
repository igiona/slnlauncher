﻿using System.IO;
using Slnx.Interfaces;

namespace SlnLauncher.Test
{
    internal class DebugTestAppNoRefFileWriter : AppBaseFileWriter
    {
        internal const string FolderName = "DebugTestAppNoRef";
        internal string SlnxName = Path.Combine(FolderName, "DebugTestAppNoRef.slnx");

        public DebugTestAppNoRefFileWriter() : base (FolderName)
        {
        }

        protected override string GetPath(string path)
        {
            var filePartialPath = Path.Combine(_folderName, Path.GetFileName(Path.GetDirectoryName(path)), Path.GetFileName(path));
            return TestHelper.GetResultPathFor(filePartialPath);
        }
    }
}
