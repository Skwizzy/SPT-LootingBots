import { HandbookHelper } from "../helpers/HandbookHelper";
import { ItemHelper } from "../helpers/ItemHelper";
import { PresetHelper } from "../helpers/PresetHelper";
import { ProfileHelper } from "../helpers/ProfileHelper";
import { RagfairServerHelper } from "../helpers/RagfairServerHelper";
import { RepeatableQuestHelper } from "../helpers/RepeatableQuestHelper";
import { Exit } from "../models/eft/common/ILocationBase";
import { TraderInfo } from "../models/eft/common/tables/IBotBase";
import { ICompletion, ICompletionAvailableFor, IElimination, IEliminationCondition, IExploration, IExplorationCondition, IRepeatableQuest, IReward, IRewards } from "../models/eft/common/tables/IRepeatableQuests";
import { ITemplateItem } from "../models/eft/common/tables/ITemplateItem";
import { IQuestConfig, IRepeatableQuestConfig } from "../models/spt/config/IQuestConfig";
import { IQuestTypePool } from "../models/spt/repeatable/IQuestTypePool";
import { ILogger } from "../models/spt/utils/ILogger";
import { EventOutputHolder } from "../routers/EventOutputHolder";
import { ConfigServer } from "../servers/ConfigServer";
import { DatabaseServer } from "../servers/DatabaseServer";
import { ItemFilterService } from "../services/ItemFilterService";
import { LocalisationService } from "../services/LocalisationService";
import { PaymentService } from "../services/PaymentService";
import { ProfileFixerService } from "../services/ProfileFixerService";
import { HttpResponseUtil } from "../utils/HttpResponseUtil";
import { JsonUtil } from "../utils/JsonUtil";
import { MathUtil } from "../utils/MathUtil";
import { ObjectId } from "../utils/ObjectId";
import { RandomUtil } from "../utils/RandomUtil";
import { TimeUtil } from "../utils/TimeUtil";
export declare class RepeatableQuestGenerator {
    protected timeUtil: TimeUtil;
    protected logger: ILogger;
    protected randomUtil: RandomUtil;
    protected httpResponse: HttpResponseUtil;
    protected mathUtil: MathUtil;
    protected jsonUtil: JsonUtil;
    protected databaseServer: DatabaseServer;
    protected itemHelper: ItemHelper;
    protected presetHelper: PresetHelper;
    protected profileHelper: ProfileHelper;
    protected profileFixerService: ProfileFixerService;
    protected handbookHelper: HandbookHelper;
    protected ragfairServerHelper: RagfairServerHelper;
    protected eventOutputHolder: EventOutputHolder;
    protected localisationService: LocalisationService;
    protected paymentService: PaymentService;
    protected objectId: ObjectId;
    protected itemFilterService: ItemFilterService;
    protected repeatableQuestHelper: RepeatableQuestHelper;
    protected configServer: ConfigServer;
    protected questConfig: IQuestConfig;
    constructor(timeUtil: TimeUtil, logger: ILogger, randomUtil: RandomUtil, httpResponse: HttpResponseUtil, mathUtil: MathUtil, jsonUtil: JsonUtil, databaseServer: DatabaseServer, itemHelper: ItemHelper, presetHelper: PresetHelper, profileHelper: ProfileHelper, profileFixerService: ProfileFixerService, handbookHelper: HandbookHelper, ragfairServerHelper: RagfairServerHelper, eventOutputHolder: EventOutputHolder, localisationService: LocalisationService, paymentService: PaymentService, objectId: ObjectId, itemFilterService: ItemFilterService, repeatableQuestHelper: RepeatableQuestHelper, configServer: ConfigServer);
    /**
     * This method is called by /GetClientRepeatableQuests/ and creates one element of quest type format (see assets/database/templates/repeatableQuests.json).
     * It randomly draws a quest type (currently Elimination, Completion or Exploration) as well as a trader who is providing the quest
     * @param pmcLevel Player's level for requested items and reward generation
     * @param pmcTraderInfo Players traper standing/rep levels
     * @param questTypePool Possible quest types pool
     * @param repeatableConfig Repeatable quest config
     * @returns IRepeatableQuest
     */
    generateRepeatableQuest(pmcLevel: number, pmcTraderInfo: Record<string, TraderInfo>, questTypePool: IQuestTypePool, repeatableConfig: IRepeatableQuestConfig): IRepeatableQuest;
    /**
     * Generate a randomised Elimination quest
     * @param pmcLevel Player's level for requested items and reward generation
     * @param traderId Trader from which the quest will be provided
     * @param questTypePool Pools for quests (used to avoid redundant quests)
     * @param repeatableConfig The configuration for the repeatably kind (daily, weekly) as configured in QuestConfig for the requestd quest
     * @returns Object of quest type format for "Elimination" (see assets/database/templates/repeatableQuests.json)
     */
    protected generateEliminationQuest(pmcLevel: number, traderId: string, questTypePool: IQuestTypePool, repeatableConfig: IRepeatableQuestConfig): IElimination;
    /**
     * A repeatable quest, besides some more or less static components, exists of reward and condition (see assets/database/templates/repeatableQuests.json)
     * This is a helper method for GenerateEliminationQuest to create a location condition.
     *
     * @param   {string}    location        the location on which to fulfill the elimination quest
     * @returns {object}                    object of "Elimination"-location-subcondition
     */
    protected generateEliminationLocation(location: string[], allowedWeapon: string, allowedWeaponCategory: string): IEliminationCondition;
    /**
     * A repeatable quest, besides some more or less static components, exists of reward and condition (see assets/database/templates/repeatableQuests.json)
     * This is a helper method for GenerateEliminationQuest to create a kill condition.
     *
     * @param   {string}    target          array of target npcs e.g. "AnyPmc", "Savage"
     * @param   {array}     bodyParts       array of body parts with which to kill e.g. ["stomach", "thorax"]
     * @param   {number}    distance        distance from which to kill (currently only >= supported)
     * @returns {object}                    object of "Elimination"-kill-subcondition
     */
    protected generateEliminationCondition(target: string, bodyPart: string[], distance: number, allowedWeapon: string, allowedWeaponCategory: string): IEliminationCondition;
    /**
     * Generates a valid Completion quest
     *
     * @param   {integer}   pmcLevel            player's level for requested items and reward generation
     * @param   {string}    traderId            trader from which the quest will be provided
     * @param   {object}    repeatableConfig    The configuration for the repeatably kind (daily, weekly) as configured in QuestConfig for the requestd quest
     * @returns {object}                        object of quest type format for "Completion" (see assets/database/templates/repeatableQuests.json)
     */
    protected generateCompletionQuest(pmcLevel: number, traderId: string, repeatableConfig: IRepeatableQuestConfig): ICompletion;
    /**
     * A repeatable quest, besides some more or less static components, exists of reward and condition (see assets/database/templates/repeatableQuests.json)
     * This is a helper method for GenerateCompletionQuest to create a completion condition (of which a completion quest theoretically can have many)
     *
     * @param   {string}    targetItemId    id of the item to request
     * @param   {integer}   value           amount of items of this specific type to request
     * @returns {object}                    object of "Completion"-condition
     */
    protected generateCompletionAvailableForFinish(targetItemId: string, value: number): ICompletionAvailableFor;
    /**
     * Generates a valid Exploration quest
     *
     * @param   {integer}   pmcLevel            player's level for reward generation
     * @param   {string}    traderId            trader from which the quest will be provided
     * @param   {object}    questTypePool       Pools for quests (used to avoid redundant quests)
     * @param   {object}    repeatableConfig    The configuration for the repeatably kind (daily, weekly) as configured in QuestConfig for the requestd quest
     * @returns {object}                        object of quest type format for "Exploration" (see assets/database/templates/repeatableQuests.json)
     */
    protected generateExplorationQuest(pmcLevel: number, traderId: string, questTypePool: IQuestTypePool, repeatableConfig: IRepeatableQuestConfig): IExploration;
    /**
     * Convert a location into an quest code can read (e.g. factory4_day into 55f2d3fd4bdc2d5f408b4567)
     * @param locationKey e.g factory4_day
     * @returns guid
     */
    protected getQuestLocationByMapId(locationKey: string): string;
    /**
     * Exploration repeatable quests can specify a required extraction point.
     * This method creates the according object which will be appended to the conditions array
     *
     * @param   {string}        exit                The exit name to generate the condition for
     * @returns {object}                            Exit condition
     */
    protected generateExplorationExitCondition(exit: Exit): IExplorationCondition;
    /**
     * Generate the reward for a mission. A reward can consist of
     * - Experience
     * - Money
     * - Items
     * - Trader Reputation
     *
     * The reward is dependent on the player level as given by the wiki. The exact mapping of pmcLevel to
     * experience / money / items / trader reputation can be defined in QuestConfig.js
     *
     * There's also a random variation of the reward the spread of which can be also defined in the config.
     *
     * Additonaly, a scaling factor w.r.t. quest difficulty going from 0.2...1 can be used
     *
     * @param   {integer}   pmcLevel            player's level
     * @param   {number}    difficulty          a reward scaling factor goint from 0.2 to 1
     * @param   {string}    traderId            the trader for reputation gain (and possible in the future filtering of reward item type based on trader)
     * @param   {object}    repeatableConfig    The configuration for the repeatably kind (daily, weekly) as configured in QuestConfig for the requestd quest
     * @returns {object}                        object of "Reward"-type that can be given for a repeatable mission
     */
    protected generateReward(pmcLevel: number, difficulty: number, traderId: string, repeatableConfig: IRepeatableQuestConfig): IRewards;
    /**
     * Helper to create a reward item structured as required by the client
     *
     * @param   {string}    tpl             itemId of the rewarded item
     * @param   {integer}   value           amount of items to give
     * @param   {integer}   index           all rewards will be appended to a list, for unkown reasons the client wants the index
     * @returns {object}                    object of "Reward"-item-type
     */
    protected generateRewardItem(tpl: string, value: number, index: number, preset?: any): IReward;
    /**
    * Picks rewardable items from items.json. This means they need to fit into the inventory and they shouldn't be keys (debatable)
     * @param repeatableQuestConfig config file
     * @returns a list of rewardable items [[_tpl, itemTemplate],...]
     */
    protected getRewardableItems(repeatableQuestConfig: IRepeatableQuestConfig): [string, ITemplateItem][];
    /**
     * Checks if an id is a valid item. Valid meaning that it's an item that may be a reward
     * or content of bot loot. Items that are tested as valid may be in a player backpack or stash.
     * @param {string} tpl template id of item to check
     * @returns boolean: true if item is valid reward
     */
    protected isValidRewardItem(tpl: string, repeatableQuestConfig: IRepeatableQuestConfig): boolean;
    /**
     * Generates the base object of quest type format given as templates in assets/database/templates/repeatableQuests.json
     * The templates include Elimination, Completion and Extraction quest types
     *
     * @param   {string}    type            quest type: "Elimination", "Completion" or "Extraction"
     * @param   {string}    traderId        trader from which the quest will be provided
     * @param   {string}    side            scav daily or pmc daily/weekly quest
     * @returns {object}                    a object which contains the base elements for repeatable quests of the requests type
     *                                      (needs to be filled with reward and conditions by called to make a valid quest)
     */
    protected generateRepeatableTemplate(type: string, traderId: string, side: string): IRepeatableQuest;
}
