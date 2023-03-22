import { DependencyContainer } from "tsyringe";

import { IPostDBLoadMod } from "@spt-aki/models/external/IPostDBLoadMod";
import { DatabaseServer } from "@spt-aki/servers/DatabaseServer";

class DisableDiscardLimits implements IPostDBLoadMod {
  public postDBLoad(container: DependencyContainer): void {
    const databaseServer = container.resolve<DatabaseServer>("DatabaseServer");
    const tables = databaseServer.getTables();

    // Find the ledx item by its Id
    Object.values(tables.templates.items).forEach((item) => {
      if (item._type == "Item" && item._props.DiscardLimit !== undefined) {
        item._props.DiscardLimit = -1;
      }
    });
  }
}

module.exports = { mod: new DisableDiscardLimits() };
