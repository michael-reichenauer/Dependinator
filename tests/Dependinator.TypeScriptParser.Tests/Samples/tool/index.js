// A plain JavaScript project (CommonJS, no config file): the second sample of the parser tests.

const fs = require("fs");
const { run, Runner } = require("./lib/runner");

function main(argv) {
    const text = fs.readFileSync(argv[0], "utf8");
    return run(text, new Runner());
}

function unused() {}

module.exports = { main };
