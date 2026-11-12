import * as fs from "node:fs";
import { builtinModules } from "node:module";
import * as path from "node:path";
import * as ts from "typescript";
import type { Item, LinkProperties, NodeProperties, NodeType } from "./items";
import * as names from "./names";
import type { Project, ProjectPart } from "./project";
import { accessorSignature, functionSignature, variableSignature } from "./signatures";

// Walks a compiled TypeScript or JavaScript project into model items.
//
// Hierarchy (dotted node names, see names.ts):
//   dependinator                              the project (Solution), named after its package
//   dependinator.src                          a folder (Folder)
//   dependinator.src.extension*ts             a source file (SourceFile)
//   dependinator.src.extension*ts.activate    a function or variable of the file: a member row
//   dependinator.src.types*ts.Options         a class, interface, enum or type alias
//   dependinator.src.types*ts.Options.port    its member: a member row
//   vscode.window                             what is used of an installed package (the model
//   node.fs.readFileSync                      puts top level names it has no node for under
//                                             its Externals node); Node's own modules under "node"
//
// What a file does not export is private to it, like a class's private members.
//
// Links come from the compiler's symbols: every identifier in a declaration that names
// something declared elsewhere is a link from that declaration (or from the file, for top level
// code) to the node of what it names. Imports are no links of their own, only what is used.
export interface ParseOptions {
    includeTests: boolean;
}

const TestFile = /\.(test|spec|e2e)\.[cm]?[jt]sx?$/i;
const TestFolders = new Set(["test", "tests", "__tests__", "__mocks__", "e2e"]);

interface OwnFile {
    file: ts.SourceFile;
    part: ProjectPart;
}

export class ModelBuilder {
    // The program and checker of the part whose files are being declared or linked
    program!: ts.Program;
    checker!: ts.TypeChecker;
    resolutionCache!: ts.ModuleResolutionCache;
    readonly root: string;

    readonly nodes = new Map<string, NodeProperties>();
    readonly links = new Map<string, { Source: string; Target: string; Properties: LinkProperties }>();
    readonly problems: Item[] = [];

    // The node of each declaration that has one, to find the node a symbol's declaration is (in).
    readonly declarationNodes = new Map<ts.Node, string>();
    // The same by its place in the source, for a declaration met through another part's
    // program, which has its own syntax tree of the file. Only kept for a project of several parts.
    readonly declarationPlaces = new Map<string, string>();
    readonly missingPackages = new Set<string>();
    readonly resolutionCaches = new Map<ProjectPart, ts.ModuleResolutionCache>();
    readonly sourcePackages = new Map<string, string | undefined>();
    readonly installedPackages = new Map<string, boolean>();
    ownFiles: OwnFile[] = [];
    baseDir = "";
    skippedTestFiles = 0;

    constructor(
        readonly project: Project,
        readonly options: ParseOptions
    ) {
        this.root = names.rootName(project.packageName, project.projectDir);
    }

    use(part: ProjectPart): void {
        if (this.program === part.program) {
            return;
        }
        this.program = part.program;
        this.checker = part.program.getTypeChecker();
        let cache = this.resolutionCaches.get(part);
        if (!cache) {
            cache = ts.createModuleResolutionCache(
                this.project.projectDir,
                fileName => (ts.sys.useCaseSensitiveFileNames ? fileName : fileName.toLowerCase()),
                part.program.getCompilerOptions()
            );
            this.resolutionCaches.set(part, cache);
        }
        this.resolutionCache = cache;
    }

    build(): Item[] {
        this.ownFiles = this.findOwnFiles();
        if (this.ownFiles.length === 0) {
            throw new Error(
                this.skippedTestFiles > 0
                    ? "The project contains only test files. Turn on Include test files in Menu > Settings to parse them."
                    : "No TypeScript or JavaScript source files found in the project."
            );
        }
        this.baseDir = commonDirectory([this.project.projectDir, ...this.ownFiles.map(f => path.dirname(f.file.fileName))]);

        this.addNode(this.root, {
            Type: "Solution",
            Description: this.project.packageDescription ?? `${this.languageName()} project`,
            // The file that defines the project, to open from the node
            FileSpan: { Path: path.normalize(this.project.entryPath), StartLine: 0, EndLine: 0 },
        });

        for (const { file, part } of this.ownFiles) {
            this.use(part);
            this.declareFile(file);
        }
        for (const { file, part } of this.ownFiles) {
            this.use(part);
            this.linkFile(file);
        }
        this.addProblems();

        return [
            ...[...this.nodes].map(([Name, Properties]) => ({ Node: { Name, Properties } })),
            ...[...this.links.values()].map(Link => ({ Link })),
            ...this.problems,
        ];
    }

    // --- Files ---------------------------------------------------------------------------

    // The project's own source: not a declaration file, not from a package, and no test unless
    // asked for. A file belongs to the part whose config names it (the last, for a file that a
    // referenced project and the project that references it both name), or else to the first
    // part that imports it.
    findOwnFiles(): OwnFile[] {
        const owners = new Map<string, OwnFile>();
        const skippedTests = new Set<string>();
        const claim = (part: ProjectPart, isRoot: boolean) => {
            for (const file of part.program.getSourceFiles()) {
                if (
                    owners.has(file.fileName) ||
                    part.rootFiles.has(file.fileName) !== isRoot ||
                    file.isDeclarationFile ||
                    file.fileName.includes("/node_modules/") ||
                    // A package of the same repository, linked into node_modules, unless it is a part itself
                    (!isRoot && part.program.isSourceFileFromExternalLibrary(file))
                ) {
                    continue;
                }
                if (!this.options.includeTests && isTestFile(file.fileName, this.project.projectDir)) {
                    skippedTests.add(file.fileName);
                    continue;
                }
                owners.set(file.fileName, { file, part });
            }
        };
        const parts = this.project.parts;
        [...parts].reverse().forEach(part => claim(part, true));
        parts.forEach(part => claim(part, false));

        this.skippedTestFiles = skippedTests.size;
        return [...owners.values()].sort((a, b) => a.file.fileName.localeCompare(b.file.fileName));
    }

    languageName(): string {
        return this.ownFiles.some(f => /\.[cm]?tsx?$/i.test(f.file.fileName)) ? "TypeScript" : "JavaScript";
    }

    fileNodeName(file: ts.SourceFile): string {
        return names.fileName(this.root, this.baseDir, path.normalize(file.fileName));
    }

    // --- Pass 1: nodes --------------------------------------------------------------------

    declareFile(file: ts.SourceFile): void {
        const fileName = this.fileNodeName(file);
        this.declareFolders(fileName);
        const lastLine = file.getLineAndCharacterOfPosition(file.getEnd()).line;
        this.addNode(fileName, {
            Type: "SourceFile",
            Description: fileDescription(file),
            FileSpan: { Path: path.normalize(file.fileName), StartLine: 0, EndLine: lastLine },
        });
        this.setDeclared(file, fileName);

        const exported = this.exportedDeclarations(file);
        for (const statement of file.statements) {
            this.declareStatement(statement, fileName, exported);
        }
    }

    // The folders above a file, each a level of its name between the root and the file itself.
    declareFolders(fileName: string): void {
        const levels = fileName.split(".");
        for (let count = 2; count < levels.length; count++) {
            this.addNode(levels.slice(0, count).join("."), { Type: "Folder" });
        }
    }

    // The declarations a module exports, in any of the forms ("export function", "export { x }",
    // "export default x", "module.exports = { x }"); undefined for a script, whose top level
    // declarations are all global.
    exportedDeclarations(file: ts.SourceFile): Set<ts.Node> | undefined {
        // A CommonJS file has its module symbol only on the file itself
        const moduleSymbol = this.checker.getSymbolAtLocation(file) ?? (file as { symbol?: ts.Symbol }).symbol;
        if (!moduleSymbol) {
            return undefined;
        }
        const exported = new Set<ts.Node>();
        for (const symbol of this.checker.getExportsOfModule(moduleSymbol)) {
            for (const declaration of this.resolveAlias(symbol)?.declarations ?? []) {
                exported.add(declaration);
                // CommonJS exports a name by assigning it: module.exports = { start }, exports.start = start
                const assigned = ts.isShorthandPropertyAssignment(declaration)
                    ? this.checker.getShorthandAssignmentValueSymbol(declaration)
                    : ts.isPropertyAssignment(declaration) && ts.isIdentifier(declaration.initializer)
                      ? this.symbolAt(declaration.initializer)
                      : ts.isBinaryExpression(declaration) && ts.isIdentifier(declaration.right)
                        ? this.symbolAt(declaration.right)
                        : ts.isPropertyAccessExpression(declaration) &&
                            ts.isBinaryExpression(declaration.parent) &&
                            ts.isIdentifier(declaration.parent.right)
                          ? this.symbolAt(declaration.parent.right)
                          : undefined;
                assigned?.declarations?.forEach(d => exported.add(d));
            }
        }
        return exported;
    }

    declareStatement(statement: ts.Statement, parent: string, exported: Set<ts.Node> | undefined): void {
        const isPrivate = (declaration: ts.Node) => (exported && !exported.has(declaration) ? true : undefined);

        if (ts.isFunctionDeclaration(statement)) {
            const name = names.declarationName(statement.name) ?? (isDefaultExport(statement) ? "default" : undefined);
            if (name) {
                this.declare(statement, names.child(parent, name), {
                    Type: "FunctionMember",
                    Signature: functionSignature(statement, this.checker),
                    IsPrivate: isPrivate(statement),
                });
            }
        } else if (ts.isClassDeclaration(statement)) {
            const name = names.declarationName(statement.name) ?? (isDefaultExport(statement) ? "default" : undefined);
            if (name) {
                this.declareClass(statement, names.child(parent, name), isPrivate(statement));
            }
        } else if (ts.isInterfaceDeclaration(statement)) {
            const name = names.child(parent, names.encodePart(statement.name.text));
            this.declare(statement, name, { Type: "InterfaceType", IsPrivate: isPrivate(statement) });
            this.declareTypeMembers(statement.members, name);
        } else if (ts.isTypeAliasDeclaration(statement)) {
            const name = names.child(parent, names.encodePart(statement.name.text));
            this.declare(statement, name, { Type: "Type", IsPrivate: isPrivate(statement) });
            if (ts.isTypeLiteralNode(statement.type)) {
                this.declareTypeMembers(statement.type.members, name);
            }
        } else if (ts.isEnumDeclaration(statement)) {
            const name = names.child(parent, names.encodePart(statement.name.text));
            this.declare(statement, name, { Type: "EnumType", IsPrivate: isPrivate(statement) });
            for (const member of statement.members) {
                const memberName = names.declarationName(member.name);
                if (memberName) {
                    this.declare(member, names.child(name, memberName), { Type: "FieldMember" });
                }
            }
        } else if (ts.isVariableStatement(statement)) {
            for (const declaration of statement.declarationList.declarations) {
                this.declareVariable(declaration, parent, isPrivate);
            }
        } else if (ts.isModuleDeclaration(statement) && ts.isIdentifier(statement.name)) {
            // namespace X { ... }: its declarations are nodes inside it
            const name = names.child(parent, names.encodePart(statement.name.text));
            this.declare(statement, name, { Type: "Namespace", IsPrivate: isPrivate(statement) });
            if (statement.body && ts.isModuleBlock(statement.body)) {
                for (const inner of statement.body.statements) {
                    this.declareStatement(inner, name, undefined);
                }
            }
        } else if (ts.isExportAssignment(statement) && !ts.isIdentifier(statement.expression)) {
            // "export default <expression>": a value without a name of its own
            this.declareValue(statement, statement.expression, names.child(parent, "default"), undefined);
        } else if (ts.isExpressionStatement(statement) && ts.isBinaryExpression(statement.expression)) {
            this.declareCommonJsExport(statement.expression, parent);
        }
    }

    // CommonJS, where an assignment is the declaration:
    //   exports.handler = function () {}        module.exports.handler = () => {}
    //   module.exports = { start() {}, stop: function () {}, limit: 10 }
    declareCommonJsExport(assignment: ts.BinaryExpression, parent: string): void {
        const { left, right } = assignment;
        if (assignment.operatorToken.kind !== ts.SyntaxKind.EqualsToken || !ts.isPropertyAccessExpression(left)) {
            return;
        }
        const value = skipWrappers(right);
        if (isModuleExports(left)) {
            if (!ts.isObjectLiteralExpression(value)) {
                return;
            }
            for (const property of value.properties) {
                const name = names.declarationName(property.name);
                if (!name) {
                    continue;
                }
                if (ts.isMethodDeclaration(property)) {
                    this.declare(property, names.child(parent, name), {
                        Type: "FunctionMember",
                        Signature: functionSignature(property, this.checker),
                    });
                } else if (ts.isPropertyAssignment(property) && !ts.isIdentifier(property.initializer)) {
                    this.declareValue(property, property.initializer, names.child(parent, name), undefined);
                }
            }
        } else if ((isExportsObject(left.expression) || isModuleExports(left.expression)) && !ts.isIdentifier(value)) {
            const name = names.declarationName(left.name);
            if (name) {
                this.declareValue(assignment, value, names.child(parent, name), undefined);
            }
        }
    }

    declareVariable(
        declaration: ts.VariableDeclaration,
        parent: string,
        isPrivate: (declaration: ts.Node) => true | undefined
    ): void {
        // const fs = require("fs"), const { run } = require("./runner"): an import, no variable
        if (declaration.initializer && isRequire(declaration.initializer)) {
            return;
        }
        if (ts.isIdentifier(declaration.name)) {
            this.declareValue(declaration, declaration.initializer, names.child(parent, names.encodePart(declaration.name.text)), isPrivate(declaration));
            return;
        }
        // const { a, b } = ...: each name it binds is a variable
        const bind = (pattern: ts.BindingPattern) => {
            for (const element of pattern.elements) {
                if (ts.isOmittedExpression(element)) {
                    continue;
                }
                if (ts.isIdentifier(element.name)) {
                    this.declare(element, names.child(parent, names.encodePart(element.name.text)), {
                        Type: "VariableMember",
                        IsPrivate: isPrivate(element),
                    });
                } else {
                    bind(element.name);
                }
            }
        };
        bind(declaration.name);
    }

    // A named value is what it holds: a function, a class, or else a variable.
    declareValue(declaration: ts.Node, value: ts.Expression | undefined, name: string, isPrivate: true | undefined): void {
        const initializer = value && skipWrappers(value);
        if (initializer && (ts.isArrowFunction(initializer) || ts.isFunctionExpression(initializer))) {
            this.declare(declaration, name, {
                Type: "FunctionMember",
                Signature: functionSignature(initializer, this.checker),
                IsPrivate: isPrivate,
            });
        } else if (initializer && ts.isClassExpression(initializer)) {
            this.declareClass(initializer, name, isPrivate, declaration);
        } else {
            this.declare(declaration, name, {
                Type: "VariableMember",
                Signature: ts.isVariableDeclaration(declaration) ? variableSignature(declaration, this.checker) : undefined,
                IsPrivate: isPrivate,
            });
        }
    }

    declareClass(classNode: ts.ClassLikeDeclaration, name: string, isPrivate: true | undefined, declaration?: ts.Node): void {
        this.declare(declaration ?? classNode, name, { Type: "ClassType", IsPrivate: isPrivate });
        this.setDeclared(classNode, name);

        for (const member of classNode.members) {
            const memberPrivate = isPrivateMember(member) ? true : undefined;
            if (ts.isConstructorDeclaration(member)) {
                this.declare(member, names.child(name, "constructor"), {
                    Type: "ConstructorMember",
                    Signature: functionSignature(member, this.checker),
                    IsPrivate: memberPrivate,
                });
                // constructor(private readonly store: Store): the parameter is a property too
                for (const parameter of member.parameters) {
                    if (ts.isIdentifier(parameter.name) && ts.isParameterPropertyDeclaration(parameter, member)) {
                        this.declare(parameter, names.child(name, names.encodePart(parameter.name.text)), {
                            Type: "PropertyMember",
                            Signature: variableSignature(parameter, this.checker),
                            IsPrivate: isPrivateMember(parameter) ? true : undefined,
                        });
                    }
                }
                continue;
            }

            const memberName = names.declarationName(member.name);
            if (!memberName) {
                continue;
            }
            const fullName = names.child(name, memberName);
            if (ts.isMethodDeclaration(member)) {
                this.declare(member, fullName, {
                    Type: "MethodMember",
                    Signature: functionSignature(member, this.checker),
                    IsPrivate: memberPrivate,
                });
            } else if (ts.isPropertyDeclaration(member)) {
                const initializer = member.initializer && skipWrappers(member.initializer);
                const isFunction = initializer && (ts.isArrowFunction(initializer) || ts.isFunctionExpression(initializer));
                this.declare(member, fullName, {
                    Type: isFunction ? "MethodMember" : "PropertyMember",
                    Signature: isFunction ? functionSignature(initializer, this.checker) : variableSignature(member, this.checker),
                    IsPrivate: memberPrivate,
                });
            } else if (ts.isGetAccessor(member) || ts.isSetAccessor(member)) {
                this.declare(member, fullName, {
                    Type: "PropertyMember",
                    Signature: accessorSignature(member, this.checker),
                    IsPrivate: memberPrivate,
                });
            }
        }
    }

    // The members of an interface or an object type
    declareTypeMembers(members: ts.NodeArray<ts.TypeElement>, parent: string): void {
        for (const member of members) {
            const memberName = names.declarationName(member.name);
            if (!memberName) {
                continue;
            }
            const fullName = names.child(parent, memberName);
            if (ts.isMethodSignature(member)) {
                this.declare(member, fullName, { Type: "MethodMember", Signature: functionSignature(member, this.checker) });
            } else if (ts.isPropertySignature(member)) {
                const isFunction = member.type && ts.isFunctionTypeNode(member.type);
                this.declare(member, fullName, {
                    Type: isFunction ? "MethodMember" : "PropertyMember",
                    Signature: isFunction ? functionSignature(member.type as ts.FunctionTypeNode, this.checker) : variableSignature(member, this.checker),
                });
            }
        }
    }

    // Adds the node of a declaration. A name declared more than once (overloads, an interface
    // merged from several declarations) is one node: the first declaration's.
    declare(declaration: ts.Node, name: string, properties: NodeProperties): void {
        this.setDeclared(declaration, name);
        const existing = this.nodes.get(name);
        if (existing && !(isMemberType(existing.Type) && !isMemberType(properties.Type))) {
            return;
        }
        const file = declaration.getSourceFile();
        this.addNode(name, {
            ...properties,
            Description: this.documentation(declaration),
            FileSpan: {
                Path: path.normalize(file.fileName),
                StartLine: file.getLineAndCharacterOfPosition(declaration.getStart(file)).line,
                EndLine: file.getLineAndCharacterOfPosition(declaration.getEnd()).line,
            },
        });
    }

    // The first declaration of a name is its node. Where a value and a type or namespace share
    // a name (const Options = {...} and type Options = ...), the one with members inside is: a
    // member row cannot hold the other's members.
    addNode(name: string, properties: NodeProperties): void {
        const existing = this.nodes.get(name);
        if (!existing) {
            this.nodes.set(name, properties);
        } else if (isMemberType(existing.Type) && !isMemberType(properties.Type)) {
            this.nodes.set(name, { ...properties, Description: properties.Description ?? existing.Description });
        }
    }

    setDeclared(node: ts.Node, name: string): void {
        this.declarationNodes.set(node, name);
        if (this.project.parts.length > 1) {
            this.declarationPlaces.set(placeOf(node.getSourceFile().fileName, node), name);
        }
    }

    // The node of a project's file, in whichever part's program the file object is from
    fileNode(file: ts.SourceFile): string | undefined {
        return this.declarationNodes.get(file) ?? this.declarationPlaces.get(placeOf(file.fileName, file));
    }

    // The declaration's JSDoc comment text
    documentation(declaration: ts.Node): string | undefined {
        const name = (declaration as ts.NamedDeclaration).name;
        const symbol = name ? this.checker.getSymbolAtLocation(name) : undefined;
        const text = symbol ? ts.displayPartsToString(symbol.getDocumentationComment(this.checker)).trim() : "";
        return text || undefined;
    }

    // --- Pass 2: links --------------------------------------------------------------------

    linkFile(file: ts.SourceFile): void {
        const visit = (node: ts.Node) => {
            if (ts.isImportDeclaration(node)) {
                this.linkImport(node);
                return; // The names an import brings in are links where they are used
            }
            if (ts.isImportEqualsDeclaration(node)) {
                return;
            }
            if (ts.isExportDeclaration(node)) {
                this.linkReExport(node);
                return;
            }
            if (ts.isHeritageClause(node)) {
                for (const type of node.types) {
                    this.linkReference(type.expression, type.expression, true);
                }
            } else if (ts.isCallExpression(node)) {
                this.linkModuleCall(node);
            } else if (ts.isBindingElement(node)) {
                this.linkDestructured(node);
            } else if (ts.isIdentifier(node) || ts.isPrivateIdentifier(node)) {
                if (!isDeclarationName(node)) {
                    this.linkReference(node, node, false);
                }
            }
            ts.forEachChild(node, visit);
        };
        visit(file);
    }

    // A link from the node the reference is in to the node of what it names.
    linkReference(location: ts.Node, reference: ts.Node, isInheritance: boolean): void {
        const source = this.enclosingNode(location);
        if (!source) {
            return;
        }
        const symbol = this.symbolAt(reference);
        const declaration = symbol?.valueDeclaration ?? symbol?.declarations?.[0];
        if (!declaration) {
            return;
        }
        const target = this.targetOf(declaration);
        if (target) {
            this.addLink(source, target.name, target.type, isInheritance);
        }
    }

    // const { start } = await import("./server"), = require("./server"), = options: the name
    // is a new variable, and a use of the property it takes
    linkDestructured(element: ts.BindingElement): void {
        const name = element.propertyName ?? element.name;
        if (!ts.isObjectBindingPattern(element.parent) || !ts.isIdentifier(name)) {
            return;
        }
        const source = this.enclosingNode(element.parent);
        const declaration = element.parent.parent;
        const value = ts.isVariableDeclaration(declaration) && declaration.initializer ? declaration.initializer : declaration;
        const property = this.checker.getTypeAtLocation(value).getProperty(name.text);
        const symbol = property && this.resolveAlias(property);
        const propertyDeclaration = symbol?.valueDeclaration ?? symbol?.declarations?.[0];
        const target = propertyDeclaration && this.targetOf(propertyDeclaration);
        if (source && target) {
            this.addLink(source, target.name, target.type, false);
        }
    }

    symbolAt(reference: ts.Node): ts.Symbol | undefined {
        // { store }: the value the shorthand names, not the property it makes
        const parent = reference.parent;
        const symbol =
            parent && ts.isShorthandPropertyAssignment(parent) && parent.name === reference
                ? this.checker.getShorthandAssignmentValueSymbol(parent)
                : this.checker.getSymbolAtLocation(reference);
        return symbol && this.resolveAlias(symbol);
    }

    // What an import or re-export name stands for; undefined when its module was not found.
    resolveAlias(symbol: ts.Symbol): ts.Symbol | undefined {
        if (!(symbol.flags & ts.SymbolFlags.Alias)) {
            return symbol;
        }
        try {
            const target = this.checker.getAliasedSymbol(symbol);
            return target.declarations?.length ? target : undefined;
        } catch {
            return undefined;
        }
    }

    // The node a declaration is, or is declared in: a local variable is part of its function.
    enclosingNode(node: ts.Node): string | undefined {
        // A file declared by another part is another syntax tree here: found by place
        const file = node.getSourceFile();
        const isDeclaredTree = this.declarationNodes.has(file);
        for (let current: ts.Node | undefined = node; current; current = current.parent) {
            const name = isDeclaredTree
                ? this.declarationNodes.get(current)
                : this.declarationPlaces.get(placeOf(file.fileName, current));
            if (name) {
                return name;
            }
        }
        return undefined;
    }

    targetOf(declaration: ts.Node): { name: string; type?: NodeType } | undefined {
        // A whole module (import * as vscode) is no use of anything in it: what is used of it is
        if (ts.isSourceFile(declaration) || (ts.isModuleDeclaration(declaration) && ts.isStringLiteral(declaration.name))) {
            return undefined;
        }
        const file = declaration.getSourceFile();
        if (this.fileNode(file)) {
            const name = this.enclosingNode(declaration);
            return name ? { name, type: this.nodes.get(name)?.Type } : undefined;
        }
        if (this.program.isSourceFileDefaultLibrary(file)) {
            return undefined; // Array, Promise, console, the DOM: the language's own
        }
        const packageName = this.packageOf(file.fileName);
        return packageName ? this.externalTarget(packageName, declaration) : undefined;
    }

    // The package a file that is not the project's own belongs to: the one it is installed as,
    // or, for a package of the same repository that node_modules only links to (a workspace
    // package, which the compiler resolves to its real folder), the nearest package.json's name.
    packageOf(fileName: string): string | undefined {
        const packageName = names.packageOfFile(fileName);
        if (packageName) {
            return packageName;
        }
        // Only a file found through node_modules: the project's own declaration files and
        // skipped test files are no package, whatever package.json is above them.
        const file = this.program.getSourceFile(fileName);
        return file && this.program.isSourceFileFromExternalLibrary(file)
            ? this.sourcePackageOf(path.dirname(path.normalize(fileName)))
            : undefined;
    }

    sourcePackageOf(dir: string): string | undefined {
        if (this.sourcePackages.has(dir)) {
            return this.sourcePackages.get(dir);
        }
        const projectDir = this.project.projectDir;
        let name: string | undefined;
        // Not the project's own package, nor one around it: its own files are no package to it
        if (dir !== projectDir && !projectDir.startsWith(dir + path.sep)) {
            name = readPackageName(path.join(dir, "package.json"));
            const parent = path.dirname(dir);
            if (!name && parent !== dir) {
                name = this.sourcePackageOf(parent);
            }
        }
        this.sourcePackages.set(dir, name);
        return name;
    }

    // What is used of a package: the package's exported name the declaration belongs to
    // ("vscode.window" for window.showWarningMessage), not each member of it.
    externalTarget(packageName: string, declaration: ts.Node): { name: string; type?: NodeType } {
        let topName: string | undefined;
        let topDeclaration: ts.Node | undefined;
        let ambientModule: string | undefined;
        for (let current: ts.Node | undefined = declaration; current && !ts.isSourceFile(current); current = current.parent) {
            if (ts.isModuleDeclaration(current) && ts.isStringLiteral(current.name)) {
                ambientModule = current.name.text; // declare module "fs" { ... }
                continue;
            }
            if (current.flags & ts.NodeFlags.GlobalAugmentation) {
                continue; // declare global { ... }
            }
            const name = names.declarationName((current as ts.NamedDeclaration).name);
            if (name && isNamedDeclaration(current)) {
                topName = name;
                topDeclaration = current;
            }
        }

        // Node's types declare each built-in module as an ambient module: "node.fs.readFileSync"
        const moduleName =
            packageName === "node" && ambientModule ? `node/${ambientModule.replace(/^node:/, "")}` : packageName;
        const packageNode = names.packageNodeName(moduleName);
        return topName
            ? { name: names.child(packageNode, topName), type: externalType(topDeclaration!) }
            : { name: packageNode };
    }

    // import "./setup" runs the file: a link to it. A package that is not installed has no
    // symbols to link to, so the import itself is the link to the package.
    linkImport(node: ts.ImportDeclaration): void {
        if (!ts.isStringLiteral(node.moduleSpecifier)) {
            return;
        }
        const source = this.enclosingNode(node);
        const isRunOnly = !node.importClause;
        const target = this.moduleTarget(node.moduleSpecifier, isRunOnly, isRunOnly);
        if (source && target) {
            this.addLink(source, target, undefined, false);
        }
    }

    // export * from "./a" and export { x } from "./a": the file passes on what the other declares
    linkReExport(node: ts.ExportDeclaration): void {
        if (!node.moduleSpecifier || !ts.isStringLiteral(node.moduleSpecifier)) {
            return;
        }
        const source = this.enclosingNode(node);
        if (!source) {
            return;
        }
        if (node.exportClause && ts.isNamedExports(node.exportClause)) {
            let isResolved = false;
            for (const element of node.exportClause.elements) {
                const symbol = this.symbolAt(element.propertyName ?? element.name);
                const declaration = symbol?.valueDeclaration ?? symbol?.declarations?.[0];
                const target = declaration && this.targetOf(declaration);
                if (target) {
                    this.addLink(source, target.name, target.type, false);
                    isResolved = true;
                }
            }
            if (isResolved) {
                return;
            }
        }
        const target = this.moduleTarget(node.moduleSpecifier, true, true);
        if (target) {
            this.addLink(source, target, undefined, false);
        }
    }

    // import("./page") and require("./page"): a link to the file or the package
    linkModuleCall(node: ts.CallExpression): void {
        const isImport = node.expression.kind === ts.SyntaxKind.ImportKeyword;
        const isRequire = ts.isIdentifier(node.expression) && node.expression.text === "require";
        const argument = node.arguments[0];
        if ((!isImport && !isRequire) || node.arguments.length !== 1 || !ts.isStringLiteralLike(argument)) {
            return;
        }
        const source = this.enclosingNode(node);
        const target = this.moduleTarget(argument, true, false);
        if (source && target) {
            this.addLink(source, target, undefined, false);
        }
    }

    // The node a module specifier names. A file of the project when linkFile, the package of
    // an installed module when linkPackage, and always the package of a specifier that could
    // not be resolved: it is missing, so nothing used of it can be a link.
    moduleTarget(specifier: ts.StringLiteralLike, linkFile: boolean, linkPackage: boolean): string | undefined {
        const resolvedFile = this.resolvedModuleFile(specifier);
        if (resolvedFile) {
            const file = this.program.getSourceFile(resolvedFile);
            const fileNode = file && this.fileNode(file);
            if (fileNode) {
                return linkFile ? fileNode : undefined;
            }
            const packageName = this.packageOf(resolvedFile);
            return linkPackage && packageName ? names.packageNodeName(packageName) : undefined;
        }

        // Not resolved. A path alias (of the config, or what a bundler knows as one) names a file
        // of the project that is not there: no package.
        const packageName = names.packageOfSpecifier(specifier.text);
        if (!packageName || this.isPathAlias(specifier.text)) {
            return undefined;
        }
        if (packageName.startsWith("node:") || builtinModules.includes(packageName)) {
            // One of Node's own modules, without Node's types installed
            return names.packageNodeName(`node/${specifier.text.replace(/^node:/, "")}`);
        }
        // A package that is installed, but has nothing the compiler reads at that path (a style
        // sheet or an image of it, or JavaScript without types): the import is the use of it.
        if (!this.isInstalled(packageName, path.dirname(path.normalize(specifier.getSourceFile().fileName)))) {
            this.missingPackages.add(packageName);
        }
        return names.packageNodeName(packageName);
    }

    isPathAlias(specifier: string): boolean {
        return Object.keys(this.program.getCompilerOptions().paths ?? {}).some(pattern => {
            const star = pattern.indexOf("*");
            return star < 0
                ? specifier === pattern
                : specifier.startsWith(pattern.slice(0, star)) && specifier.endsWith(pattern.slice(star + 1));
        });
    }

    // Whether a node_modules folder at or above the folder has the package.
    isInstalled(packageName: string, dir: string): boolean {
        const key = `${packageName}\n${dir}`;
        let isInstalled = this.installedPackages.get(key);
        if (isInstalled === undefined) {
            const parent = path.dirname(dir);
            isInstalled =
                fs.existsSync(path.join(dir, "node_modules", packageName)) ||
                (parent !== dir && this.isInstalled(packageName, parent));
            this.installedPackages.set(key, isInstalled);
        }
        return isInstalled;
    }

    // The file a module specifier resolves to: as the compiler resolved it (a module declared
    // by a package's types has a symbol, but no file of its own name), or else by its path.
    resolvedModuleFile(specifier: ts.StringLiteralLike): string | undefined {
        const declaration = this.checker.getSymbolAtLocation(specifier)?.declarations?.[0];
        if (declaration) {
            return declaration.getSourceFile().fileName;
        }
        return ts.resolveModuleName(
            specifier.text,
            specifier.getSourceFile().fileName,
            this.program.getCompilerOptions(),
            ts.sys,
            this.resolutionCache
        ).resolvedModule?.resolvedFileName;
    }

    addLink(source: string, target: string, targetType: NodeType | undefined, isInheritance: boolean): void {
        // No link to itself, to what it is part of, or to its own parts
        if (source === target || source.startsWith(`${target}.`) || target.startsWith(`${source}.`)) {
            return;
        }
        const key = `${source}\n${target}`;
        const link = this.links.get(key);
        if (link) {
            if (isInheritance) {
                link.Properties.IsInheritance = true;
            }
            return;
        }
        this.links.set(key, {
            Source: source,
            Target: target,
            Properties: { TargetType: targetType, IsInheritance: isInheritance ? true : undefined },
        });
    }

    // --- Problems -------------------------------------------------------------------------

    // What can leave the model incomplete, one brief summary each: the editor lists the errors.
    addProblems(): void {
        for (const { file, part } of this.ownFiles) {
            const errors = part.program
                .getSyntacticDiagnostics(file)
                .filter(d => d.category === ts.DiagnosticCategory.Error).length;
            if (errors > 0) {
                const relative = path.relative(this.project.projectDir, file.fileName).split(path.sep).join("/");
                this.problems.push({
                    Problem: {
                        Message: `${errors} syntax ${errors === 1 ? "error" : "errors"} in ${relative}`,
                        ErrorCount: errors,
                        NodeName: this.fileNodeName(file),
                    },
                });
            }
        }

        const missing = [...this.missingPackages].sort();
        if (missing.length > 0) {
            const shown = missing.slice(0, 3).join(", ") + (missing.length > 3 ? ", …" : "");
            this.problems.push({
                Problem: {
                    Message:
                        missing.length === 1
                            ? `The package ${shown} is not installed, so what is used of it is not shown (npm install)`
                            : `${missing.length} packages are not installed (${shown}), so what is used of them is not shown (npm install)`,
                    ErrorCount: missing.length,
                    NodeName: this.root,
                },
            });
        }
    }
}

// --- Helpers ------------------------------------------------------------------------------

// A declaration's place in the source: the same in every program that has the file.
function placeOf(fileName: string, node: ts.Node): string {
    return `${fileName}\n${node.pos}\n${node.end}\n${node.kind}`;
}

function isMemberType(type: NodeType): boolean {
    return type.endsWith("Member");
}

function readPackageName(packagePath: string): string | undefined {
    try {
        const name = JSON.parse(fs.readFileSync(packagePath, "utf8")).name;
        return typeof name === "string" && name !== "" ? name : undefined;
    } catch {
        return undefined;
    }
}

function isTestFile(fileName: string, projectDir: string): boolean {
    if (TestFile.test(fileName)) {
        return true;
    }
    const relative = path.relative(projectDir, path.normalize(fileName)).split(path.sep);
    return relative.slice(0, -1).some(folder => TestFolders.has(folder.toLowerCase()));
}

function isDefaultExport(node: ts.Node): boolean {
    const flags = ts.getCombinedModifierFlags(node as ts.Declaration);
    return (flags & ts.ModifierFlags.Default) !== 0;
}

function isPrivateMember(member: ts.Node): boolean {
    const name = (member as ts.NamedDeclaration).name;
    if (name && ts.isPrivateIdentifier(name)) {
        return true;
    }
    return (ts.getCombinedModifierFlags(member as ts.Declaration) & ts.ModifierFlags.Private) !== 0;
}

// require("x"), require("x").y
function isRequire(expression: ts.Expression): boolean {
    let current = skipWrappers(expression);
    while (ts.isPropertyAccessExpression(current)) {
        current = current.expression;
    }
    return ts.isCallExpression(current) && ts.isIdentifier(current.expression) && current.expression.text === "require";
}

// module.exports
function isModuleExports(node: ts.Node): boolean {
    return (
        ts.isPropertyAccessExpression(node) &&
        ts.isIdentifier(node.expression) &&
        node.expression.text === "module" &&
        node.name.text === "exports"
    );
}

// exports
function isExportsObject(node: ts.Node): boolean {
    return ts.isIdentifier(node) && node.text === "exports";
}

// The value inside "x as T", "x satisfies T", "(x)" and "x!"
function skipWrappers(expression: ts.Expression): ts.Expression {
    let current = expression;
    while (
        ts.isParenthesizedExpression(current) ||
        ts.isAsExpression(current) ||
        ts.isSatisfiesExpression(current) ||
        ts.isNonNullExpression(current) ||
        ts.isTypeAssertionExpression(current)
    ) {
        current = current.expression;
    }
    return current;
}

// The identifier is the name of the declaration it is in (function activate, { port: 1 }),
// not a use of something declared elsewhere.
function isDeclarationName(identifier: ts.Node): boolean {
    const parent = identifier.parent;
    if (!parent || ts.isPropertyAccessExpression(parent) || ts.isShorthandPropertyAssignment(parent)) {
        return false;
    }
    if (ts.isJsxAttribute(parent)) {
        return false; // <Panel title="x">: a use of the component's title
    }
    return (parent as ts.NamedDeclaration).name === identifier;
}

function isNamedDeclaration(node: ts.Node): boolean {
    return (
        ts.isFunctionDeclaration(node) ||
        ts.isClassDeclaration(node) ||
        ts.isInterfaceDeclaration(node) ||
        ts.isTypeAliasDeclaration(node) ||
        ts.isEnumDeclaration(node) ||
        ts.isVariableDeclaration(node) ||
        ts.isModuleDeclaration(node)
    );
}

function externalType(declaration: ts.Node): NodeType {
    if (ts.isFunctionDeclaration(declaration)) {
        return "FunctionMember";
    }
    if (ts.isClassDeclaration(declaration)) {
        return "ClassType";
    }
    if (ts.isInterfaceDeclaration(declaration)) {
        return "InterfaceType";
    }
    if (ts.isEnumDeclaration(declaration)) {
        return "EnumType";
    }
    if (ts.isVariableDeclaration(declaration)) {
        return "VariableMember";
    }
    if (ts.isModuleDeclaration(declaration)) {
        return "Namespace";
    }
    return "Type";
}

// The comment at the top of the file, when it describes the file: the first block of comments,
// followed by an empty line or by the imports (so it is not the first declaration's own), and
// no license header or tool directive.
function fileDescription(file: ts.SourceFile): string | undefined {
    const text = file.text;
    const ranges = ts.getLeadingCommentRanges(text, ts.getShebang(text)?.length ?? 0) ?? [];
    if (ranges.length === 0) {
        return undefined;
    }

    const lines = commentLines(text.slice(ranges[0].pos, ranges[0].end));
    let end = ranges[0].end;
    for (const range of ranges.slice(1)) {
        if (HasEmptyLine.test(text.slice(end, range.pos))) {
            break;
        }
        lines.push(...commentLines(text.slice(range.pos, range.end)));
        end = range.end;
    }

    const firstStatement = file.statements[0];
    const isFilesOwn =
        !firstStatement ||
        ts.isImportDeclaration(firstStatement) ||
        HasEmptyLine.test(text.slice(end, firstStatement.getStart(file)));
    const description = lines.join("\n").trim();
    if (!isFilesOwn || !description || /copyright|license|eslint-|@ts-|prettier-ignore/i.test(description)) {
        return undefined;
    }
    return description;
}

const HasEmptyLine = /\n[ \t]*\r?\n/;

function commentLines(comment: string): string[] {
    if (comment.startsWith("//")) {
        return [comment.replace(/^\/\/+\s?/, "")];
    }
    return comment
        .replace(/^\/\*+/, "")
        .replace(/\*+\/$/, "")
        .split(/\r?\n/)
        .map(line => line.replace(/^\s*\*\s?/, "").trimEnd())
        .filter((line, index, all) => line !== "" || (index > 0 && index < all.length - 1));
}

function commonDirectory(directories: string[]): string {
    let common = path.normalize(directories[0]).split(path.sep);
    for (const directory of directories.slice(1)) {
        const parts = path.normalize(directory).split(path.sep);
        let length = 0;
        while (length < common.length && length < parts.length && common[length] === parts[length]) {
            length++;
        }
        common = common.slice(0, length);
    }
    return common.join(path.sep) || path.sep;
}
