param(
    [string]$OutputPath = "",
    [switch]$IncludeArtifacts
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repoRoot "artifacts\SourceWorker-source.zip"
}

$resolvedOutputDirectory = Split-Path -Parent $OutputPath
if ([string]::IsNullOrWhiteSpace($resolvedOutputDirectory)) {
    $resolvedOutputDirectory = $repoRoot
}

New-Item -ItemType Directory -Force -Path $resolvedOutputDirectory | Out-Null

$excludeSegments = @(
    ".git",
    ".vs",
    ".idea",
    "bin",
    "obj",
    "node_modules",
    "SourceWorker.Desktop.exe.WebView2"
)

if (-not $IncludeArtifacts) {
    $excludeSegments += "artifacts"
}

$excludeFiles = @(
    "*.user",
    "*.suo",
    "*.db",
    "*.db-shm",
    "*.db-wal",
    "*.log",
    "*.tmp"
)

$repoRootPath = $repoRoot.Path.TrimEnd('\')
$files = Get-ChildItem -LiteralPath $repoRootPath -Recurse -File -Force |
    Where-Object {
        $relativePath = $_.FullName.Substring($repoRootPath.Length).TrimStart('\')
        $normalized = $relativePath -replace '\\', '/'
        $segments = $normalized.Split('/', [StringSplitOptions]::RemoveEmptyEntries)

        foreach ($segment in $excludeSegments) {
            if ($segments -contains $segment) {
                return $false
            }
        }

        foreach ($pattern in $excludeFiles) {
            if ($_.Name -like $pattern) {
                return $false
            }
        }

        return $true
    }

if (Test-Path -LiteralPath $OutputPath) {
    Remove-Item -LiteralPath $OutputPath -Force
}

$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("sourceworker-archive-" + [System.Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $tempRoot | Out-Null

try {
    foreach ($file in $files) {
        $relativePath = $file.FullName.Substring($repoRootPath.Length).TrimStart('\')
        $targetPath = Join-Path $tempRoot $relativePath
        $targetDirectory = Split-Path -Parent $targetPath
        New-Item -ItemType Directory -Force -Path $targetDirectory | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $targetPath -Force
    }

    Compress-Archive -Path (Join-Path $tempRoot "*") -DestinationPath $OutputPath -Force
}
finally {
    if (Test-Path -LiteralPath $tempRoot) {
        Remove-Item -LiteralPath $tempRoot -Recurse -Force
    }
}

Write-Host "Source archive created:"
Write-Host (Resolve-Path $OutputPath)
