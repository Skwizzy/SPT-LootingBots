import { FenceBaseAssortGenerator } from "@spt/generators/FenceBaseAssortGenerator";
import { PaymentHelper } from "@spt/helpers/PaymentHelper";
import { ProfileHelper } from "@spt/helpers/ProfileHelper";
import { TraderAssortHelper } from "@spt/helpers/TraderAssortHelper";
import { TraderHelper } from "@spt/helpers/TraderHelper";
import { ITraderAssort, ITraderBase } from "@spt/models/eft/common/tables/ITrader";
import { IGetItemPricesResponse } from "@spt/models/eft/game/IGetItemPricesResponse";
import { ITraderConfig } from "@spt/models/spt/config/ITraderConfig";
import type { ILogger } from "@spt/models/spt/utils/ILogger";
import { ConfigServer } from "@spt/servers/ConfigServer";
import { DatabaseService } from "@spt/services/DatabaseService";
import { FenceService } from "@spt/services/FenceService";
import { RagfairPriceService } from "@spt/services/RagfairPriceService";
import { TraderAssortService } from "@spt/services/TraderAssortService";
import { TraderPurchasePersisterService } from "@spt/services/TraderPurchasePersisterService";
import { TimeUtil } from "@spt/utils/TimeUtil";
import type { ICloner } from "@spt/utils/cloners/ICloner";
export declare class TraderController {
    protected logger: ILogger;
    protected timeUtil: TimeUtil;
    protected databaseService: DatabaseService;
    protected traderAssortHelper: TraderAssortHelper;
    protected profileHelper: ProfileHelper;
    protected traderHelper: TraderHelper;
    protected paymentHelper: PaymentHelper;
    protected traderAssortService: TraderAssortService;
    protected ragfairPriceService: RagfairPriceService;
    protected traderPurchasePersisterService: TraderPurchasePersisterService;
    protected fenceService: FenceService;
    protected fenceBaseAssortGenerator: FenceBaseAssortGenerator;
    protected configServer: ConfigServer;
    protected cloner: ICloner;
    protected traderConfig: ITraderConfig;
    constructor(logger: ILogger, timeUtil: TimeUtil, databaseService: DatabaseService, traderAssortHelper: TraderAssortHelper, profileHelper: ProfileHelper, traderHelper: TraderHelper, paymentHelper: PaymentHelper, traderAssortService: TraderAssortService, ragfairPriceService: RagfairPriceService, traderPurchasePersisterService: TraderPurchasePersisterService, fenceService: FenceService, fenceBaseAssortGenerator: FenceBaseAssortGenerator, configServer: ConfigServer, cloner: ICloner);
    /**
     * Runs when onLoad event is fired
     * Iterate over traders, ensure a pristine copy of their assorts is stored in traderAssortService
     * Store timestamp of next assort refresh in nextResupply property of traders .base object
     */
    load(): void;
    /**
     * Runs when onUpdate is fired
     * If current time is > nextResupply(expire) time of trader, refresh traders assorts and
     * Fence is handled slightly differently
     * @returns has run
     */
    update(): boolean;
    /**
     * Handle client/trading/api/traderSettings
     * Return an array of all traders
     * @param sessionID Session id
     * @returns array if ITraderBase objects
     */
    getAllTraders(sessionID: string): ITraderBase[];
    /**
     * Order traders by their traderId (Ttid)
     * @param traderA First trader to compare
     * @param traderB Second trader to compare
     * @returns 1,-1 or 0
     */
    protected sortByTraderId(traderA: ITraderBase, traderB: ITraderBase): number;
    /** Handle client/trading/api/getTrader */
    getTrader(sessionID: string, traderID: string): ITraderBase;
    /** Handle client/trading/api/getTraderAssort */
    getAssort(sessionId: string, traderId: string): ITraderAssort;
    /** Handle client/items/prices/TRADERID */
    getItemPrices(sessionId: string, traderId: string): IGetItemPricesResponse;
}
