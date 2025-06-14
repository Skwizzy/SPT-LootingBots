using EFT;

namespace LootingBots
{
    public static class LootingBotsExtensions
    {
        public static IPlayer GetClosestPlayer(this List<IPlayer> players, BotOwner botOwner)
        {
            if (players == null || players.Count == 0)
            {
                return null;
            }

            IPlayer closestPlayer = null;
            float closestDistance = float.MaxValue;

            foreach (var player in players)
            {
                if(!player.HealthController.IsAlive)
                {
                    continue;
                }

                float distance = (botOwner.Position - player.Position).sqrMagnitude;

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestPlayer = player;
                }
            }

            return closestPlayer;
        }
    }
}
