# Build MSIX Installer for Mermaid Diagram Editor
# This script builds the app and creates an MSIX package

param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64"
)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Mermaid Diagram Editor - MSIX Builder" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Clean previous builds
Write-Host "[1/5] Cleaning previous builds..." -ForegroundColor Yellow
dotnet clean -c $Configuration
if (-not $?) {
    Write-Host "? Clean failed!" -ForegroundColor Red
    exit 1
}
Write-Host "? Clean completed" -ForegroundColor Green
Write-Host ""

# Step 2: Restore dependencies
Write-Host "[2/5] Restoring dependencies..." -ForegroundColor Yellow
dotnet restore
if (-not $?) {
    Write-Host "? Restore failed!" -ForegroundColor Red
    exit 1
}
Write-Host "? Restore completed" -ForegroundColor Green
Write-Host ""

# Step 3: Build the application
Write-Host "[3/5] Building application..." -ForegroundColor Yellow
dotnet publish -c $Configuration -r win-x64 --self-contained true -p:PublishSingleFile=false -p:PublishReadyToRun=true
if (-not $?) {
    Write-Host "? Build failed!" -ForegroundColor Red
    exit 1
}
Write-Host "? Build completed" -ForegroundColor Green
Write-Host ""

# Step 4: Prepare packaging folder
Write-Host "[4/5] Preparing packaging folder..." -ForegroundColor Yellow
$publishDir = "bin\$Configuration\net8.0-windows10.0.19041.0\win-x64\publish"
$packageDir = "bin\Package"

if (Test-Path $packageDir) {
    Remove-Item $packageDir -Recurse -Force
}
New-Item -ItemType Directory -Path $packageDir -Force | Out-Null

# Copy published files
Copy-Item -Path "$publishDir\*" -Destination $packageDir -Recurse -Force

# Copy manifest
Copy-Item -Path "Package.appxmanifest" -Destination $packageDir -Force

# Create Images folder for assets
$imagesDir = "$packageDir\Images"
New-Item -ItemType Directory -Path $imagesDir -Force | Out-Null

Write-Host "? Packaging folder prepared" -ForegroundColor Green
Write-Host ""

# Step 5: Create MSIX package
Write-Host "[5/5] Creating MSIX package..." -ForegroundColor Yellow
Write-Host ""
Write-Host "To create the MSIX package, you need to:" -ForegroundColor Cyan
Write-Host "1. Install Windows SDK (includes makeappx.exe)" -ForegroundColor White
Write-Host "2. Create app icons in Images\ folder:" -ForegroundColor White
Write-Host "   - Square44x44Logo.png (44x44)" -ForegroundColor Gray
Write-Host "   - Square150x150Logo.png (150x150)" -ForegroundColor Gray
Write-Host "   - Wide310x150Logo.png (310x150)" -ForegroundColor Gray
Write-Host "   - StoreLogo.png (50x50)" -ForegroundColor Gray
Write-Host "   - SplashScreen.png (620x300)" -ForegroundColor Gray
Write-Host "3. Run: makeappx pack /d bin\Package /p MermaidEditor.msix" -ForegroundColor White
Write-Host "4. Sign the package (for distribution)" -ForegroundColor White
Write-Host ""
Write-Host "Or use Visual Studio 2022 with 'Windows Application Packaging Project' template" -ForegroundColor Cyan
Write-Host ""

Write-Host "========================================" -ForegroundColor Green
Write-Host "Build completed successfully!" -ForegroundColor Green
Write-Host "Output: $packageDir" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
