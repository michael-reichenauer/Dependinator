// Bundles the parser into one script (dist/ts-parser.js): its own code, the TypeScript compiler
// and the compiler's library declarations (lib.*.d.ts: Array, Promise, the DOM ...), which the
// compiler otherwise reads from files beside itself. See src/libraries.ts.
import * as esbuild from "esbuild";
import * as fs from "node:fs";
import * as path from "node:path";

const libraryFolder = path.join("node_modules", "typescript", "lib");
const libraries = Object.fromEntries(
    fs
        .readdirSync(libraryFolder)
        .filter(name => /^lib\..*\.d\.ts$/.test(name) || name === "lib.d.ts")
        .map(name => [name, fs.readFileSync(path.join(libraryFolder, name), "utf8")])
);

await esbuild.build({
    entryPoints: ["src/main.ts"],
    bundle: true,
    platform: "node",
    target: "node18",
    minify: true,
    legalComments: "none",
    outfile: "dist/ts-parser.js",
    plugins: [
        {
            name: "typescript-libraries",
            setup(build) {
                build.onResolve({ filter: /^virtual:typescript-libraries$/ }, args => ({
                    path: args.path,
                    namespace: "typescript-libraries",
                }));
                build.onLoad({ filter: /.*/, namespace: "typescript-libraries" }, () => ({
                    contents: JSON.stringify(libraries),
                    loader: "json",
                }));
            },
        },
    ],
});
