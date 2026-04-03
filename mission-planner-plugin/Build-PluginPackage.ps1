param(
    [string]$ProjectPath = ".\MissionWizardPlugin\MissionWizardPlugin.csproj",
    [string]$Configuration = "Release",
    [string]$Version = "0.1.0"
)

$ErrorActionPreference = "Stop"

function Write-Step([string]$msg) {
    Write-Host "[STEP] $msg" -ForegroundColor Cyan
}

function Write-Ok([string]$msg) {
    Write-Host "[OK] $msg" -ForegroundColor Green
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptDir

$resolvedProject = Resolve-Path $ProjectPath
$projectDir = Split-Path $resolvedProject -Parent

Write-Step "Building plugin ($Configuration)"
dotnet build "$resolvedProject" -c $Configuration
if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE"
}

$outputDll = Join-Path $projectDir "bin\$Configuration\net48\MissionWizardPlugin.dll"
if (-not (Test-Path $outputDll)) {
    throw "Plugin DLL not found: $outputDll"
}

$distRoot = Join-Path $scriptDir "dist"
$packageName = "MissionWizardPlugin-v$Version"
$packageDir = Join-Path $distRoot $packageName
$zipPath = Join-Path $distRoot "$packageName.zip"

if (Test-Path $packageDir) {
    Remove-Item $packageDir -Recurse -Force
}
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

New-Item -ItemType Directory -Path $packageDir -Force | Out-Null
Copy-Item $outputDll (Join-Path $packageDir "MissionWizardPlugin.dll") -Force

$installText = @"
MissionWizardPlugin package

Manual install:
1. Close Mission Planner.
2. Copy MissionWizardPlugin.dll to Mission Planner plugins folder:
   D:\mission planner\plugins
3. Start Mission Planner.
4. Open Flight Planner map context menu and run ""Mайстер місії"".

Package contents:
- MissionWizardPlugin.dll
"@

$installPath = Join-Path $packageDir "INSTALL.txt"
Set-Content -Path $installPath -Value $installText -Encoding UTF8

Write-Step "Creating zip package"
Compress-Archive -Path "$packageDir\*" -DestinationPath $zipPath -Force

Write-Ok "Package folder: $packageDir"
Write-Ok "Zip package:   $zipPath"
