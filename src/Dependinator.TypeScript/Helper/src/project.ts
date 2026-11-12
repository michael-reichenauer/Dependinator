import * as fs from "node:fs";
import * as path from "node:path";
import * as ts from "typescript";
import { createCompilerHost } from "./libraries";

// A project to parse: the compiler's program for the entry file given as the model path.
//   tsconfig.json / jsconfig.json   the files and options it names, with the projects it
//                                   references (a "solution style" config)
//   package.json                    the tsconfig.json or jsconfig.json beside it, or else every
//                                   source file below its folder (a plain JavaScript project)
export interface Project {
    entryPath: string;
    projectDir: string;
    packageName?: string;
    packageDescription?: string;
    program: ts.Program;
}

const SourceExtensions = [".ts", ".tsx", ".mts", ".cts", ".js", ".jsx", ".mjs", ".cjs"];

// Folders that hold what is built or downloaded, never the project's own source.
const OutputFolders = ["node_modules", "dist", "build", "out", "coverage", "bower_components"];

export function loadProject(entryPath: string): Project {
    const fullPath = path.resolve(entryPath);
    const projectDir = path.dirname(fullPath);
    const { name, description } = readPackage(projectDir);

    const configPath = findConfigPath(fullPath);
    const { fileNames, options } = configPath ? readConfig(configPath) : inferProject(projectDir);
    if (fileNames.length === 0) {
        throw new Error(`No TypeScript or JavaScript files found for '${path.basename(fullPath)}'.`);
    }

    const programOptions: ts.CompilerOptions = {
        ...options,
        // Nothing is emitted and the libraries are not checked: only symbols are resolved.
        noEmit: true,
        skipLibCheck: true,
        composite: false,
        incremental: false,
        tsBuildInfoFile: undefined,
    };
    const program = ts.createProgram({
        rootNames: fileNames,
        options: programOptions,
        host: createCompilerHost(programOptions),
    });
    return { entryPath: fullPath, projectDir, packageName: name, packageDescription: description, program };
}

function findConfigPath(entryPath: string): string | undefined {
    if (path.basename(entryPath).toLowerCase() !== "package.json") {
        return entryPath;
    }
    const dir = path.dirname(entryPath);
    return ["tsconfig.json", "jsconfig.json"].map(name => path.join(dir, name)).find(p => fs.existsSync(p));
}

function readPackage(projectDir: string): { name?: string; description?: string } {
    try {
        const json = JSON.parse(fs.readFileSync(path.join(projectDir, "package.json"), "utf8"));
        return {
            name: typeof json.name === "string" ? json.name : undefined,
            description: typeof json.description === "string" ? json.description : undefined,
        };
    } catch {
        return {};
    }
}

// The files and options of a config file, with those of the projects it references: a
// solution style tsconfig.json names no files of its own, only references.
function readConfig(configPath: string): { fileNames: string[]; options: ts.CompilerOptions } {
    const fileNames = new Set<string>();
    const visited = new Set<string>();
    let options: ts.CompilerOptions | undefined;

    const read = (configFile: string) => {
        const key = path.resolve(configFile);
        if (visited.has(key)) {
            return;
        }
        visited.add(key);

        const parsed = ts.getParsedCommandLineOfConfigFile(configFile, undefined, {
            ...ts.sys,
            onUnRecoverableConfigFileDiagnostic: diagnostic => {
                throw new Error(ts.flattenDiagnosticMessageText(diagnostic.messageText, "\n"));
            },
        });
        if (!parsed) {
            return;
        }
        parsed.fileNames.forEach(f => fileNames.add(f));
        // The options of the first config that has files: the entry's own, or those of the
        // first project a solution style config references.
        if (!options && parsed.fileNames.length > 0) {
            options = parsed.options;
        }
        for (const reference of parsed.projectReferences ?? []) {
            const referencePath = ts.resolveProjectReferencePath(reference);
            if (fs.existsSync(referencePath)) {
                read(referencePath);
            }
        }
    };
    read(configPath);

    return { fileNames: [...fileNames], options: options ?? {} };
}

// A project without a config file: every source file below the folder, as JavaScript that may
// hold TypeScript too.
function inferProject(projectDir: string): { fileNames: string[]; options: ts.CompilerOptions } {
    const fileNames = ts.sys.readDirectory(
        projectDir,
        SourceExtensions,
        OutputFolders.map(folder => `**/${folder}`),
        ["**/*"]
    );
    return {
        fileNames,
        options: {
            allowJs: true,
            checkJs: false,
            target: ts.ScriptTarget.ESNext,
            module: ts.ModuleKind.ESNext,
            moduleResolution: ts.ModuleResolutionKind.Bundler,
            jsx: ts.JsxEmit.Preserve,
            resolveJsonModule: true,
            allowImportingTsExtensions: true,
        },
    };
}
