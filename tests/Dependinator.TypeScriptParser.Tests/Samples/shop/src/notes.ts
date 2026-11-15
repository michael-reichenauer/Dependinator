// The shop's notes: the comment forms that describe a declaration (see CommentTests).

// The longest note that is kept
// (longer ones are cut).
export const maxLength = 200,
    minLength = 1;

/* The notes, newest first */
const notes: string[] = [];

// eslint-disable-next-line @typescript-eslint/no-unused-vars
// Adds a note.
export function add(text: string): void {
    notes.unshift(text.slice(0, maxLength));
}

// A comment with an empty line below it is no declaration's

export function clear(): void {
    notes.length = 0;
}

// Formats notes for display.
export class Formatter {
    // Makes a formatter that puts the prefix before each note.
    constructor(readonly prefix: string) {}

    // The notes as lines of text.
    format(): string[] {
        return notes.map(note => this.prefix + note);
    }

    /** The number of notes. */
    get count(): number {
        return notes.length;
    }
}

// What a stored note holds.
export interface Note {
    // When it was written.
    time: number;
    text: string; // a comment at the end of a line describes nothing
    tags: string[];
}

// How important a note is.
export enum Priority {
    // Can wait.
    Low,
    High,
}

/** @deprecated */
// Kept for old callers.
export function old(): void {}
