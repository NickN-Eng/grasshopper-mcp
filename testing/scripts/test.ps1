# Grasshopper MCP Test Runner
# Uses GrasshopperDeveloperSettings approach (NO environment variables!)
# Opens default test file: gh_scripts/gh_mcp_load.gh

param(
    [string]$GrasshopperFile = "gh_scripts\gh_mcp_load.gh",
    [string]$Configuration = "Debug",  # Default to Debug for development
    [switch]$SkipBuild,
    [switch]$UsePlayer,      # Use GrasshopperPlayer (headless)
    [switch]$KeepOpen,       # Keep Rhino open (default: auto-close after 30s)
    [int]$TimeoutSeconds = 30
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $PSScriptRoot
$LogsDir = Join-Path $RepoRoot "logs"
$Timestamp = Get-Date -Format "yyyyMMdd-HHmmss"

if (-not (Test-Path $LogsDir)) {
    New-Item -ItemType Directory -Path $LogsDir | Out-Null
}

$BuildLog = Join-Path $LogsDir "build-$Timestamp.log"

Write-Host "================================================================" -ForegroundColor Cyan
Write-Host "  Grasshopper MCP - Test Runner" -ForegroundColor Cyan
Write-Host "================================================================" -ForegroundColor Cyan
Write-Host ""

# ============================================================================
# Build
# ============================================================================
if (-not $SkipBuild) {
    Write-Host "[1/3] Building solution..." -ForegroundColor Green
    $SolutionPath = Join-Path $RepoRoot "GH_MCP\GH_MCP.sln"

    $buildOutput = & dotnet build $SolutionPath -c $Configuration --nologo 2>&1
    $buildOutput | Out-File -FilePath $BuildLog -Encoding UTF8

    if ($LASTEXITCODE -ne 0) {
        Write-Host "[ERROR] Build FAILED" -ForegroundColor Red
        Get-Content $BuildLog -Tail 20 | Write-Host -ForegroundColor Red
        exit 1
    }
    Write-Host "[OK] Build succeeded ($Configuration)" -ForegroundColor Green
} else {
    Write-Host "[1/3] Skipping build" -ForegroundColor Yellow
}

# ============================================================================
# Check GrasshopperDeveloperSettings
# ============================================================================
Write-Host ""
Write-Host "[2/3] Checking plugin setup..." -ForegroundColor Green

$PluginDir = Join-Path $RepoRoot "GH_MCP\GH_MCP\bin\$Configuration\net48"
$GhaFile = Join-Path $PluginDir "GH_MCP.gha"

if (-not (Test-Path $GhaFile)) {
    Write-Host "[ERROR] Plugin not found: $GhaFile" -ForegroundColor Red
    exit 1
}

Write-Host "[OK] Plugin built: $GhaFile" -ForegroundColor Green
Write-Host ""
Write-Host "IMPORTANT: Plugin will be loaded via GrasshopperDeveloperSettings" -ForegroundColor Yellow
Write-Host ""
Write-Host "One-time setup (if not done already):" -ForegroundColor Cyan
Write-Host "  1. Open Grasshopper in Rhino" -ForegroundColor White
Write-Host "  2. Run command: GrasshopperDeveloperSettings" -ForegroundColor White
Write-Host "  3. Click 'Add folder' and select:" -ForegroundColor White
Write-Host "     $PluginDir" -ForegroundColor Gray
Write-Host "  4. Restart Grasshopper" -ForegroundColor White
Write-Host ""
Write-Host "Current build will be loaded from your configured developer folders." -ForegroundColor Yellow
Write-Host "Press Enter to continue or Ctrl+C to cancel..." -ForegroundColor Yellow
$null = Read-Host

# ============================================================================
# Launch Rhino + Grasshopper
# ============================================================================
Write-Host ""
Write-Host "[3/3] Launching Rhino + Grasshopper..." -ForegroundColor Green

# Resolve Grasshopper file path
if ([System.IO.Path]::IsPathRooted($GrasshopperFile)) {
    $GhFileAbsolute = $GrasshopperFile
} else {
    $GhFileAbsolute = Join-Path $RepoRoot $GrasshopperFile
}

if (-not (Test-Path $GhFileAbsolute)) {
    Write-Host "[ERROR] Grasshopper file not found: $GhFileAbsolute" -ForegroundColor Red
    exit 1
}

Write-Host "  Test file:  $GhFileAbsolute" -ForegroundColor Gray
Write-Host "  Mode:       $(if ($UsePlayer) { 'Grasshopper Player (headless)' } else { 'Grasshopper Editor' })" -ForegroundColor Gray
Write-Host ""

# Build Rhino runscript
if ($UsePlayer) {
    $RunScript = "-GrasshopperPlayer `"$GhFileAbsolute`""
    if (-not $KeepOpen) {
        $RunScript += " _Exit"
    }
} else {
    $RunScript = "-Grasshopper Editor Load Document Open `"$GhFileAbsolute`" _Enter"
    if (-not $KeepOpen) {
        # Auto-close after timeout
        $RunScript += " _Pause $TimeoutSeconds _Exit"
    }
}

# Launch Rhino
$RhinoExe = "C:\Program Files\Rhino 8\System\Rhino.exe"
if (-not (Test-Path $RhinoExe)) {
    Write-Host "[ERROR] Rhino not found at: $RhinoExe" -ForegroundColor Red
    exit 1
}

$RhinoArgs = @(
    "/nosplash",
    "/notemplate",
    "/netfx",
    "/runscript=`"$RunScript`""
)

try {
    Write-Host "Launching Rhino..." -ForegroundColor Yellow

    $process = Start-Process -FilePath $RhinoExe -ArgumentList $RhinoArgs -PassThru

    Write-Host "[OK] Rhino launched (PID: $($process.Id))" -ForegroundColor Green
    Write-Host ""
    Write-Host "================================================================" -ForegroundColor Cyan

    if ($KeepOpen) {
        Write-Host "  Rhino is running - close manually when done" -ForegroundColor Yellow
        Write-Host "  Process ID: $($process.Id)" -ForegroundColor Gray
    } else {
        Write-Host "  Rhino will auto-close after $TimeoutSeconds seconds" -ForegroundColor Yellow
        Write-Host "  (Use -KeepOpen to prevent auto-close)" -ForegroundColor Gray
    }

    Write-Host "================================================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Plugin loaded from: GrasshopperDeveloperSettings" -ForegroundColor White
    Write-Host "Build directory:    $PluginDir" -ForegroundColor White
    Write-Host "Test file:          $GhFileAbsolute" -ForegroundColor White
    Write-Host ""
    Write-Host "NO environment variables used!" -ForegroundColor Green
    Write-Host "NO conflicts with manual settings!" -ForegroundColor Green
    Write-Host ""

    exit 0
}
catch {
    Write-Host "[ERROR] Failed to launch Rhino: $_" -ForegroundColor Red
    exit 1
}
