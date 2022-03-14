using System;

namespace SlnLauncher.Exceptions
{
    public class MultiFrameworkAppException : Exception
    {
        public MultiFrameworkAppException(string msg) : base(msg) { }
    }
}
