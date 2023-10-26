import { DependencyContainer } from "tsyringe";

import { IPostDBLoadMod } from "@spt-aki/models/external/IPostDBLoadMod";
import { DatabaseServer } from "@spt-aki/servers/DatabaseServer";
import { ILogger } from "@spt-aki/models/spt/utils/ILogger";
import { ConfigServer } from "@spt-aki/servers/ConfigServer";
import { ConfigTypes } from "@spt-aki/models/enums/ConfigTypes";
import { IPmcConfig } from "@spt-aki/models/spt/config/IPmcConfig";


import config from "../config/config.json";

class DisableDiscardLimits implements IPostDBLoadMod {
  public postDBLoad(container: DependencyContainer): void {
    const databaseServer = container.resolve<DatabaseServer>("DatabaseServer");
    const configServer = container.resolve<ConfigServer>("ConfigServer");
    const pmcConfig = configServer.getConfig<IPmcConfig>(ConfigTypes.PMC);
    const { logInfo } = useLogger(container);

    const tables = databaseServer.getTables();

    /**
     * Set the item generation weights for backpackLoot, vestLoot, and pocketLoot to zero to prevent extra loot items from spawning on the specified bot type
     * @param botTypes 
     */
    const emptyInventory = (botTypes: string[]) => {
      botTypes.forEach((type) => {
        logInfo(`Removing loot from ${type}`);
        const backpackWeights = tables.bots.types[type].generation.items.backpackLoot.weights;
        const vestWeights = tables.bots.types[type].generation.items.vestLoot.weights;
        const pocketWeights = tables.bots.types[type].generation.items.pocketLoot.weights;
        
        Object.keys(backpackWeights).forEach(weight => backpackWeights[weight] = 0);
        Object.keys(vestWeights).forEach(weight => vestWeights[weight] = 0);
        Object.keys(pocketWeights).forEach(weight => pocketWeights[weight] = 0);
      });
    };

    if (!config.pmcSpawnWithLoot) {
      emptyInventory(["usec", "bear"]);
      // Do not allow weapons to spawn in PMC bags
      pmcConfig.looseWeaponInBackpackLootMinMax.max = 0;
    }

    if (!config.scavSpawnWithLoot) {
      emptyInventory(["assault"]);
    }

    logInfo("Marking items with DiscardLimits as InsuranceDisabled");
    for (let itemId in tables.templates.items) {
      const template = tables.templates.items[itemId];
      /**
       * When we set DiscardLimitsEnabled to false further down, this will cause some items to be able to be insured when they normally should not be.
       * The DiscardLimit property is used by BSG for RMT protections and their code internally treats things with discard limits as not insurable.
       * For items that have a DiscardLimit >= 0, we need to manually flag them as InsuranceDisabled to make sure they still cannot be insured by the player.
       * Do not disable insurance if the item is marked as always available for insurance.
       */
      if (
        template._props.DiscardLimit >= 0 &&
        !template._props.IsAlwaysAvailableForInsurance
      ) {
        template._props.InsuranceDisabled = true;
      }
    }

    tables.globals.config.DiscardLimitsEnabled = false;
    logInfo("Global config DiscardLimitsEnabled set to false");
  }
}

function useLogger(container: DependencyContainer) {
  const logger = container.resolve<ILogger>("WinstonLogger");
  return {
    logInfo: (message: string) => {
      logger.info(`[NoDiscardLimit] ${message}`);
    },
  };
}

module.exports = { mod: new DisableDiscardLimits() };
