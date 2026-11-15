import * as ts from "typescript";
import libraries from "virtual:typescript-libraries";

// The compiler's library declarations (lib.es2022.d.ts, lib.dom.d.ts ...), which type what the
// language itself provides: without them an awaited value or an array's element has no type,
// and nothing used of it can be a link. The compiler reads them from files beside itself, but
// the parser is one bundled script, so they are bundled into it (build.mjs) and served to the
// compiler from memory, as files of a folder that does not exist.
const LibraryFolder = "/$typescript-libraries";

// The files parsed so far. A project of several parts is several programs that read the same
// libraries, package types and each other's source, and parsing the DOM's and Node's types for
// each part costs most of its time and memory (a monorepo of 30 parts ran Node out of memory).
// A parsed file is shared by the programs whose options parse and bind it alike, as the editor
// shares them between its projects (the compiler's document registry).
const sourceFiles = new Map<string, ts.SourceFile | undefined>();

// The options that decide how a file is parsed and bound: the compiler's own list (not part of
// its public types), or else the ones it had in TypeScript 6.0.
const SourceFileOptions: string[] = (
    (ts as { sourceFileAffectingCompilerOptions?: { name: string }[] }).sourceFileAffectingCompilerOptions ?? []
).map(option => option.name);
const KnownSourceFileOptions = [
    "target",
    "module",
    "moduleResolution",
    "moduleDetection",
    "jsx",
    "jsxImportSource",
    "importHelpers",
    "alwaysStrict",
    "strict",
    "noFallthroughCasesInSwitch",
    "allowUnusedLabels",
    "allowUnreachableCode",
];

export function createCompilerHost(options: ts.CompilerOptions): ts.CompilerHost {
    const host = ts.createCompilerHost(options, true);
    const optionsKey = [...new Set([...SourceFileOptions, ...KnownSourceFileOptions])]
        .map(name => JSON.stringify(options[name]) ?? "")
        .join("|");
    const library = (fileName: string): string | undefined =>
        fileName.startsWith(`${LibraryFolder}/`) ? libraries[fileName.slice(LibraryFolder.length + 1)] : undefined;

    const { fileExists, readFile, getSourceFile } = host;
    return {
        ...host,
        getDefaultLibLocation: () => LibraryFolder,
        getDefaultLibFileName: compilerOptions => `${LibraryFolder}/${ts.getDefaultLibFileName(compilerOptions)}`,
        fileExists: fileName => library(fileName) !== undefined || fileExists(fileName),
        readFile: fileName => library(fileName) ?? readFile(fileName),
        getSourceFile: (fileName, languageVersionOrOptions, onError, shouldCreateNewSourceFile) => {
            const create = () => {
                const text = library(fileName);
                return text !== undefined
                    ? ts.createSourceFile(fileName, text, languageVersionOrOptions)
                    : getSourceFile(fileName, languageVersionOrOptions, onError, shouldCreateNewSourceFile);
            };
            const parse =
                typeof languageVersionOrOptions === "object"
                    ? [
                          languageVersionOrOptions.languageVersion,
                          languageVersionOrOptions.impliedNodeFormat,
                          languageVersionOrOptions.jsDocParsingMode,
                      ]
                    : [languageVersionOrOptions];
            const key = `${fileName}|${optionsKey}|${parse.join("|")}`;
            if (!sourceFiles.has(key)) {
                sourceFiles.set(key, create());
            }
            return sourceFiles.get(key);
        },
    };
}
