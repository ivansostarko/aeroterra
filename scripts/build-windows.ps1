# =============================================================================
# AeroTerra — build the Windows player, run natively from Windows PowerShell.
# Use this instead of build-windows.sh when the Unity Editor is installed on
# the Windows machine itself (see docs/07-WINDOWS-SETUP.md).
#
# Usage (PowerShell):
#   .\scripts\build-windows.ps1
#   .\scripts\build-windows.ps1 -Unity "C:\Program Files\Unity\Hub\Editor\2022.3.50f1\Editor\Unity.exe"
# =============================================================================
param(
    [string]$Unity = ""
)

$Project = Split-Path -Parent $PSScriptRoot

if ([string]::IsNullOrEmpty($Unity)) {
    $candidates = Get-ChildItem "$env:ProgramFiles\Unity\Hub\Editor\*\Editor\Unity.exe" -ErrorAction SilentlyContinue |
        Sort-Object FullName
    if ($candidates.Count -gt 0) {
        $Unity = $candidates[-1].FullName
    }
}

if ([string]::IsNullOrEmpty($Unity) -or -not (Test-Path $Unity)) {
    Write-Error "Unity editor not found. Pass -Unity 'C:\...\Editor\Unity.exe' or install via Unity Hub with 'Windows Build Support (Mono)'."
    exit 1
}

Write-Host "Using Unity: $Unity"
Write-Host "Project:     $Project"

New-Item -ItemType Directory -Force -Path "$Project\Builds" | Out-Null

& "$Unity" -batchmode -quit `
    -projectPath "$Project" `
    -executeMethod AeroTerra.EditorTools.BuildScript.BuildWindows `
    -logFile "$Project\Builds\log-Windows.txt"

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed — see Builds\log-Windows.txt"
    exit $LASTEXITCODE
}

Write-Host "Done -> Builds\Windows\AeroTerra.exe"
