import { RepeatableQuestGenerator } from "../generators/RepeatableQuestGenerator";
import { ProfileHelper } from "../helpers/ProfileHelper";
import { RagfairServerHelper } from "../helpers/RagfairServerHelper";
import { RepeatableQuestHelper } from "../helpers/RepeatableQuestHelper";
import { IEmptyRequestData } from "../models/eft/common/IEmptyRequestData";
import { IPmcData } from "../models/eft/common/IPmcData";
import { IPmcDataRepeatableQuest } from "../models/eft/common/tables/IRepeatableQuests";
import { IItemEventRouterResponse } from "../models/eft/itemEvent/IItemEventRouterResponse";
import { IRepeatableQuestChangeRequest } from "../models/eft/quests/IRepeatableQuestChangeRequest";
import { IQuestConfig, IRepeatableQuestConfig } from "../models/spt/config/IQuestConfig";
import { IQuestTypePool } from "../models/spt/repeatable/IQuestTypePool";
import { ILogger } from "../models/spt/utils/ILogger";
import { EventOutputHolder } from "../routers/EventOutputHolder";
import { ConfigServer } from "../servers/ConfigServer";
import { PaymentService } from "../services/PaymentService";
import { ProfileFixerService } from "../services/ProfileFixerService";
import { HttpResponseUtil } from "../utils/HttpResponseUtil";
import { JsonUtil } from "../utils/JsonUtil";
import { ObjectId } from "../utils/ObjectId";
import { RandomUtil } from "../utils/RandomUtil";
import { TimeUtil } from "../utils/TimeUtil";
export declare class RepeatableQuestController {
    protected timeUtil: TimeUtil;
    protected logger: ILogger;
    protected randomUtil: RandomUtil;
    protected httpResponse: HttpResponseUtil;
    protected jsonUtil: JsonUtil;
    protected profileHelper: ProfileHelper;
    protected profileFixerService: ProfileFixerService;
    protected ragfairServerHelper: RagfairServerHelper;
    protected eventOutputHolder: EventOutputHolder;
    protected paymentService: PaymentService;
    protected objectId: ObjectId;
    protected repeatableQuestGenerator: RepeatableQuestGenerator;
    protected repeatableQuestHelper: RepeatableQuestHelper;
    protected configServer: ConfigServer;
    protected questConfig: IQuestConfig;
    constructor(timeUtil: TimeUtil, logger: ILogger, randomUtil: RandomUtil, httpResponse: HttpResponseUtil, jsonUtil: JsonUtil, profileHelper: ProfileHelper, profileFixerService: ProfileFixerService, ragfairServerHelper: RagfairServerHelper, eventOutputHolder: EventOutputHolder, paymentService: PaymentService, objectId: ObjectId, repeatableQuestGenerator: RepeatableQuestGenerator, repeatableQuestHelper: RepeatableQuestHelper, configServer: ConfigServer);
    /**
     * Handle client/repeatalbeQuests/activityPeriods
     * Returns an array of objects in the format of repeatable quests to the client.
     * repeatableQuestObject = {
     *  id: Unique Id,
     *  name: "Daily",
     *  endTime: the time when the quests expire
     *  activeQuests: currently available quests in an array. Each element of quest type format (see assets/database/templates/repeatableQuests.json).
     *  inactiveQuests: the quests which were previously active (required by client to fail them if they are not completed)
     * }
     *
     * The method checks if the player level requirement for repeatable quests (e.g. daily lvl5, weekly lvl15) is met and if the previously active quests
     * are still valid. This ischecked by endTime persisted in profile accordning to the resetTime configured for each repeatable kind (daily, weekly)
     * in QuestCondig.js
     *
     * If the condition is met, new repeatableQuests are created, old quests (which are persisted in the profile.RepeatableQuests[i].activeQuests) are
     * moved to profile.RepeatableQuests[i].inactiveQuests. This memory is required to get rid of old repeatable quest data in the profile, otherwise
     * they'll litter the profile's Quests field.
     * (if the are on "Succeed" but not "Completed" we keep them, to allow the player to complete them and get the rewards)
     * The new quests generated are again persisted in profile.RepeatableQuests
     *
     *
     * @param   {string}    sessionId       Player's session id
     * @returns  {array}                    array of "repeatableQuestObjects" as descibed above
     */
    getClientRepeatableQuests(_info: IEmptyRequestData, sessionID: string): IPmcDataRepeatableQuest[];
    /**
     * Get repeatable quest data from profile from name (daily/weekly), creates base repeatable quest object if none exists
     * @param repeatableConfig daily/weekly config
     * @param pmcData Profile to search
     * @returns IPmcDataRepeatableQuest
     */
    protected getRepeatableQuestSubTypeFromProfile(repeatableConfig: IRepeatableQuestConfig, pmcData: IPmcData): IPmcDataRepeatableQuest;
    /**
     * Just for debug reasons. Draws dailies a random assort of dailies extracted from dumps
     */
    generateDebugDailies(dailiesPool: any, factory: any, number: number): any;
    /**
     * Used to create a quest pool during each cycle of repeatable quest generation. The pool will be subsequently
     * narrowed down during quest generation to avoid duplicate quests. Like duplicate extractions or elimination quests
     * where you have to e.g. kill scavs in same locations.
     * @param repeatableConfig main repeatable quest config
     * @param pmcLevel level of pmc generating quest pool
     * @returns IQuestTypePool
     */
    protected generateQuestPool(repeatableConfig: IRepeatableQuestConfig, pmcLevel: number): IQuestTypePool;
    protected createBaseQuestPool(repeatableConfig: IRepeatableQuestConfig): IQuestTypePool;
    debugLogRepeatableQuestIds(pmcData: IPmcData): void;
    /**
     * Handle RepeatableQuestChange event
     */
    changeRepeatableQuest(pmcData: IPmcData, changeRequest: IRepeatableQuestChangeRequest, sessionID: string): IItemEventRouterResponse;
}
