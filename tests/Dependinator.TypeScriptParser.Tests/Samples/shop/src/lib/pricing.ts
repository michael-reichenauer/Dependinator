import { Currency, type Item } from "../model";
import { settings } from "./config";

/** The price of an item, with tax. */
export function price(item: Item): number;
export function price(item: Item, currency: Currency): number;
export function price(item: Item, currency = Currency.Eur): number {
    return round(item.price.amount * (1 + settings.vat), currency);
}

function round(amount: number, currency: Currency): number {
    return currency === Currency.Sek ? Math.round(amount) : Math.round(amount * 100) / 100;
}
