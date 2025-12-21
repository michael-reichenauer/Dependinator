$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Resolve-Path (Join-Path $scriptDir "../..")).Path
$wasmProject = Join-Path $repoRoot "DependinatorWasm/DependinatorWasm.csproj"
$publishRoot = Join-Path $repoRoot "DependinatorVsCode/.publish"
$publishDir = Join-Path $publishRoot "wwwroot"
$targetDir = Join-Path $repoRoot "DependinatorVsCode/media"

dotnet publish $wasmProject -c Release -p:PublishDir="$publishRoot/" | Out-Host

if (Test-Path $targetDir) {
    Remove-Item $targetDir -Recurse -Force
}
New-Item -ItemType Directory -Path $targetDir | Out-Null
New-Item -ItemType File -Path (Join-Path $targetDir ".gitkeep") -Force | Out-Null
Copy-Item (Join-Path $publishDir "*") $targetDir -Recurse -Force
