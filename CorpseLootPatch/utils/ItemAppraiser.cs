using EFT.InventoryLogic;
using System.Threading.Tasks;
using Comfort.Common;
using System;
using EFT.UI.Ragfair;
using EFT;
using System.Linq;
using System.Collections.Generic;

namespace LootingBots.Patch.Util
{
    public class ItemAppraiser
    {
        public Log log;
        public GClass2529 handbookData;
        public Dictionary<string, float> marketData;

        public void init()
        {
            this.log = LootingBots.log;

            // This is the handbook instance which is initialized when the client first starts.
            handbookData = Singleton<GClass2529>.Instance;

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
            }
        }

        /** Will either get the lootItem's price using the ragfair service or the handbook depending on the option selected in the mod menu*/
        public float getItemPrice(Item lootItem)
        {
            if (LootingBots.useMarketPrices.Value && marketData != null)
            {
                return getItemMarketPrice(lootItem);
            }

            if (handbookData != null)
            {
                return lootItem is Weapon
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
            EFT.HandBook.HandbookData itemData = Array.Find(
                handbookData.Items,
                item => item?.Id == lootItem.TemplateId
            );

            if (itemData == null)
            {
                log.logWarning("Could not find price in handbook");
            }

            log.logDebug($"Price of {lootItem.Name.Localized()} is {itemData?.Price ?? 0}");
            return itemData?.Price ?? 0;
        }

        public float getItemMarketPrice(Item lootItem)
        {
            log.logDebug($"Attempting to get price of: {lootItem.Name.Localized()}");
            return marketData[lootItem.TemplateId];
        }
    }
}
