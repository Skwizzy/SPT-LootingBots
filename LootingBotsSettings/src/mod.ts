import { DependencyContainer } from "tsyringe";

import { IPostDBLoadMod } from "@spt-aki/models/external/IPostDBLoadMod";
import { DatabaseServer } from "@spt-aki/servers/DatabaseServer";
import { ILogger } from "@spt-aki/models/spt/utils/ILogger";
import { Difficulty } from "@spt-aki/models/eft/common/tables/IBotType";
import modSettings from 'config/settings.json';

class DisableDiscardLimits implements IPostDBLoadMod {
  public postDBLoad(container: DependencyContainer): void {
    const databaseServer = container.resolve<DatabaseServer>("DatabaseServer");
    const logger = container.resolve<ILogger>("WinstonLogger");

    const tables = databaseServer.getTables()
    
    tables.globals.config.DiscardLimitsEnabled = false;
    logger.info("Global config DiscardLimitsEnabled set to false");
    
    Object.values(tables.bots.types).forEach(botType => 
      Object.values(botType.difficulty).forEach((settings: Difficulty) => {
        settings.Patrol.CAN_LOOK_TO_DEADBODIES = true;
        settings.Mind.HOW_WORK_OVER_DEAD_BODY = 2;
        settings.Patrol.DEAD_BODY_SEE_DIST = modSettings.seeDist;
        settings.Patrol.DEAD_BODY_LEAVE_DIST = modSettings.leaveDist;
        settings.Patrol.DEAD_BODY_LOOK_PERIOD = modSettings.lookPeriod;
      })
    )
  }
}

module.exports = { mod: new DisableDiscardLimits() };
