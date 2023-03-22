import { OnLoad } from "../di/OnLoad";
import { ILogger } from "../models/spt/utils/ILogger";
import { ImageRouter } from "../routers/ImageRouter";
import { DatabaseServer } from "../servers/DatabaseServer";
import { LocalisationService } from "../services/LocalisationService";
import { EncodingUtil } from "./EncodingUtil";
import { HashUtil } from "./HashUtil";
import { ImporterUtil } from "./ImporterUtil";
import { JsonUtil } from "./JsonUtil";
import { VFS } from "./VFS";
export declare class DatabaseImporter implements OnLoad {
    protected logger: ILogger;
    protected vfs: VFS;
    protected jsonUtil: JsonUtil;
    protected localisationService: LocalisationService;
    protected databaseServer: DatabaseServer;
    protected imageRouter: ImageRouter;
    protected encodingUtil: EncodingUtil;
    protected hashUtil: HashUtil;
    protected importerUtil: ImporterUtil;
    private hashedFile;
    private valid;
    private filepath;
    constructor(logger: ILogger, vfs: VFS, jsonUtil: JsonUtil, localisationService: LocalisationService, databaseServer: DatabaseServer, imageRouter: ImageRouter, encodingUtil: EncodingUtil, hashUtil: HashUtil, importerUtil: ImporterUtil);
    onLoad(): Promise<void>;
    /**
     * Read all json files in database folder and map into a json object
     * @param filepath path to database folder
     */
    protected hydrateDatabase(filepath: string): Promise<void>;
    private onReadValidate;
    getRoute(): string;
    private validateFile;
    loadImages(filepath: string): void;
}
