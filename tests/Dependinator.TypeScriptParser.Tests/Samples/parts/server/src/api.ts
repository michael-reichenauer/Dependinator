// The server part: uses Node, a package of the same repository and another part as a package
import { readFileSync } from "fs";
import { join } from "path";
import { shared } from "@acme/core";
import { stamp } from "@acme/util";
import { area } from "shapes/square";
import { gone } from "shapes/gone";
import "tiny-css/dist/tiny.css";

export function ping(): string {
    const text = readFileSync(join(process.env.HOME ?? "", "ping.txt"), "utf8");
    return stamp(text + shared() + area(2) + gone);
}
