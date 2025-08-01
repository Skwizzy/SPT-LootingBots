using EFT;
using EFT.InventoryLogic;

namespace LootingBots
{
    public static class LootingBotsExtensions
    {
        public static IPlayer GetClosestPlayer(this IEnumerable<IPlayer> players, BotOwner botOwner)
        {
            if (players == null || !players.Any())
            {
                return null;
            }

            IPlayer closestPlayer = null;
            float closestDistance = float.MaxValue;

            foreach (var player in players)
            {
                if (!player.HealthController.IsAlive)
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

        public static Item GetFirstItem(this IEnumerable<Item> items)
        {
            if (items == null)
                return null;

            using var enumerator = items.GetEnumerator();
            return enumerator.MoveNext() ? enumerator.Current : null;
        }
    }
}
