using System;

using EFT;

namespace LootingBots.Patch.Util
{
    [Flags]
    public enum LogLevel
    {
        /// <summary>
        ///     No level selected.
        /// </summary>
        None = 0,

        /// <summary>
        ///     An error has occured, but can be recovered from.
        /// </summary>
        Error = 2,

        /// <summary>
        ///     A warning has been produced, but does not necessarily mean that something wrong has happened.
        /// </summary>
        Warning = 4,

        /// <summary>
        ///     A message of low importance.
        /// </summary>
        Info = 16,

        /// <summary>
        ///     A message that would likely only interest a developer.
        /// </summary>
        Debug = 32,

        /// <summary>
        ///     All log levels.
        /// </summary>
        All = Error | Warning | Info | Debug
    }

    public class BotLog
    {
        private readonly Log _log;
        private readonly BotOwner _botOwner;
        private readonly string _botString;

        public BotLog(Log log, BotOwner botOwner)
        {
            _log = log;
            _botOwner = botOwner;
            _botString = $"([{_botOwner.Profile.Info.Settings.Role}] {_botOwner.name})";
        }

        public void LogDebug(object msg)
        {
            _log.LogDebug(FormatMessage(msg));
        }

        public void LogInfo(object msg)
        {
            _log.LogInfo(FormatMessage(msg));
        }

        public void LogWarning(object msg)
        {
            _log.LogWarning(FormatMessage(msg));
        }

        public void LogError(object msg)
        {
            _log.LogError(FormatMessage(msg));
        }

        private string FormatMessage(object data)
        {
            return $"{_botString} {data}";
        }
    }

    public class Log
    {
        public BepInEx.Logging.ManualLogSource Logger;
        public BepInEx.Configuration.ConfigEntry<LogLevel> LogLevels;

        public Log(
            BepInEx.Logging.ManualLogSource logger,
            BepInEx.Configuration.ConfigEntry<LogLevel> logLevels
        )
        {
            Logger = logger;
            LogLevels = logLevels;
        }

        public bool IsDebug()
        {
            return LogLevels.Value.HasDebug();
        }

        public void LogDebug(object data)
        {
            if (LogLevels.Value.HasDebug())
            {
                Logger.LogDebug(data);
            }
        }

        public void LogInfo(object data)
        {
            if (LogLevels.Value.HasInfo())
            {
                Logger.LogInfo(data);
            }
        }

        public void LogWarning(object data)
        {
            if (LogLevels.Value.HasWarning())
            {
                Logger.LogWarning(data);
            }
        }

        public void LogError(object data)
        {
            if (LogLevels.Value.HasError())
            {
                Logger.LogError(data);
            }
        }
    }

    public static class LogUtils
    {
        public static bool HasError(this LogLevel logLevel)
        {
            return logLevel.HasFlag(LogLevel.Error);
        }

        public static bool HasWarning(this LogLevel logLevel)
        {
            return logLevel.HasFlag(LogLevel.Warning);
        }

        public static bool HasInfo(this LogLevel logLevel)
        {
            return logLevel.HasFlag(LogLevel.Info);
        }

        public static bool HasDebug(this LogLevel logLevel)
        {
            return logLevel.HasFlag(LogLevel.Debug);
        }
    }
}
