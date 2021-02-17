using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Slnx
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

    public class Logger
    {
        private LogLevel _enabledLevel = LogLevel.None;
        private string _filePath;
        private object _lock = new object();

        private static Logger _instance;

        public static Logger Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new Logger();
                }
                return _instance;
            }
        }

        protected Logger()
        {
        }

        public void SetLog(string filePath, LogLevel level)
        {
            lock (_lock)
            {
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
                _filePath = filePath;
                _enabledLevel = level;
            }
        }

        public void Fatal(string msg, params object[] args)
        {
            LogTryAppend(LogLevel.Fatal, msg, args);
        }

        public void Error(string msg, params object[] args)
        {
            LogTryAppend(LogLevel.Error, msg, args);
        }

        public void Warn(string msg, params object[] args)
        {
            LogTryAppend(LogLevel.Warning, msg, args);
        }

        public void Info(string msg, params object[] args)
        {
            LogTryAppend(LogLevel.Info, msg, args);
        }

        public void Debug(string msg, params object[] args)
        {
            LogTryAppend(LogLevel.Debug, msg, args);
        }

        public void Trace(string msg, params object[] args)
        {
            LogTryAppend(LogLevel.Trace, msg, args);
        }

        void LogTryAppend(LogLevel logLevel, string msg, params object[] args)
        {
            if (!string.IsNullOrEmpty(_filePath) && _enabledLevel >= logLevel)
            {
                var now = DateTime.Now;
                var logContent = string.Format("\n{0}:{1}:{2} | {3} | {4}", now.Hour, now.Minute, now.Second, logLevel, string.Format(msg, args));

                lock (_lock)
                {
                    System.IO.File.AppendAllText(_filePath, logContent);
                }
            }
        }
    }
}
