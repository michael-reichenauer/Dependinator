import { Cart } from "./cart";
import { store } from "./store";

export function cartIsEmpty(): boolean {
    return new Cart("test", store).describe() === "0 items";
}
