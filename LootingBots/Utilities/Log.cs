using EFT;

namespace LootingBots.Utilities
{
    [Flags]
    public enum LogLevel
    {
        /// <summary>
        ///     No level selected.
        /// </summary>
        None = 0,

        /// <summary>
        ///     An error has occurred, but can be recovered from.
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
        All = Error | Warning | Info | Debug,
    }

    public class BotLog
    {
        private readonly Log _log;
        private readonly BotOwner _botOwner;
        private readonly string _botString;

        private string _currentBotFilter
        {
            get { return LootingBots.FilterLogsOnBot.Value.ToString(); }
        }

        private bool _isLogShown
        {
            get { return _currentBotFilter == "0" || _botOwner.name.Equals("Bot" + _currentBotFilter); }
        }

        public bool DebugEnabled
        {
            get { return _log.DebugEnabled; }
        }
        public bool WarningEnabled
        {
            get { return _log.WarningEnabled; }
        }
        public bool InfoEnabled
        {
            get { return _log.InfoEnabled; }
        }
        public bool ErrorEnabled
        {
            get { return _log.ErrorEnabled; }
        }

        public BotLog(Log log, BotOwner botOwner)
        {
            _log = log;
            _botOwner = botOwner;
            _botString = $"([{_botOwner.Profile.Info.Settings.Role}] {_botOwner.name})";
        }

        public void LogDebug(object msg)
        {
            if (_isLogShown)
                _log.LogDebug(FormatMessage(msg));
        }

        public void LogInfo(object msg)
        {
            if (_isLogShown)
                _log.LogInfo(FormatMessage(msg));
        }

        public void LogWarning(object msg)
        {
            if (_isLogShown)
                _log.LogWarning(FormatMessage(msg));
        }

        public void LogError(object msg)
        {
            if (_isLogShown)
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

        public Log(BepInEx.Logging.ManualLogSource logger, BepInEx.Configuration.ConfigEntry<LogLevel> logLevels)
        {
            Logger = logger;
            LogLevels = logLevels;
        }

        public bool DebugEnabled
        {
            get { return LogLevels.Value.HasDebug(); }
        }
        public bool WarningEnabled
        {
            get { return LogLevels.Value.HasWarning(); }
        }
        public bool InfoEnabled
        {
            get { return LogLevels.Value.HasInfo(); }
        }
        public bool ErrorEnabled
        {
            get { return LogLevels.Value.HasError(); }
        }

        public void LogDebug(object data)
        {
            Logger.LogDebug(data);
        }

        public void LogInfo(object data)
        {
            Logger.LogInfo(data);
        }

        public void LogWarning(object data)
        {
            Logger.LogWarning(data);
        }

        public void LogError(object data)
        {
            Logger.LogError(data);
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
