import { ModelBuilder } from "./model";
import { loadProject } from "./project";

// The TypeScript and JavaScript parser of Dependinator: a console program that parses the
// project of a tsconfig.json, jsconfig.json or package.json with the TypeScript compiler and
// prints the model items (see items.ts) as one JSON array to stdout. Dependinator.TypeScript
// runs it with Node and reads the items.
//
//   node ts-parser.js <path to tsconfig.json | jsconfig.json | package.json> [--include-tests]
//
// Log lines and errors go to stderr; a failure exits with code 1 and its message as the last
// line of stderr.
function main(): void {
    const args = process.argv.slice(2);
    const entryPath = args.find(arg => !arg.startsWith("--"));
    if (!entryPath) {
        throw new Error("Usage: ts-parser <tsconfig.json | jsconfig.json | package.json> [--include-tests]");
    }

    const start = Date.now();
    const project = loadProject(entryPath);
    const items = new ModelBuilder(project, { includeTests: args.includes("--include-tests") }).build();

    const nodes = items.filter(item => item.Node).length;
    const links = items.filter(item => item.Link).length;
    console.error(`Parsed ${entryPath}: ${nodes} nodes, ${links} links in ${Date.now() - start} ms`);

    // One item per line: readable, and written in pieces so a large model is no single huge string
    const lines = items.map((item, index) => JSON.stringify(item) + (index < items.length - 1 ? "," : ""));
    process.stdout.write(`[\n${lines.join("\n")}\n]\n`);
}

try {
    main();
} catch (error) {
    console.error(error instanceof Error ? error.message : String(error));
    process.exitCode = 1;
}
