using BepInEx.Bootstrap;
using Comfort.Common;
using EFT;
using EFT.InventoryLogic;
using Fika.Core.Main.Players;
using Fika.Core.Networking;
using Fika.Core.Networking.LiteNetLib;
using Fika.Core.Networking.Packets.Player;
using Version = System.Version;

namespace LootingBots.Utilities;

public static class FikaHandler
{
    private static readonly Version _minimumVersion = new(2, 4, 3);

    public static bool IsPresent { get; private set; }
    public static bool CanUseInterop { get; private set; }

    public static void Init()
    {
        IsPresent = Chainloader.PluginInfos.TryGetValue("com.fika.core", out var pluginInfo);
        if (IsPresent && pluginInfo!.Metadata.Version >= _minimumVersion)
        {
            LootingBots.LootLog.LogInfo("Initializing Fika compatibility");

            CanUseInterop = true;
        }
    }

    public static void TrySendAmmoAddedPacket(Player player, Item item)
    {
        if (!CanUseInterop)
        {
            return;
        }

        SendItemAddedPacket(player, item);
    }

    private static void SendItemAddedPacket(Player player, Item item)
    {
        if (player is not FikaBot fikaBot)
        {
            return;
        }

        var packet = new SpawnItemInInventoryPacket
        {
            NetId = fikaBot.NetId,
            ItemId = item.Id,
            TemplateId = item.TemplateId,
            Amount = item.StackObjectsCount,
            ItemAddress = item.Parent,
        };
        Singleton<IFikaNetworkManager>.Instance.SendData(ref packet, DeliveryMethod.ReliableOrdered);
    }
}
