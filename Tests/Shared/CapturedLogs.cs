using System;
using System.Collections.Generic;
using UnityEngine;

namespace CG.CombatFramework.Tests
{
    public sealed class CapturedLogs : ILogHandler, IDisposable
    {
        private readonly ILogHandler previous;

        public readonly List<Exception> Exceptions = new();
        public readonly List<string> Warnings = new();
        public readonly List<string> Errors = new();

        public CapturedLogs()
        {
            previous = Debug.unityLogger.logHandler;
            Debug.unityLogger.logHandler = this;
        }

        public void LogFormat(LogType logType, UnityEngine.Object context, string format, params object[] args)
        {
            string message = args == null || args.Length == 0 ? format : string.Format(format, args);
            if (logType == LogType.Warning) Warnings.Add(message);
            else if (logType == LogType.Error || logType == LogType.Assert) Errors.Add(message);
        }

        public void LogException(Exception exception, UnityEngine.Object context) => Exceptions.Add(exception);

        public void Dispose() => Debug.unityLogger.logHandler = previous;
    }
}
