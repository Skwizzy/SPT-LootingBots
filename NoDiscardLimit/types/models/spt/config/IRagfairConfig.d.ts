import { MinMax } from "../../common/MinMax";
import { IBaseConfig } from "./IBaseConfig";
export interface IRagfairConfig extends IBaseConfig {
    kind: "aki-ragfair";
    runIntervalSeconds: number;
    sell: Sell;
    traders: Record<string, boolean>;
    dynamic: Dynamic;
}
export interface Sell {
    fees: boolean;
    chance: Chance;
    time: Time;
    reputation: Reputation;
    simulatedSellHours: number;
}
export interface Chance {
    base: number;
    overpriced: number;
    underpriced: number;
}
export interface Time {
    base: number;
    min: number;
    max: number;
}
export interface Reputation {
    gain: number;
    loss: number;
}
export interface Dynamic {
    purchasesAreFoundInRaid: boolean;
    /** Use the highest trader price for an offer if its greater than the price in templates/prices.json */
    useTraderPriceForOffersIfHigher: boolean;
    barter: Barter;
    offerAdjustment: OfferAdjustment;
    expiredOfferThreshold: number;
    offerItemCount: MinMax;
    price: MinMax;
    presetPrice: MinMax;
    showDefaultPresetsOnly: boolean;
    endTimeSeconds: MinMax;
    condition: Condition;
    stackablePercent: MinMax;
    nonStackableCount: MinMax;
    rating: MinMax;
    currencies: Record<string, number>;
    showAsSingleStack: string[];
    removeSeasonalItemsWhenNotInEvent: boolean;
    blacklist: Blacklist;
}
export interface Barter {
    enable: boolean;
    chancePercent: number;
    itemCountMin: number;
    itemCountMax: number;
    priceRangeVariancePercent: number;
    minRoubleCostToBecomeBarter: number;
    itemTypeBlacklist: string[];
}
export interface OfferAdjustment {
    maxPriceDifferenceBelowHandbookPercent: number;
    handbookPriceMultipier: number;
    priceThreshholdRub: number;
}
export interface Condition {
    conditionChance: number;
    min: number;
    max: number;
}
export interface Blacklist {
    /**
     * show/hide trader items that are blacklisted by bsg
     */
    traderItems: boolean;
    custom: string[];
    enableBsgList: boolean;
    enableQuestList: boolean;
}
