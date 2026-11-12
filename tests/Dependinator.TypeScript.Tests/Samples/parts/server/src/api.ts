// The server part: uses Node and a package of the same repository
import { readFileSync } from "fs";
import { shared } from "@acme/core";

export function ping(): string {
    return readFileSync(process.env.HOME + "/ping.txt", "utf8") + shared();
}
