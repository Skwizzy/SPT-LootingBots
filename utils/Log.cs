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
            return LootingBots.enableLogging.Value;
        }

        public void logDebug(object data)
        {
            if (isDebug())
            {
                Logger.LogDebug(data);
            }
        }

        public void logWarning(object data)
        {
            if (isDebug())
            {
                Logger.LogWarning(data);
            }
        }
    }
}
