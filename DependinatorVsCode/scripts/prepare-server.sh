#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/../.." && pwd)"
server_project="$repo_root/DependinatorLanguageServer/DependinatorLanguageServer.csproj"

target="${VSCODE_TARGET:-}"
rid=""

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
        *)
            echo "Unknown argument: $1"
            exit 1
            ;;
    esac
done

if [ -z "$rid" ]; then
    if [ -n "$target" ]; then
        case "$target" in
            linux-x64) rid="linux-x64" ;;
            linux-arm64) rid="linux-arm64" ;;
            win32-x64) rid="win-x64" ;;
            win32-arm64) rid="win-arm64" ;;
            darwin-x64) rid="osx-x64" ;;
            darwin-arm64) rid="osx-arm64" ;;
            *)
                echo "Unsupported VS Code target: $target"
                exit 1
                ;;
        esac
    else
        uname_s="$(uname -s)"
        uname_m="$(uname -m)"
        case "$uname_s" in
            Linux*) os="linux" ;;
            Darwin*) os="osx" ;;
            MINGW*|MSYS*|CYGWIN*|Windows_NT) os="win" ;;
            *)
                echo "Unsupported host OS: $uname_s"
                exit 1
                ;;
        esac
        case "$uname_m" in
            x86_64|amd64) arch="x64" ;;
            arm64|aarch64) arch="arm64" ;;
            *)
                echo "Unsupported host architecture: $uname_m"
                exit 1
                ;;
        esac
        rid="${os}-${arch}"
    fi
fi

target_root="$repo_root/DependinatorVsCode/server"
target_dir="$target_root/$rid"

if [ -d "$target_root" ]; then
    rm -rf "$target_root"
fi
mkdir -p "$target_dir"

dotnet publish "$server_project" -c Release -r "$rid" --self-contained true -p:PublishSingleFile=true -o "$target_dir"
