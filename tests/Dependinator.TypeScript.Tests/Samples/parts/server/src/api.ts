// The server part: uses Node, a package of the same repository and another part as a package
import { readFileSync } from "fs";
import { join } from "path";
import { shared } from "@acme/core";
import { stamp } from "@acme/util";

export function ping(): string {
    return stamp(readFileSync(join(process.env.HOME ?? "", "ping.txt"), "utf8") + shared());
}
