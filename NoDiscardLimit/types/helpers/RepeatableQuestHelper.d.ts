import { IEliminationConfig, IQuestConfig, IRepeatableQuestConfig } from "../models/spt/config/IQuestConfig";
import { ConfigServer } from "../servers/ConfigServer";
import { JsonUtil } from "../utils/JsonUtil";
import { MathUtil } from "../utils/MathUtil";
import { ProbabilityObject, ProbabilityObjectArray } from "../utils/RandomUtil";
export declare class RepeatableQuestHelper {
    protected mathUtil: MathUtil;
    protected jsonUtil: JsonUtil;
    protected configServer: ConfigServer;
    protected questConfig: IQuestConfig;
    constructor(mathUtil: MathUtil, jsonUtil: JsonUtil, configServer: ConfigServer);
    /**
     * Get the relevant elimination config based on the current players PMC level
     * @param pmcLevel Level of PMC character
     * @param repeatableConfig Main repeatable config
     * @returns IEliminationConfig
     */
    getEliminationConfigByPmcLevel(pmcLevel: number, repeatableConfig: IRepeatableQuestConfig): IEliminationConfig;
    probabilityObjectArray<K, V>(configArrayInput: ProbabilityObject<K, V>[]): ProbabilityObjectArray<K, V>;
}
