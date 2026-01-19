#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/../.." && pwd)"
server_project="$repo_root/DependinatorLanguageServer/DependinatorLanguageServer.csproj"
target_dir="$repo_root/DependinatorVsCode/server"

if [ -d "$target_dir" ]; then
    rm -rf "$target_dir"
fi

dotnet publish "$server_project" -c Release -o "$target_dir"
