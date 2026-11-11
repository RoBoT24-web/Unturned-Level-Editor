# Installs the BarricadeSlots content into Unturned's Sandbox folder so the
# objects appear in the level editor. Run again after re-exporting the bundle.
param(
    [string]$GamePath = "C:\Program Files (x86)\Steam\Steam\steamapps\common\Unturned"
)

$source = Join-Path $PSScriptRoot "Content\BarricadeSlots"
$target = Join-Path $GamePath "Sandbox\BarricadeSlots"

if (-not (Test-Path (Join-Path $GamePath "Unturned.exe"))) {
    Write-Error "Unturned.exe not found in '$GamePath'. Pass -GamePath 'C:\path\to\Unturned'."
    exit 1
}

$bundle = Join-Path $source "barricadeslots.masterbundle"
if (-not (Test-Path $bundle)) {
    Write-Warning "barricadeslots.masterbundle not found in Content\BarricadeSlots."
    Write-Warning "Export it from Unity first (Tools > Barricade Slots > 2. Export Master Bundle...)."
    Write-Warning "Installing anyway so you can verify the folder layout."
}

New-Item -ItemType Directory -Force -Path $target | Out-Null
Copy-Item -Path (Join-Path $source "*") -Destination $target -Recurse -Force
Write-Host "Installed to $target"
Get-ChildItem $target -Recurse -File | ForEach-Object { Write-Host "  $($_.FullName.Substring($target.Length + 1))" }
