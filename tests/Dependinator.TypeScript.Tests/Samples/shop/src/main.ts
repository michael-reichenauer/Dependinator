// The shop's entry point: starts the server.

import "./setup";
import { readFileSync } from "fs";
import chalk from "chalk";
import { createServer, type Server } from "tiny-http";
import { Cart } from "./cart";
import { store } from "./store";

let server: Server | undefined;

/**
 * Starts the shop.
 * @param port The port to listen on.
 */
export async function start(port = 8080): Promise<void> {
    const banner = readFileSync("banner.txt", "utf8");
    const cart = new Cart("cart-1", store);
    await cart.add("item-1");
    const { price } = await import("./lib/pricing");
    server = createServer(path => chalk.green(`${banner} ${path} ${cart.describe()} ${price.name}`));
    server.listen(port);
}

export default function stop(): void {
    server?.close();
}
