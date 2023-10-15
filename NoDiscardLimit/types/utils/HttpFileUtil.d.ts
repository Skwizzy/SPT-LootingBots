/// <reference types="node" />
import { ServerResponse } from "node:http";
import { HttpServerHelper } from "../helpers/HttpServerHelper";
export declare class HttpFileUtil {
    protected httpServerHelper: HttpServerHelper;
    constructor(httpServerHelper: HttpServerHelper);
    sendFile(resp: ServerResponse, file: any): void;
}
