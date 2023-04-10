import { MinMax } from "../../common/MinMax";
import { IBaseConfig } from "./IBaseConfig";
export interface IAirdropConfig extends IBaseConfig {
    kind: "aki-airdrop";
    airdropChancePercent: AirdropChancePercent;
    planeMinFlyHeight: number;
    planeMaxFlyHeight: number;
    planeVolume: number;
    planeSpeed: number;
    crateFallSpeed: number;
    airdropMinStartTimeSeconds: number;
    airdropMaxStartTimeSeconds: number;
    loot: AirdropLoot;
}
export interface AirdropChancePercent {
    bigmap: number;
    woods: number;
    lighthouse: number;
    shoreline: number;
    interchange: number;
    reserve: number;
    tarkovStreets: number;
}
export interface AirdropLoot {
    presetCount: MinMax;
    itemCount: MinMax;
    itemBlacklist: string[];
    itemTypeWhitelist: string[];
    /** key: item base type: value: max count */
    itemLimits: Record<string, number>;
    itemStackLimits: Record<string, MinMax>;
    armorLevelWhitelist: number[];
}
