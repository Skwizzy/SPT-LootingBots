using System.Collections.Generic;
using System.Linq;

using Comfort.Common;

using EFT;
using EFT.HandBook;
using EFT.InventoryLogic;

using LootingBots.Patch.Util;

namespace LootingBots.Patch.Components
{
    public class ItemAppraiser
    {
        public Log Log;
        public Dictionary<string, HandbookData> HandbookData;
        public Dictionary<string, float> MarketData;

        public bool MarketInitialized = false;

        public void Init()
        {
            Log = LootingBots.ItemAppraiserLog;

            if (LootingBots.UseMarketPrices.Value)
            {
                // Initialize ragfair prices from the BE session
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
                HandbookData = Singleton<HandbookClass>.Instance.Items.ToDictionary(
                    (item) => item.Id
                );
            }
        }

        /** Will either get the lootItem's price using the ragfair service or the handbook depending on the option selected in the mod menu. If the item is a weapon, will calculate its value based off its attachments if the mod setting is enabled */
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

            if (Log.DebugEnabled)
                Log.LogDebug($"ItemAppraiser data is null");

            return 0;
        }

        /**
        * Get the price of a weapon from the sum of its attachments mods, using the default handbook prices to appraise each mod.
        */
        public float GetWeaponHandbookPrice(Weapon lootWeapon)
        {
            if (Log.DebugEnabled)
                Log.LogDebug($"Getting value of attachments for {lootWeapon.Name.Localized()}");

            float finalPrice = lootWeapon.Mods.Aggregate(
                0f,
                (price, mod) => price += GetItemHandbookPrice(mod)
            );

            if (Log.DebugEnabled)
                Log.LogDebug(
                    $"Final price of attachments: {finalPrice} compared to full item {GetItemHandbookPrice(lootWeapon)}"
                );

            return finalPrice;
        }

        /** Gets the price of the item as stated from the beSession handbook values */
        public float GetItemHandbookPrice(Item lootItem)
        {
            HandbookData.TryGetValue(lootItem.TemplateId, out HandbookData value);
            float price = value?.Price ?? 0;

            if (Log.DebugEnabled)
                Log.LogDebug($"Price of {lootItem.Name.Localized()} is {price}");

            return price;
        }

        /**
        * Get the price of a weapon from the sum of its attachments mods, using the ragfair prices to appraise each mod.
        */
        public float GetWeaponMarketPrice(Weapon lootWeapon)
        {
            if (Log.DebugEnabled)
                Log.LogDebug($"Getting value of attachments for {lootWeapon.Name.Localized()}");

            float finalPrice = lootWeapon.Mods.Aggregate(
                0f,
                (price, mod) => price += GetItemMarketPrice(mod)
            );

            if (Log.DebugEnabled)
                Log.LogDebug(
                    $"Final price of attachments: {finalPrice} compared to item template {GetItemMarketPrice(lootWeapon)}"
                );

            return finalPrice;
        }

        /** Gets the price of the item as stated from the ragfair values */
        public float GetItemMarketPrice(Item lootItem)
        {
            float price = MarketData[lootItem.TemplateId];

            if (Log.DebugEnabled)
                Log.LogDebug($"Price of {lootItem.Name.Localized()} is {price}");

            return price;
        }
    }
}
