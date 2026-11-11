# Creates a collection folder for Unturned's Workshop > Submit screen.
$ErrorActionPreference = 'Stop'
$source = Join-Path $PSScriptRoot 'Content\BarricadeSlots'
$collection = Join-Path $PSScriptRoot 'WorkshopUpload'
$destination = Join-Path $collection 'BarricadeSlots'

$required = @(
    'MasterBundle.dat',
    'barricadeslots.masterbundle',
    'Objects\Slot_Door\Slot_Door.dat',
    'Objects\Slot_Gate\Slot_Gate.dat',
    'Objects\Slot_Window\Slot_Window.dat',
    'Objects\Slot_Hatch\Slot_Hatch.dat'
)
foreach ($relative in $required) {
    $file = Join-Path $source $relative
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
        throw "Missing asset: $file. Export the bundle from Unity before preparing an upload."
    }
    if ((Get-Item -LiteralPath $file).Length -eq 0) {
        throw "Asset is empty: $file"
    }
}

# Replace only this script's generated asset folder so removed files cannot linger.
if (Test-Path -LiteralPath $destination) {
    Remove-Item -LiteralPath $destination -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $destination | Out-Null
Get-ChildItem -LiteralPath $source -Recurse -File |
    Where-Object { $_.Extension -ne '.manifest' } |
    ForEach-Object {
        $relative = $_.FullName.Substring($source.Length + 1)
        $target = Join-Path $destination $relative
        New-Item -ItemType Directory -Force -Path (Split-Path $target -Parent) | Out-Null
        Copy-Item -LiteralPath $_.FullName -Destination $target -Force
    }

Write-Host "Workshop Collection Path: $collection"
Write-Host 'Use a separate PNG/JPG screenshot as the Preview Image.'
Write-Host 'Upload as Ready-to-Use / Object. Start with Private visibility for testing.'
