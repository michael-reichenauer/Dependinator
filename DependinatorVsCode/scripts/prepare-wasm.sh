#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/../.." && pwd)"
wasm_project="$repo_root/DependinatorWasm/DependinatorWasm.csproj"
publish_root="$repo_root/DependinatorVsCode/.publish"
publish_dir="$publish_root/wwwroot"
target_dir="$repo_root/DependinatorVsCode/media"

dotnet publish "$wasm_project" -c Release -p:PublishDir="$publish_root/"

if [ -d "$target_dir" ]; then
    rm -rf "$target_dir"
fi
mkdir -p "$target_dir"
touch "$target_dir/.gitkeep"
cp -R "$publish_dir"/. "$target_dir"
