param(
    [string]$MissionPlannerDir = "D:\mission planner",
    [string]$ProjectPath = ".\MissionWizardPlugin\MissionWizardPlugin.csproj",
    [string]$Configuration = "Debug",
    [switch]$RestartMissionPlanner = $true
)

$ErrorActionPreference = "Stop"

function Write-Step([string]$msg) {
    Write-Host "[STEP] $msg" -ForegroundColor Cyan
}

function Write-Ok([string]$msg) {
    Write-Host "[OK] $msg" -ForegroundColor Green
}

function Write-Warn([string]$msg) {
    Write-Host "[WARN] $msg" -ForegroundColor Yellow
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptDir

$resolvedProject = Resolve-Path $ProjectPath
$mpExe = Join-Path $MissionPlannerDir "MissionPlanner.exe"
$pluginsDir = Join-Path $MissionPlannerDir "plugins"

if (-not (Test-Path $mpExe)) {
    throw "MissionPlanner.exe not found at: $mpExe"
}
if (-not (Test-Path $pluginsDir)) {
    throw "Plugins directory not found at: $pluginsDir"
}

$runningMp = Get-Process | Where-Object { $_.ProcessName -like "MissionPlanner*" } | Select-Object -First 1
if ($runningMp -and $RestartMissionPlanner) {
    Write-Step "Stopping Mission Planner before deploy"
    Get-Process | Where-Object { $_.ProcessName -like "MissionPlanner*" } | Stop-Process -Force
    Start-Sleep -Seconds 1
    Write-Ok "Mission Planner stopped"
}

Write-Step "Building plugin project"
$buildCmd = "dotnet build `"$resolvedProject`" -c $Configuration -p:MissionPlannerDir=`"$MissionPlannerDir`""
Invoke-Expression $buildCmd
if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE. Deployment aborted."
}

$outputDll = Join-Path (Split-Path $resolvedProject -Parent) "bin\$Configuration\net48\MissionWizardPlugin.dll"
if (-not (Test-Path $outputDll)) {
    throw "Build completed but plugin DLL not found: $outputDll"
}
Write-Ok "Build output found: $outputDll"

$targetDll = Join-Path $pluginsDir "MissionWizardPlugin.dll"
if (Test-Path $targetDll) {
    $backup = "$targetDll.bak_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
    Copy-Item $targetDll $backup -Force
    Write-Ok "Backup created: $backup"
}

Write-Step "Copying plugin DLL to Mission Planner plugins"
Copy-Item $outputDll $targetDll -Force
Write-Ok "Plugin deployed: $targetDll"

if ($RestartMissionPlanner) {
    Write-Step "Restarting Mission Planner"
    Start-Process -FilePath $mpExe -WorkingDirectory $MissionPlannerDir
    Write-Ok "Mission Planner started"
}
else {
    Write-Warn "Restart skipped by parameter"
}

Write-Host ""
Write-Ok "Done. Deployed and verified successfully."
