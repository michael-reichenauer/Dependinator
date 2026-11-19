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
    parts: ProjectPart[];
}

// One config file's program. A config that references other projects is several parts: each
// project is compiled with its own options (its path aliases, types, libraries and JSX), as
// the compiler builds them.
export interface ProjectPart {
    program: ts.Program;
    // The files the config names, apart from what they import
    rootFiles: Set<string>;
}

export const SourceExtensions = [".ts", ".tsx", ".mts", ".cts", ".js", ".jsx", ".mjs", ".cjs"];

// Folders that hold what is built or downloaded, never the project's own source.
const OutputFolders = ["node_modules", "dist", "build", "out", "coverage", "bower_components"];

export function loadProject(entryPath: string): Project {
    const fullPath = path.resolve(entryPath);
    const projectDir = path.dirname(fullPath);
    const { name, description } = readPackage(projectDir);

    const configPath = findConfigPath(fullPath);
    const configs = configPath ? readConfigs(configPath) : [inferProject(projectDir)];
    if (configs.every(config => config.fileNames.length === 0)) {
        throw new Error(`No TypeScript or JavaScript files found for '${path.basename(fullPath)}'.`);
    }

    const withFiles = configs.filter(config => config.fileNames.length > 0);
    const parts = withFiles.map(config => createPart(config, withFiles));
    return { entryPath: fullPath, projectDir, packageName: name, packageDescription: description, parts };
}

function createPart(config: Config, configs: Config[]): ProjectPart {
    const { fileNames, options } = config;
    const programOptions: ts.CompilerOptions = {
        ...options,
        // TypeScript 6 no longer includes the @types packages by itself. Without Node's and the
        // other global typings, what a project uses of them has no declaration to link to.
        types: options.types ?? ["*"],
        // Nothing is emitted and the libraries are not checked: only symbols are resolved.
        noEmit: true,
        skipLibCheck: true,
        composite: false,
        incremental: false,
        tsBuildInfoFile: undefined,
    };
    const host = {
        ...createCompilerHost(programOptions),
        // (Two options of the editor's hosts that the compiler host's type leaves out)
        useSourceOfProjectReferenceRedirect: () => true,
        getParsedCommandLine: readConfig,
    } as ts.CompilerHost;
    const program = ts.createProgram({
        rootNames: fileNames,
        options: programOptions,
        // An import of another part is that part's source, as in the editor: not the declaration
        // files it builds, which may not be built and are no nodes of the model. Every other
        // part counts as referenced, also where a config leaves its "references" to the
        // package manager's workspace.
        projectReferences: configs
            .filter(other => other !== config && other.configPath)
            .map(other => ({ path: other.configPath! })),
        host,
    });
    return { program, rootFiles: new Set(fileNames.map(f => program.getSourceFile(f)?.fileName ?? f)) };
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

interface Config {
    configPath?: string;
    fileNames: string[];
    options: ts.CompilerOptions;
}

// The config files read so far: each program asks for the configs of the parts it references.
const parsedConfigs = new Map<string, ts.ParsedCommandLine | undefined>();

function readConfig(configFile: string): ts.ParsedCommandLine | undefined {
    const key = path.resolve(configFile);
    if (!parsedConfigs.has(key)) {
        parsedConfigs.set(
            key,
            ts.getParsedCommandLineOfConfigFile(configFile, undefined, {
                ...ts.sys,
                onUnRecoverableConfigFileDiagnostic: diagnostic => {
                    throw new Error(ts.flattenDiagnosticMessageText(diagnostic.messageText, "\n"));
                },
            })
        );
    }
    return parsedConfigs.get(key);
}

// A config file's files and options, followed by those of the projects it references (and
// they reference): a solution style tsconfig.json names no files of its own, only references.
function readConfigs(configPath: string): Config[] {
    const configs: Config[] = [];
    const visited = new Set<string>();

    const read = (configFile: string) => {
        const key = path.resolve(configFile);
        if (visited.has(key)) {
            return;
        }
        visited.add(key);

        const parsed = readConfig(configFile);
        if (!parsed) {
            return;
        }
        configs.push({ configPath: key, fileNames: parsed.fileNames, options: parsed.options });
        for (const reference of parsed.projectReferences ?? []) {
            const referencePath = ts.resolveProjectReferencePath(reference);
            if (fs.existsSync(referencePath)) {
                read(referencePath);
            }
        }
    };
    read(configPath);

    return configs;
}

// A project without a config file: every source file below the folder, as JavaScript that may
// hold TypeScript too.
function inferProject(projectDir: string): Config {
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
