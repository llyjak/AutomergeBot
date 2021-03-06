﻿using System;
using System.IO;
using Microsoft.Extensions.Configuration;
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

            var seqUrl = GetSeqUrlFromAppConfiguration();

            Logger serilogLogger;
            if (!string.IsNullOrWhiteSpace(seqUrl))
            {
                serilogLogger = CreateSerilogLoggerForSeq(seqUrl);
            }
            else
            {
                serilogLogger = CreateSerilogLogger(logFilesBasePath);
            }

            lock (LockObject)
            {
                if (_loggingInitialized) return;

                _loggingInitialized = true;
                Log.Logger = serilogLogger;
            }
        }

        private static string GetSeqUrlFromAppConfiguration()
        {
            var configurationRoot = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", true, true)
                .Build();

            var seqUrl = configurationRoot["SeqUrl"];
            return seqUrl;
        }

        public static Logger CreateSerilogLogger(string logFilesBasePath)
        {
            logFilesBasePath = logFilesBasePath ?? string.Empty;
            var conf = CreateBaseLoggerConfiguration();

            EnableWriteToConsole(conf);
            EnableWriteToFile(logFilesBasePath, conf);

            conf.WriteTo.Logger(ConfigureEasyMonitoringFileLogger(logFilesBasePath));
            return conf.CreateLogger();
        }

        public static Logger CreateSerilogLoggerForSeq(string seqUrl)
        {
            var conf = CreateBaseLoggerConfiguration();

            EnableWriteToConsole(conf);
            conf.WriteTo.Seq(seqUrl, compact: true);

            return conf.CreateLogger();
        }

        private static LoggerConfiguration CreateBaseLoggerConfiguration()
        {
            var conf = new LoggerConfiguration();

            conf.Enrich.FromLogContext();

            conf.MinimumLevel.ControlledBy(LoggingLevelSwitch)
                .Filter.ByExcluding(MicrosoftNotImportantLogsSelector);
            return conf;
        }

        private static void EnableWriteToFile(string logFilesBasePath, LoggerConfiguration conf)
        {
            conf.WriteTo.File(
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties}{NewLine}{Exception}",
                path: Path.Combine(logFilesBasePath, $"AutomergeBot.log"),
                fileSizeLimitBytes: 10 * 1024 * 1024,
                rollOnFileSizeLimit: true,
                retainedFileCountLimit: 10);
        }

        private static void EnableWriteToConsole(LoggerConfiguration conf)
        {
            conf.WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}\t{Properties:j}{NewLine}{Exception}",
                restrictedToMinimumLevel: LogEventLevel.Information);
        }

        private static Action<LoggerConfiguration> ConfigureEasyMonitoringFileLogger(string logFilesBasePath)
        {
            return lc => lc
                .Enrich.FromLogContext()
                .WriteTo.File(
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{TraceIdentifier}] {Message:lj} {payloadModel}{NewLine}{Exception}",
                    path: Path.Combine(logFilesBasePath, $"AutomergeBot_easy_monitoring.log"),
                    fileSizeLimitBytes: 10 * 1024 * 1024,
                    rollOnFileSizeLimit: true,
                    retainedFileCountLimit: 10)
                .MinimumLevel.Information()
                .Filter.ByIncludingOnly(@event => LevelIsMinimumError(@event) || AutomergeBotLogsFilter(@event));
        }

        private static bool LevelIsMinimumError(LogEvent @event)
        {
            return @event.Level >= LogEventLevel.Error;
        }

        private static bool MicrosoftNotImportantLogsSelector(LogEvent le)
        {
            return le.Level < LogEventLevel.Warning && ((ScalarValue)le.Properties[Constants.SourceContextPropertyName]).Value.ToString().StartsWith("Microsoft", StringComparison.Ordinal);
        }

        private static bool AutomergeBotLogsFilter(LogEvent le)
        {
            return ((ScalarValue)le.Properties[Constants.SourceContextPropertyName]).Value.ToString().StartsWith("PerfectGym.AutomergeBot.AutomergeBot", StringComparison.Ordinal);
        }

        public static void SetMinimulLoggingLevel(LogEventLevel level)
        {
            LoggingLevelSwitch.MinimumLevel = level;
        }
    }
}