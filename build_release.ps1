# PowerShell script to build all BaoTools v105.4 release packages
$ErrorActionPreference = "Stop"

Write-Host "===============================================" -ForegroundColor Cyan
Write-Host " Building BaoTools v105.4 Release Packages" -ForegroundColor Cyan
Write-Host "===============================================" -ForegroundColor Cyan

# 1. Run Tests
Write-Host "`n[1/4] Running unit tests..." -ForegroundColor Yellow
dotnet test --nologo -v q
if ($LASTEXITCODE -ne 0) {
    Write-Error "Unit tests failed. Build aborted."
    exit 1
}

# 2. Build Portable Directory
Write-Host "`n[2/4] Building portable folder (out_portable)..." -ForegroundColor Yellow
dotnet publish src/BaoToolsGui/BaoToolsGui.csproj -c Release -o out_portable --nologo -v q

# 3. Compile Inno Setup Installer
Write-Host "`n[3/4] Compiling Inno Setup installer (BaoTools_Setup.exe)..." -ForegroundColor Yellow
$iscc = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if (Test-Path $iscc) {
    & $iscc setup.iss | Out-Null
} else {
    Write-Warning "Inno Setup compiler (ISCC.exe) not found. Skipping installer generation."
}

# 4. Build Single-File Self-Contained Portable Executable
Write-Host "`n[4/4] Building standalone single-file BaoTools.exe (self-contained)..." -ForegroundColor Yellow
dotnet publish src/BaoToolsGui/BaoToolsGui.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o out_single_standalone --nologo -v q

# 5. Assemble to release_v105.4
Write-Host "`nAssembling release assets to release_v105.4/ ..." -ForegroundColor Green
New-Item -ItemType Directory -Force -Path "release_v105.4" | Out-Null
Copy-Item "out_single_standalone\BaoTools.exe" "release_v105.4\BaoTools.exe" -Force
if (Test-Path "out_setup\BaoTools_Setup.exe") {
    Copy-Item "out_setup\BaoTools_Setup.exe" "release_v105.4\BaoTools_Setup.exe" -Force
}
Compress-Archive -Path "out_portable\*" -DestinationPath "release_v105.4\BaoTools_v105.4_Portable.zip" -Force

Write-Host "`nAll release assets successfully created in release_v105.4/:" -ForegroundColor Green
Get-ChildItem "release_v105.4" | Select-Object Name, Length, LastWriteTime | Format-Table -AutoSize
