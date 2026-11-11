$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$module = Join-Path $root 'LevelEditorPlus\src\LevelEditorPlus\LevelEditorPlus.csproj'

dotnet build $module -c Release -p:InstallModuleOnBuild=false -v minimal
if ($LASTEXITCODE -ne 0) { throw 'Module build failed.' }

dotnet build (Join-Path $PSScriptRoot 'LevelEditorPlus.Setup.csproj') -c Release -v minimal
if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }

$source = Join-Path $PSScriptRoot 'bin\Release\LevelEditorPlus-Setup.exe'
$output = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Force -Path $output | Out-Null
Copy-Item -LiteralPath $source -Destination (Join-Path $output 'LevelEditorPlus-Setup.exe') -Force
Write-Host "Send this single file: $(Join-Path $output 'LevelEditorPlus-Setup.exe')"
