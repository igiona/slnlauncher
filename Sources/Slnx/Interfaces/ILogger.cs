using Slnx.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Slnx.Interfaces
{
    public enum LogLevel
    {
        None,
        Fatal,
        Error,
        Warning,
        Info,
        Debug,
        Trace
    }

    public interface ILogger
    {
        void Fatal(string msg, params object[] args);

        void Error(string msg, params object[] args);

        void Warn(string msg, params object[] args);

        void Info(string msg, params object[] args);

        void Debug(string msg, params object[] args);

        void Trace(string msg, params object[] args);
    }
}
