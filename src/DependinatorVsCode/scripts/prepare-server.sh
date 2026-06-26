#!/usr/bin/env bash
set -euo pipefail

# Publishes the LSP server into DependinatorVsCode/server/<rid>/ so the packaged
# extension can launch the runtime matching the machine it executes on
# (languageServer.ts resolves server/<rid>/ from process.platform/process.arch).
#
# Which RIDs are built depends on how the script is invoked:
#   * --target <vscode-target> / VSCODE_TARGET : one RID (Marketplace per-target package)
#   * --rid <dotnet-rid>                        : one explicit RID
#   * --all / DEPENDINATOR_ALL_SERVERS=1        : all RIDs (universal sideload package)
#   * otherwise                                 : the host RID only (fast local/dev builds)

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/../.." && pwd)"
server_project="$repo_root/Dependinator.Lsp/Dependinator.Lsp.csproj"
target_root="$repo_root/DependinatorVsCode/server"

# RIDs bundled into the universal sideload package. Deliberately excludes
# win-arm64 and osx-x64 to keep the universal .vsix smaller; those platforms
# are still served via their per-target Marketplace packages (package:<target>).
all_rids=(linux-x64 linux-arm64 win-x64 osx-arm64)

target="${VSCODE_TARGET:-}"
rid=""
build_all="${DEPENDINATOR_ALL_SERVERS:-}"

while [ $# -gt 0 ]; do
    case "$1" in
        --target)
            target="${2:-}"
            shift 2
            ;;
        --rid)
            rid="${2:-}"
            shift 2
            ;;
        --all)
            build_all="1"
            shift
            ;;
        *)
            echo "Unknown argument: $1"
            exit 1
            ;;
    esac
done

# Maps a VS Code target (e.g. darwin-arm64) to its dotnet RID (e.g. osx-arm64).
target_to_rid() {
    case "$1" in
        linux-x64) echo "linux-x64" ;;
        linux-arm64) echo "linux-arm64" ;;
        win32-x64) echo "win-x64" ;;
        win32-arm64) echo "win-arm64" ;;
        darwin-x64) echo "osx-x64" ;;
        darwin-arm64) echo "osx-arm64" ;;
        *)
            echo "Unsupported VS Code target: $1" >&2
            exit 1
            ;;
    esac
}

# Resolves the dotnet RID for the current host from uname.
host_rid() {
    local uname_s uname_m os arch
    uname_s="$(uname -s)"
    uname_m="$(uname -m)"
    case "$uname_s" in
        Linux*) os="linux" ;;
        Darwin*) os="osx" ;;
        MINGW*|MSYS*|CYGWIN*|Windows_NT) os="win" ;;
        *)
            echo "Unsupported host OS: $uname_s" >&2
            exit 1
            ;;
    esac
    case "$uname_m" in
        x86_64|amd64) arch="x64" ;;
        arm64|aarch64) arch="arm64" ;;
        *)
            echo "Unsupported host architecture: $uname_m" >&2
            exit 1
            ;;
    esac
    echo "${os}-${arch}"
}

# Publishes one self-contained server into server/<rid>/ and verifies the
# Roslyn build host files MSBuildWorkspace needs are present.
publish_rid() {
    local rid="$1"
    local target_dir="$target_root/$rid"
    mkdir -p "$target_dir"
    echo "Publishing LSP server for $rid -> $target_dir"
    dotnet publish "$server_project" -c Release -r "$rid" --self-contained true -o "$target_dir"

    local build_host_dll="$target_dir/BuildHost-netcore/Microsoft.CodeAnalysis.Workspaces.MSBuild.BuildHost.dll"
    if [ ! -f "$build_host_dll" ]; then
        echo "Expected Roslyn build host file is missing: $build_host_dll"
        exit 1
    fi
}

# Decide which RIDs to build.
rids=()
if [ -n "$build_all" ]; then
    rids=("${all_rids[@]}")
elif [ -n "$rid" ]; then
    rids=("$rid")
elif [ -n "$target" ]; then
    rids=("$(target_to_rid "$target")")
else
    rids=("$(host_rid)")
fi

# Rebuild the server tree from scratch so stale RIDs are never packaged.
rm -rf "$target_root"
mkdir -p "$target_root"

for rid in "${rids[@]}"; do
    publish_rid "$rid"
done
