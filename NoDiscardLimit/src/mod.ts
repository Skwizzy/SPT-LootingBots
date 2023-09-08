import { DependencyContainer } from "tsyringe";

import { IPostDBLoadMod } from "@spt-aki/models/external/IPostDBLoadMod";
import { DatabaseServer } from "@spt-aki/servers/DatabaseServer";
import { ILogger } from "@spt-aki/models/spt/utils/ILogger";
import { ConfigServer } from "@spt-aki/servers/ConfigServer";
import { ConfigTypes } from "@spt-aki/models/enums/ConfigTypes";
import { IBotConfig } from "@spt-aki/models/spt/config/IBotConfig";

import config from "../config/config.json";
import { ParentClasses } from "./enums.js";

class DisableDiscardLimits implements IPostDBLoadMod {
  public postDBLoad(container: DependencyContainer): void {
    const databaseServer = container.resolve<DatabaseServer>("DatabaseServer");
    const configServer = container.resolve<ConfigServer>("ConfigServer");
    const botConf = configServer.getConfig<IBotConfig>(ConfigTypes.BOT);
    const { logInfo } = useLogger(container);

    const tables = databaseServer.getTables();

    const allowedItemTypes: string[] = [
      ParentClasses.THROW_WEAPON,
      ParentClasses.AMMO,
      ParentClasses.MEDICAL,
      ParentClasses.MEDKIT,
      ParentClasses.DRUGS,
      ParentClasses.DRINK,
      ParentClasses.FOOD,
      ParentClasses.FOOD_DRINK,
      ParentClasses.STIMULATOR,
    ];
    const pmcConfig = botConf.pmc;

    const emptyInventory = (botTypes: string[]) => {
      botTypes.forEach((type) => {
        logInfo(`Removing loot from ${type}`);
        tables.bots.types[type].inventory.items.Pockets = [];
        tables.bots.types[type].inventory.items.Pockets = [];
      });
    };

    if (!config.pmcSpawnWithLoot) {
      emptyInventory(["usec", "bear"]);
      // Do not allow weapons to spawn in PMC bags
      pmcConfig.looseWeaponInBackpackLootMinMax.min = 0;
      pmcConfig.looseWeaponInBackpackLootMinMax.max = 0;
      // Restrict the amount of food/drink items that a PMC can spawn with
      tables.bots.types["usec"].generation.items.looseLoot.max = 4;
      tables.bots.types["bear"].generation.items.looseLoot.max = 4;


      //have to add all loot items we don't want to pmc blacklist because PMCs use "dynamic loot" pool
      for (let item in tables.templates.items) {
        const {_parent, _id} = tables.templates.items[item];
        if (!allowedItemTypes.includes(_parent)) {
          pmcConfig.pocketLoot.blacklist.push(_id);
          pmcConfig.backpackLoot.blacklist.push(_id);
          pmcConfig.vestLoot.blacklist.push(_id);
        }
      }
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
