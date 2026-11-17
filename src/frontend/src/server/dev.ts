import homepage from "../client/index.html";
import { parseConfig } from "./config.ts";
import { startServer } from "./index.ts";

const config = parseConfig(Bun.env);
await startServer(config, homepage);
