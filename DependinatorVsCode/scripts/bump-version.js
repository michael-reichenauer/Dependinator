const fs = require("fs");
const path = require("path");

const rootDir = path.resolve(__dirname, "..");
const packageJsonPath = path.join(rootDir, "package.json");
const packageLockPath = path.join(rootDir, "package-lock.json");

function bumpPatch(version) {
    const parts = version.split(".");
    if (parts.length < 3) {
        throw new Error(`Unsupported version format: ${version}`);
    }

    const patch = Number(parts[2]);
    if (!Number.isInteger(patch)) {
        throw new Error(`Patch version is not an integer: ${version}`);
    }

    parts[2] = String(patch + 1);
    return parts.slice(0, 3).join(".");
}

function writeJson(filePath, data) {
    const json = JSON.stringify(data, null, 4);
    fs.writeFileSync(filePath, `${json}\n`, "utf8");
}

const packageJson = JSON.parse(fs.readFileSync(packageJsonPath, "utf8"));
const nextVersion = bumpPatch(packageJson.version);

packageJson.version = nextVersion;
writeJson(packageJsonPath, packageJson);

if (fs.existsSync(packageLockPath)) {
    const packageLock = JSON.parse(fs.readFileSync(packageLockPath, "utf8"));
    packageLock.version = nextVersion;

    if (packageLock.packages && packageLock.packages[""]) {
        packageLock.packages[""].version = nextVersion;
    }

    writeJson(packageLockPath, packageLock);
}

console.log(`Bumped extension version to ${nextVersion}`);
