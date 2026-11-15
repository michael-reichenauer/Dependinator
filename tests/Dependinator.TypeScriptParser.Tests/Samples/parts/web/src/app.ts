// The web part: imports by its path alias, a style sheet of an installed package, a file that
// is not there, and a file of the server part
import { format } from "@/lib/format";
import "tiny-css/dist/tiny.css";
import { missing } from "@/missing";
import { ping } from "../../server/src/api";

// A value and a type of one name
export const Options = { size: 1 };
export type Options = { size: number };

export class Counter {
    #count = 0;
    data$ = "";

    next(): string {
        this.#count++;
        return format(this.#count) + ping() + missing;
    }
}
