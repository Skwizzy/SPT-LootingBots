using System.Collections.Generic;
using System.Linq;

using Comfort.Common;

using EFT;
using EFT.InventoryLogic;

using LootingBots.Patch.Util;

namespace LootingBots.Patch.Components
{
    public class ItemAppraiser
    {
        public Log Log;
        public Dictionary<string, EFT.HandBook.HandbookData> HandbookData;
        public Dictionary<string, float> MarketData;

        public bool MarketInitialized = false;

        public void Init()
        {
            Log = LootingBots.LootLog;

            if (LootingBots.UseMarketPrices.Value)
            {
                Singleton<ClientApplication<ISession>>.Instance
                    .GetClientBackEndSession()
                    .RagfairGetPrices(
                        new Callback<Dictionary<string, float>>(
                            (Result<Dictionary<string, float>> result) => MarketData = result.Value
                        )
                    );
                MarketInitialized = true;
            }
            else
            {
                // This is the handbook instance which is initialized when the client first starts.
                HandbookData = Singleton<GClass2532>.Instance.Items.ToDictionary((item) => item.Id);
            }
        }

        /** Will either get the lootItem's price using the ragfair service or the handbook depending on the option selected in the mod menu*/
        public float GetItemPrice(Item lootItem)
        {
            bool valueFromMods = LootingBots.ValueFromMods.Value;
            if (LootingBots.UseMarketPrices.Value && MarketData != null)
            {
                return lootItem is Weapon && valueFromMods
                    ? GetWeaponMarketPrice(lootItem as Weapon)
                    : GetItemMarketPrice(lootItem);
            }

            if (HandbookData != null)
            {
                return lootItem is Weapon && valueFromMods
                    ? GetWeaponHandbookPrice(lootItem as Weapon)
                    : GetItemHandbookPrice(lootItem);
            }

            Log.LogDebug($"ItemAppraiser data is null");

            return 0;
        }

        public float GetWeaponHandbookPrice(Weapon lootWeapon)
        {
            Log.LogDebug($"Getting value of attachments for {lootWeapon.Name.Localized()}");
            float finalPrice = lootWeapon.Mods.Aggregate(
                0f,
                (price, mod) => price += GetItemHandbookPrice(mod)
            );
            Log.LogDebug(
                $"Final price of attachments: {finalPrice} compared to full item {GetItemHandbookPrice(lootWeapon)}"
            );

            return finalPrice;
        }

        /** Gets the price of the item as stated from the beSession handbook values */
        public float GetItemHandbookPrice(Item lootItem)
        {
            float price = HandbookData[lootItem.TemplateId]?.Price ?? 0;

            Log.LogDebug($"Price of {lootItem.Name.Localized()} is {price}");
            return price;
        }

        public float GetWeaponMarketPrice(Weapon lootWeapon)
        {
            Log.LogDebug($"Getting value of attachments for {lootWeapon.Name.Localized()}");
            float finalPrice = lootWeapon.Mods.Aggregate(
                0f,
                (price, mod) => price += GetItemMarketPrice(mod)
            );
            Log.LogDebug(
                $"Final price of attachments: {finalPrice} compared to item template {GetItemMarketPrice(lootWeapon)}"
            );

            return finalPrice;
        }

        public float GetItemMarketPrice(Item lootItem)
        {
            float price = MarketData[lootItem.TemplateId];
            Log.LogDebug($"Price of {lootItem.Name.Localized()} is {price}");

            return price;
        }
    }
}
