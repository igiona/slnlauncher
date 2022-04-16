using Slnx.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Slnx
{
    public class Logger : NuGet.Common.ILogger, ILogger
    {
        private LogLevel _enabledLevel = LogLevel.None;
        private LogLevel _higestLevelDetected = LogLevel.None;
        private Dictionary<LogLevel, bool> _logLevelDetected = new Dictionary<LogLevel, bool>(Enum.GetValues(typeof(LogLevel)).Cast<int>().Select(x => new KeyValuePair<LogLevel, bool>((LogLevel)x, false)));
        private string _filePath;
        private object _lock = new object();
        private object _lockMaxLevel = new object();
        
        private IFileWriter _fileWriter = null;

        public Logger(IFileWriter fileWriter)
        {
            _fileWriter = fileWriter;
        }

        public LogLevel MaxLogLevelDetected
        {
            get { return _higestLevelDetected; }
        }

        public string LogPath
        {
            get { return _filePath; }
        }

        public bool LogLevelDetected(LogLevel l)
        {
            return _logLevelDetected[l];
        }

        public void SetLog(string filePath, LogLevel level)
        {
            lock (_lock)
            {
                if (_fileWriter.FileExists(filePath))
                {
                    _fileWriter.DeleteFile(filePath);
                }
                _filePath = filePath;
                _enabledLevel = level;
            }
        }

        public void Fatal(string msg, params object[] args)
        {
            LogTryAppend(LogLevel.Fatal, "SlnLauncher", msg, args);
        }

        public void Error(string msg, params object[] args)
        {
            LogTryAppend(LogLevel.Error, "SlnLauncher", msg, args);
        }

        public void Warn(string msg, params object[] args)
        {
            LogTryAppend(LogLevel.Warning, "SlnLauncher", msg, args);
        }

        public void Info(string msg, params object[] args)
        {
            LogTryAppend(LogLevel.Info, "SlnLauncher", msg, args);
        }

        public void Debug(string msg, params object[] args)
        {
            LogTryAppend(LogLevel.Debug, "SlnLauncher", msg, args);
        }

        public void Trace(string msg, params object[] args)
        {
            LogTryAppend(LogLevel.Trace, "SlnLauncher", msg, args);
        }

        void LogTryAppend(LogLevel logLevel, string app, string msg, params object[] args)
        {
            lock (_lockMaxLevel)
            {
                _higestLevelDetected = (LogLevel)Math.Max((int)_higestLevelDetected, (int)logLevel);
                _logLevelDetected[logLevel] = true;
            }

            if (!string.IsNullOrEmpty(_filePath) && _enabledLevel >= logLevel)
            {
                var now = DateTime.Now;
                var logContent = string.Format("\n{0:D2}:{1:D2}:{2:D2} | {3}\t| {4}\t| {5}", now.Hour, now.Minute, now.Second, logLevel, app, string.Format(msg, args));

                lock (_lock)
                {
                    _fileWriter.AppendAllText(_filePath, logContent);
                }
            }
        }

        void NuGet.Common.ILogger.LogDebug(string data)
        {
            LogTryAppend(LogLevel.Debug, "NuGet", data);
        }

        void NuGet.Common.ILogger.LogVerbose(string data)
        {
            LogTryAppend(LogLevel.Trace, "NuGet", data);
        }

        void NuGet.Common.ILogger.LogInformation(string data)
        {
            //Map NuGet info log to debug
            LogTryAppend(LogLevel.Debug, "NuGet", data);
        }

        void NuGet.Common.ILogger.LogMinimal(string data)
        {
            LogTryAppend(LogLevel.Info, "NuGet", data);
        }

        void NuGet.Common.ILogger.LogWarning(string data)
        {
            LogTryAppend(LogLevel.Warning, "NuGet", data);
        }

        void NuGet.Common.ILogger.LogError(string data)
        {
            LogTryAppend(LogLevel.Error, "NuGet", data);
        }

        void NuGet.Common.ILogger.LogInformationSummary(string data)
        {
            //Map NuGet info log to debug
            LogTryAppend(LogLevel.Debug, "NuGet", data);
        }

        void NuGet.Common.ILogger.Log(NuGet.Common.LogLevel level, string data)
        {
            switch (level)
            {
                case NuGet.Common.LogLevel.Debug:
                    ((NuGet.Common.ILogger)this).LogDebug(data);
                    break;
                case NuGet.Common.LogLevel.Error:
                    ((NuGet.Common.ILogger)this).LogError(data);
                    break;
                case NuGet.Common.LogLevel.Information:
                    ((NuGet.Common.ILogger)this).LogInformation(data);
                    break;
                case NuGet.Common.LogLevel.Minimal:
                    ((NuGet.Common.ILogger)this).LogMinimal(data);
                    break;
                case NuGet.Common.LogLevel.Warning:
                    ((NuGet.Common.ILogger)this).LogWarning(data);
                    break;
                case NuGet.Common.LogLevel.Verbose:
                    ((NuGet.Common.ILogger)this).LogVerbose(data);
                    break;
                default:
                    ((NuGet.Common.ILogger)this).LogVerbose(data);
                    break;
            }
        }

        Task NuGet.Common.ILogger.LogAsync(NuGet.Common.LogLevel level, string data)
        {
            return Task.CompletedTask;
        }

        void NuGet.Common.ILogger.Log(NuGet.Common.ILogMessage message)
        {
            ((NuGet.Common.ILogger)this).Log(message.Level, $"{message.Time} - {message.Code} - {message.Message}");
        }

        Task NuGet.Common.ILogger.LogAsync(NuGet.Common.ILogMessage message)
        {
            return Task.CompletedTask;
        }
    }
}
