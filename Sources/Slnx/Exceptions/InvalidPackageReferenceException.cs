using System;

namespace Slnx.Exceptions
{
    public class InvalidPackageReferenceException : Exception
    {
        public InvalidPackageReferenceException(string msg) : base(msg) { }
    }
}
