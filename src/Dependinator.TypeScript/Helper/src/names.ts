import * as path from "node:path";
import * as ts from "typescript";

// Node names. "." (and "/") separate the levels of a model node name, so a literal dot in a
// file, folder or package name is written "*" ("extension*ts", shown as "extension.ts"), the
// encoding the C# and Bicep parsers use for solution, project and file names. "$" and "#" have
// meanings of their own in node names ("$Externals") and are not shown, so the ones of
// JavaScript names ("data$", "#count", "posts.$slug.tsx") are written as their full-width
// forms, which the UI shows as "$" and "#" again (NodeName.ToNiceText).

export function encodePart(text: string): string {
    return text.replace(/\./g, "*").replace(/\$/g, "\uFF04").replace(/#/g, "\uFF03");
}

export function child(parentName: string, name: string): string {
    return `${parentName}.${name}`;
}

// The model's root: the package name without its scope, or the project's folder name.
export function rootName(packageName: string | undefined, projectDir: string): string {
    const name = packageName?.split("/").pop() || path.basename(projectDir) || "project";
    return encodePart(name);
}

// "root.src.app.main*ts" for <baseDir>/src/app/main.ts
export function fileName(root: string, baseDir: string, filePath: string): string {
    const relative = path.relative(baseDir, filePath).split(path.sep).join("/");
    return [root, ...relative.split("/").map(encodePart)].join(".");
}

// The name of a declaration as a node name part, or undefined for a name that cannot be one
// (a computed name, or a string name with characters that would split or group the node name).
export function declarationName(name: ts.DeclarationName | undefined): string | undefined {
    if (!name) {
        return undefined;
    }
    if (ts.isIdentifier(name) || ts.isPrivateIdentifier(name)) {
        return encodePart(name.text);
    }
    if (ts.isStringLiteralLike(name) || ts.isNumericLiteral(name)) {
        return /^[\w$#-]+$/.test(name.text) ? encodePart(name.text) : undefined;
    }
    return undefined;
}

// A package's name as npm allows it, with an optional scope
const PackageName = /^(@[a-z0-9][\w.-]*\/)?[a-z0-9][\w.-]*$/i;

// The package a module specifier names: "lodash" for "lodash/fp", "@scope/pkg" for
// "@scope/pkg/sub"; undefined for a relative or absolute path, for "virtual:" style ids and
// for what can be no package's name: the usual path aliases ("@/components/Button", "~/lib/x")
// and Node's subpath imports ("#internal/x").
export function packageOfSpecifier(specifier: string): string | undefined {
    if (specifier.startsWith("node:")) {
        return specifier;
    }
    if (specifier.startsWith(".") || specifier.startsWith("/") || specifier.includes(":") || specifier === "") {
        return undefined;
    }
    const parts = specifier.split("/");
    const name = specifier.startsWith("@") ? parts.slice(0, 2).join("/") : parts[0];
    return PackageName.test(name) ? name : undefined;
}

// The package a file under node_modules belongs to, with a "@types/x" package named as the
// package it types ("@types/node" is "node", "@types/babel__core" is "@babel/core").
export function packageOfFile(filePath: string): string | undefined {
    const normalized = filePath.replace(/\\/g, "/");
    const index = normalized.lastIndexOf("/node_modules/");
    if (index < 0) {
        return undefined;
    }
    const parts = normalized.slice(index + "/node_modules/".length).split("/");
    let name = parts[0].startsWith("@") ? `${parts[0]}/${parts[1]}` : parts[0];
    if (name.startsWith("@types/")) {
        name = name.slice("@types/".length);
        if (name.includes("__")) {
            name = `@${name.replace("__", "/")}`;
        }
    }
    return name;
}

// The node name of an external package: each "/" level is a level in the model, so a scope
// groups its packages ("@angular.core") and Node's built-in modules sit under "node" ("node.fs").
export function packageNodeName(packageName: string): string {
    return packageName.split("/").filter(Boolean).map(encodePart).join(".");
}
