using System;
using System.IO;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace PerfectGym.AutomergeBot
{
    public static class Logging
    {
        private static bool _loggingInitialized;
        private static readonly object LockObject = new object();
        private static readonly LoggingLevelSwitch LoggingLevelSwitch = new LoggingLevelSwitch(LogEventLevel.Verbose);

        public static void EnsureLoggingInitialized(string logFilesBasePath)
        {
            if (_loggingInitialized) return;

            var serilogLogger = CreateSerilogLogger(logFilesBasePath);

            lock (LockObject)
            {
                if (_loggingInitialized) return;

                _loggingInitialized = true;
                Log.Logger = serilogLogger;
            }
        }

        public static Logger CreateSerilogLogger(string logFilesBasePath)
        {
            return new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Console(
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}\t{Properties:j}{NewLine}{Exception}",
                    restrictedToMinimumLevel: LogEventLevel.Information)
                .WriteTo.File(
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties}{NewLine}{Exception}",
                    path: Path.Combine(logFilesBasePath, $"{nameof(AutomergeBot.AutomergeBot)}.log"),
                    fileSizeLimitBytes: 10 * 1024 * 1024,
                    rollOnFileSizeLimit: true,
                    retainedFileCountLimit: 10)
                .MinimumLevel.ControlledBy(LoggingLevelSwitch)
                .Filter.ByExcluding(MicrosoftNotImportantLogsSelector)
                .CreateLogger();
        }

        private static bool MicrosoftNotImportantLogsSelector(LogEvent le)
        {
            return le.Level < LogEventLevel.Warning && ((ScalarValue)le.Properties[Constants.SourceContextPropertyName]).Value.ToString().StartsWith("Microsoft");
        }

        public static void SetMinimulLoggingLevel(LogEventLevel level)
        {
            LoggingLevelSwitch.MinimumLevel = level;
        }
    }
}