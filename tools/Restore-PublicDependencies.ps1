param([string] $Destination)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
if (-not $Destination) { $Destination = Join-Path $root '.dependencies' }
New-Item -ItemType Directory -Force $Destination | Out-Null

function Get-PinnedSource($name, $url, $commit) {
    $path = Join-Path $Destination $name
    if (-not (Test-Path $path)) {
        git clone $url $path
        if ($LASTEXITCODE) { throw "Clone failed: $name" }
    }
    $dirty = @(git -C $path status --porcelain)
    if ($LASTEXITCODE -or $dirty.Count) { throw "Dependency checkout is not clean: $path" }
    git -C $path checkout --detach $commit
    if ($LASTEXITCODE) { throw "Checkout failed: $name" }
}

Get-PinnedSource 'LwXLab.Markdown' 'https://github.com/lwxlabs/LwXLab.Razor.Markdown.git' '03ed9ce1e0eb40b8fd8f21839d9590756d74cc75'
Get-PinnedSource 'Localite' 'https://github.com/lwxlabs/Localite.git' 'a8c63bd0d06dd52e8c091ca6972a6f5feb56ab91'
