using EFT.InventoryLogic;
using Comfort.Common;
using EFT;
using System.Linq;
using System.Collections.Generic;

namespace LootingBots.Patch.Util
{
    public class ItemAppraiser
    {
        public Log log;
        public Dictionary<string, EFT.HandBook.HandbookData> handbookData;
        public Dictionary<string, float> marketData;

        public bool marketInitialized = false;

        public void init()
        {
            this.log = LootingBots.lootLog;

            if (LootingBots.useMarketPrices.Value)
            {
                Singleton<ClientApplication<ISession>>.Instance
                    .GetClientBackEndSession()
                    .RagfairGetPrices(
                        new Callback<Dictionary<string, float>>(
                            (Result<Dictionary<string, float>> result) =>
                            {
                                marketData = result.Value;
                            }
                        )
                    );
                marketInitialized = true;
            }
            else
            {
                // This is the handbook instance which is initialized when the client first starts.
                handbookData = Singleton<GClass2531>.Instance.Items.ToDictionary((item) => item.Id);
            }
        }

        /** Will either get the lootItem's price using the ragfair service or the handbook depending on the option selected in the mod menu*/
        public float getItemPrice(Item lootItem)
        {
            bool valueFromMods = LootingBots.valueFromMods.Value;
            if (LootingBots.useMarketPrices.Value && marketData != null)
            {
                return lootItem is Weapon && valueFromMods
                    ? getWeaponMarketPrice(lootItem as Weapon)
                    : getItemMarketPrice(lootItem);
            }

            if (handbookData != null)
            {
                return lootItem is Weapon && valueFromMods
                    ? getWeaponHandbookPrice(lootItem as Weapon)
                    : getItemHandbookPrice(lootItem);
            }

            log.logDebug($"ItemAppraiser data is null");

            return 0;
        }

        public float getWeaponHandbookPrice(Weapon lootWeapon)
        {
            log.logDebug($"Getting value of attachments for {lootWeapon.Name.Localized()}");
            float finalPrice = lootWeapon.Mods.Aggregate(
                0f,
                (price, mod) => price += getItemHandbookPrice(mod)
            );
            log.logDebug(
                $"Final price of attachments: {finalPrice} compared to full item {getItemHandbookPrice(lootWeapon)}"
            );

            return finalPrice;
        }

        /** Gets the price of the item as stated from the beSession handbook values */
        public float getItemHandbookPrice(Item lootItem)
        {
            float price = handbookData[lootItem.TemplateId]?.Price ?? 0;

            log.logDebug($"Price of {lootItem.Name.Localized()} is {price}");
            return price;
        }

        public float getWeaponMarketPrice(Weapon lootWeapon)
        {
            log.logDebug($"Getting value of attachments for {lootWeapon.Name.Localized()}");
            float finalPrice = lootWeapon.Mods.Aggregate(
                0f,
                (price, mod) => price += getItemMarketPrice(mod)
            );
            log.logDebug(
                $"Final price of attachments: {finalPrice} compared to item template {getItemMarketPrice(lootWeapon)}"
            );

            return finalPrice;
        }

        public float getItemMarketPrice(Item lootItem)
        {
            float price = marketData[lootItem.TemplateId];
            log.logDebug($"Price of {lootItem.Name.Localized()} is {price}");

            return price;
        }
    }
}
