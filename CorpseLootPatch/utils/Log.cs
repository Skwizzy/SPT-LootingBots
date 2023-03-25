using System;

namespace LootingBots.Patch.Util
{
    public class Log
    {
        public BepInEx.Logging.ManualLogSource Logger;

        public Log(BepInEx.Logging.ManualLogSource Logger)
        {
            this.Logger = Logger;
        }

        public bool isDebug()
        {
            return LootingBots.enabledLogLevels.Value.hasDebug();
        }

        public void logDebug(object data)
        {
            if (LootingBots.enabledLogLevels.Value.hasDebug())
            {
                Logger.LogDebug(data);
            }
        }

        public void logWarning(object data)
        {
            if (LootingBots.enabledLogLevels.Value.hasWarning())
            {
                Logger.LogWarning(data);
            }
        }

        public void logError(object data)
        {
            if (LootingBots.enabledLogLevels.Value.hasError())
            {
                Logger.LogError(data);
            }
        }
    }

    public static class LogUtils
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
            // Info = 16,

            /// <summary>
            ///     A message that would likely only interest a developer.
            /// </summary>
            Debug = 32,

            /// <summary>
            ///     All log levels.
            /// </summary>
            All = Error | Warning | Debug
        }

        public static bool hasError(this LogLevel logLevel)
        {
            return logLevel.HasFlag(LogLevel.Error);
        }

        public static bool hasWarning(this LogLevel logLevel)
        {
            return logLevel.HasFlag(LogLevel.Warning);
        }

        // public static bool hasInfo(this LogLevel logLevel)
        // {
        //     return logLevel.HasFlag(LogLevel.Info);
        // }

        public static bool hasDebug(this LogLevel logLevel)
        {
            return logLevel.HasFlag(LogLevel.Debug);
        }
    }
}
