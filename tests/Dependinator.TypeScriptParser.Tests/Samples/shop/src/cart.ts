import { Currency, Entity, type Item, type Money, type Priced } from "./model";
import type { Store } from "./store";
import { price } from "@lib/pricing";

/** The items a customer is about to buy. */
export class Cart extends Entity implements Priced {
    #items: Item[] = [];
    static count = 0;

    constructor(
        id: string,
        private readonly store: Store
    ) {
        super(id);
        Cart.count++;
    }

    get price(): Money {
        return { amount: this.total(), currency: Currency.Eur };
    }

    async add(id: string, quantity = 1): Promise<void> {
        const item = await this.store.load(id);
        if (item) {
            this.#items.push(item);
        }
    }

    describe(): string {
        return `${this.#items.length} items`;
    }

    private total(): number {
        return this.#items.reduce((sum, item) => sum + price(item), 0);
    }

    onCleared = (): void => {
        this.#items = [];
    };
}
