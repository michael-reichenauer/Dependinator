import type { Item } from "./model";

export interface Store {
    load(id: string): Promise<Item | undefined>;
}

const cache = new Map<string, Item>();

/** Loads an item, from the cache when it is there. */
export const load = async (id: string): Promise<Item | undefined> => cache.get(id);

export const store: Store = { load };

export const defaultLimit = 10;
