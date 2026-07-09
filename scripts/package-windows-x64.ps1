# SPDX-License-Identifier: MPL-2.0
param(
    [string]$Configuration = "Release",
    [string]$Version = "1.2.0"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot "src\VisualJson.App\VisualJson.App.vbproj"
$artifactsRoot = Join-Path $repoRoot "artifacts"
$publishRoot = Join-Path $repoRoot "artifacts\publish\VisualJson-win-x64"
$zipPath = Join-Path $repoRoot "artifacts\visual-json-v$Version-win-x64.zip"
$shaPath = Join-Path $repoRoot "artifacts\visual-json-v$Version-win-x64.sha256"

function Assert-UnderArtifacts {
    param([string]$Path)

    $resolvedArtifacts = [System.IO.Path]::GetFullPath($artifactsRoot)
    $resolvedTarget = [System.IO.Path]::GetFullPath($Path)
    if (-not $resolvedTarget.StartsWith($resolvedArtifacts, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to modify path outside artifacts: $resolvedTarget"
    }
}

Assert-UnderArtifacts $publishRoot
Assert-UnderArtifacts $zipPath
Assert-UnderArtifacts $shaPath

if (Test-Path -LiteralPath $publishRoot) {
    Remove-Item -LiteralPath $publishRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null

dotnet publish $project `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    --output $publishRoot

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$exePath = Join-Path $publishRoot "VisualJson.App.exe"
if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Publish output is missing VisualJson.App.exe"
}

$publicDocs = @(
    "README.md",
    "README.ja.md",
    "LICENSE",
    "NOTICE.md",
    "THIRD_PARTY_NOTICES.md",
    "CHANGELOG.md",
    "RELEASE_NOTES.md"
)

foreach ($doc in $publicDocs) {
    $source = Join-Path $repoRoot $doc
    if (-not (Test-Path -LiteralPath $source)) {
        throw "Required release document is missing: $source"
    }

    Copy-Item -LiteralPath $source -Destination $publishRoot
}

if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

if (Test-Path -LiteralPath $shaPath) {
    Remove-Item -LiteralPath $shaPath -Force
}

for ($attempt = 1; $attempt -le 3; $attempt++) {
    try {
        Compress-Archive -Path (Join-Path $publishRoot "*") -DestinationPath $zipPath -ErrorAction Stop
        break
    }
    catch {
        if ($attempt -eq 3) {
            throw
        }

        Start-Sleep -Seconds 2
    }
}

$hash = Get-FileHash -LiteralPath $zipPath -Algorithm SHA256
"$($hash.Hash)  $(Split-Path -Leaf $zipPath)" | Set-Content -LiteralPath $shaPath -Encoding ASCII
Write-Host "Created $zipPath"
Write-Host "Created $shaPath"
