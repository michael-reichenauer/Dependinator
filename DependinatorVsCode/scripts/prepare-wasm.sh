#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/../.." && pwd)"
wasm_project="$repo_root/DependinatorWasm/DependinatorWasm.csproj"
publish_root="$repo_root/DependinatorVsCode/.publish"
publish_dir="$publish_root/wwwroot"
target_dir="$repo_root/DependinatorVsCode/media"

enable_aot=false
if [ "${1:-}" = "--aot" ]; then
    enable_aot=true
fi

publish_args=(
    "-c" "Release"
    "-p:PublishDir=$publish_root/"
)

if [ "$enable_aot" = false ]; then
    publish_args+=("-p:RunAOTCompilation=")
    publish_args+=(
        "-p:WasmGenerateCompressedArtifacts=false"
        "-p:WasmBuildNative=false"
    )
fi

dotnet publish "$wasm_project" "${publish_args[@]}"

if [ -d "$target_dir" ]; then
    rm -rf "$target_dir"
fi
mkdir -p "$target_dir"
touch "$target_dir/.gitkeep"
cp -R "$publish_dir"/. "$target_dir"
