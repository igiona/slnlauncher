using System;

namespace Slnx.Exceptions
{
    public class DuplicatePackageReferenceException : Exception
    {
        public DuplicatePackageReferenceException(string msg) : base(msg) { }
    }
}
