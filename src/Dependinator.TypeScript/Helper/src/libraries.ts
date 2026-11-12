import * as ts from "typescript";
import libraries from "virtual:typescript-libraries";

// The compiler's library declarations (lib.es2022.d.ts, lib.dom.d.ts ...), which type what the
// language itself provides: without them an awaited value or an array's element has no type,
// and nothing used of it can be a link. The compiler reads them from files beside itself, but
// the parser is one bundled script, so they are bundled into it (build.mjs) and served to the
// compiler from memory, as files of a folder that does not exist.
const LibraryFolder = "/$typescript-libraries";

export function createCompilerHost(options: ts.CompilerOptions): ts.CompilerHost {
    const host = ts.createCompilerHost(options, true);
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
            const text = library(fileName);
            return text !== undefined
                ? ts.createSourceFile(fileName, text, languageVersionOrOptions)
                : getSourceFile(fileName, languageVersionOrOptions, onError, shouldCreateNewSourceFile);
        },
    };
}
