import { DependencyContainer } from "tsyringe";

import { IPostDBLoadMod } from "@spt-aki/models/external/IPostDBLoadMod";
import { DatabaseServer } from "@spt-aki/servers/DatabaseServer";
import { ILogger } from "@spt-aki/models/spt/utils/ILogger";

class DisableDiscardLimits implements IPostDBLoadMod {
  public postDBLoad(container: DependencyContainer): void {
    const databaseServer = container.resolve<DatabaseServer>("DatabaseServer");
    const logger = container.resolve<ILogger>("WinstonLogger");

    const tables = databaseServer.getTables()
    
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
