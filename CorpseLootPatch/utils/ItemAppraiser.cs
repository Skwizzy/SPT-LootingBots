using EFT.InventoryLogic;
using System.Threading.Tasks;
using Comfort.Common;
using System;
using EFT.UI.Ragfair;
using EFT;

namespace LootingBots.Patch.Util
{
    public class ItemAppraiser
    {
        public Log log;
        public GClass2529 handbookData;
        public ISession beSession;

        public ItemAppraiser(Log log, GClass2530<EFT.HandBook.HandbookData> handbookData = null)
        {
            this.log = log;
            // This is the handbook instance which is initialized when the client first starts.
            this.handbookData = Singleton<GClass2529>.Instance;
            this.beSession = Singleton<ClientApplication<ISession>>.Instance.GetClientBackEndSession();

        }

        /** Will either get the lootItem's price using the ragfair service or the handbook depending on the option selected in the mod menu*/
        public async Task<float> getItemPrice(Item lootItem)
        {
            if (LootingBots.useMarketPrices.Value)
            {
                ItemMarketPrices prices = await getItemMarketPrice(lootItem);
                return prices.avg;
            }
            else if (handbookData != null)
            {
                return getItemHandbookPrice(lootItem);
            }

            return 0;
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

        /** Use the beSession to query the RagFair serivce for the price of the current lootItem**/
        public Task<ItemMarketPrices> getItemMarketPrice(Item lootItem)
        {
            log.logDebug($"Attempting to get price of: {lootItem.Name.Localized()}");
            TaskCompletionSource<ItemMarketPrices> promise =
                new TaskCompletionSource<ItemMarketPrices>();

            Task.Factory.StartNew(
                () =>
                    beSession.GetMarketPrices(
                        lootItem.TemplateId,
                        new Callback<ItemMarketPrices>(
                            (Result<ItemMarketPrices> result) =>
                            {
                                log.logDebug(
                                    $"Price of {lootItem.Name.Localized()}: {result.Value?.avg}"
                                );
                                promise.TrySetResult(result.Value);
                            }
                        )
                    )
            );

            return promise.Task;
        }
    }
}
