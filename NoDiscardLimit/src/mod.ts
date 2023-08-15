import { DependencyContainer } from "tsyringe";

import { IPostDBLoadMod } from "@spt-aki/models/external/IPostDBLoadMod";
import { DatabaseServer } from "@spt-aki/servers/DatabaseServer";
import { ILogger } from "@spt-aki/models/spt/utils/ILogger";
import { ConfigServer } from "@spt-aki/servers/ConfigServer";
import { ConfigTypes } from "@spt-aki/models/enums/ConfigTypes";
import { IBotConfig } from "@spt-aki/models/spt/config/IBotConfig";

const config = require("../config/config.json");
import { ParentClasses } from "./enums.js";

class DisableDiscardLimits implements IPostDBLoadMod {
  public postDBLoad(container: DependencyContainer): void {
    const databaseServer = container.resolve<DatabaseServer>("DatabaseServer");
    const logger = container.resolve<ILogger>("WinstonLogger");
    const configServer = container.resolve<ConfigServer>("ConfigServer");
    const botConf = configServer.getConfig<IBotConfig>(ConfigTypes.BOT);

    const tables = databaseServer.getTables();

    const allowedItemTypes: string[] = [ParentClasses.THROW_WEAPON, ParentClasses.AMMO, ParentClasses.MEDICAL, ParentClasses.MEDKIT, ParentClasses.DRUGS, ParentClasses.DRINK, ParentClasses.FOOD, ParentClasses.FOOD_DRINK, ParentClasses.STIMULATOR];

    for (let i in config.bot_types) {
      let botType = config.bot_types[i];
      if (config.empty_pockets) {
        tables.bots.types[botType].inventory.items.Pockets = [];
      }
      if (config.empty_bag) {
        tables.bots.types[botType].inventory.items.Backpack = [];
      }
    }

    botConf.pmc.looseWeaponInBackpackLootMinMax.min = 0;
    botConf.pmc.looseWeaponInBackpackLootMinMax.max = 0;

    //have to add all loot items we don't want to pmc blacklist because PMCs use "dynamic loot" pool
    for (let item in tables.templates.items) {
      let serverItem = tables.templates.items[item];
      for (let allowedItem in allowedItemTypes) {
        if (serverItem._parent !== allowedItemTypes[allowedItem]) {
          if (config.empty_pockets) {
            botConf.pmc.pocketLoot.blacklist.push(serverItem._id);
          }
          if (config.empty_bag) {
            botConf.pmc.backpackLoot.blacklist.push(serverItem._id);
          }
          botConf.pmc.vestLoot.blacklist.push(serverItem._id);
        }
      }
    }

    logger.info("Marking items with DiscardLimits as InsuranceDisabled")
    for (let itemId in tables.templates.items) {
      const template = tables.templates.items[itemId];
      // When we set DiscardLimitsEnabled to false further down, this will cause some items to be able to be insured when they normally should not be.
      // The DiscardLimit property is used by BSG for RMT protections and their code internally treats things with discard limits as not insurable.
      // For items that have a DiscardLimit >= 0, we need to manually flag them as InsuranceDisabled to make sure they still cannot be insured by the player. 
      if (template._props.DiscardLimit >= 0) {
        template._props.InsuranceDisabled = true;
      }
    }

    tables.globals.config.DiscardLimitsEnabled = false;
    logger.info("Global config DiscardLimitsEnabled set to false");
  }
}

module.exports = { mod: new DisableDiscardLimits() };
