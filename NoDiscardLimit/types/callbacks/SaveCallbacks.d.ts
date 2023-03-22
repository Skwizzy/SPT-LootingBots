import { OnLoad } from "../di/OnLoad";
import { OnUpdate } from "../di/OnUpdate";
import { SaveServer } from "../servers/SaveServer";
export declare class SaveCallbacks implements OnLoad, OnUpdate {
    protected saveServer: SaveServer;
    constructor(saveServer: SaveServer);
    onLoad(): Promise<void>;
    getRoute(): string;
    onUpdate(secondsSinceLastRun: number): Promise<boolean>;
}
