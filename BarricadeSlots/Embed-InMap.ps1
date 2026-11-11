# Embeds the BarricadeSlots content inside a map folder so the MAP ITSELF
# carries the assets. Anyone who downloads the map (workshop or manual copy)
# gets the slot objects automatically - no separate mod needed.
#
# Usage:
#   .\Embed-InMap.ps1 -MapPath "C:\...\Unturned\Maps\MyMap"
#   .\Embed-InMap.ps1 -MapPath "..." -RemoveSandboxCopy   # avoid duplicate GUIDs
param(
    [Parameter(Mandatory = $true)][string]$MapPath,
    [string]$GamePath = "C:\Program Files (x86)\Steam\Steam\steamapps\common\Unturned",
    [switch]$RemoveSandboxCopy
)

$source = Join-Path $PSScriptRoot "Content\BarricadeSlots"

if (-not (Test-Path $MapPath)) {
    Write-Error "Map folder not found: $MapPath"
    exit 1
}
if (-not (Test-Path (Join-Path $MapPath "Level.dat"))) {
    Write-Warning "No Level.dat in '$MapPath' - is this really a map folder? Continuing anyway."
}

$bundle = Join-Path $source "barricadeslots.masterbundle"
if (-not (Test-Path $bundle)) {
    Write-Warning "barricadeslots.masterbundle missing from Content\BarricadeSlots - export it from Unity first."
}

$target = Join-Path $MapPath "Bundles\BarricadeSlots"
New-Item -ItemType Directory -Force -Path $target | Out-Null
Copy-Item -Path (Join-Path $source "*") -Destination $target -Recurse -Force
Write-Host "Embedded content into $target"

# The same GUIDs must not be loaded twice (map copy + Sandbox copy).
$sandbox = Join-Path $GamePath "Sandbox\BarricadeSlots"
if (Test-Path $sandbox) {
    if ($RemoveSandboxCopy) {
        Remove-Item $sandbox -Recurse -Force
        Write-Host "Removed Sandbox copy at $sandbox"
    }
    else {
        Write-Warning "A Sandbox copy also exists at '$sandbox'."
        Write-Warning "Keeping both causes duplicate-GUID warnings. Re-run with -RemoveSandboxCopy,"
        Write-Warning "or keep the Sandbox copy only while the map is not yet embedded."
    }
}
