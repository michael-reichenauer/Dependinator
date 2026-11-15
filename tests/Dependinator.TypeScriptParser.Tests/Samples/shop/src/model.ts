// The shop's data: what is sold and what it costs.

/** Something with a price. */
export interface Priced {
    readonly price: Money;
    describe(): string;
}

export type Money = {
    amount: number;
    currency: Currency;
};

export enum Currency {
    Eur,
    Sek,
}

/** An item of the catalog. */
export interface Item extends Priced {
    id: string;
    name?: string;
    onChange: (item: Item) => void;
}

export abstract class Entity {
    constructor(readonly id: string) {}

    abstract describe(): string;
}
