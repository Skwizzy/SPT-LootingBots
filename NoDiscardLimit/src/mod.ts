import { DependencyContainer } from "tsyringe";

import { IPostDBLoadMod } from "@spt-aki/models/external/IPostDBLoadMod";
import { DatabaseServer } from "@spt-aki/servers/DatabaseServer";
import { ILogger } from "@spt-aki/models/spt/utils/ILogger";

class DisableDiscardLimits implements IPostDBLoadMod {
  public postDBLoad(container: DependencyContainer): void {
    const databaseServer = container.resolve<DatabaseServer>("DatabaseServer");
    const logger = container.resolve<ILogger>("WinstonLogger");

    const tables = databaseServer.getTables()
    
    tables.globals.config.DiscardLimitsEnabled = false;
    logger.info("Global config DiscardLimitsEnabled set to false");
  }
}

module.exports = { mod: new DisableDiscardLimits() };
