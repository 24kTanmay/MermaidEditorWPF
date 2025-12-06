# Mermaid Diagram Editor - Build & Package Script
# Supports multiple installer formats

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("Build", "MSI", "MSIX", "Portable", "All")]
    [string]$Target = "Build",
    
    [Parameter(Mandatory=$false)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    
    [Parameter(Mandatory=$false)]
    [switch]$SelfContained = $true
)

$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host $Message -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
}

function Write-Success {
    param([string]$Message)
    Write-Host "? $Message" -ForegroundColor Green
}

function Write-Info {
    param([string]$Message)
    Write-Host "? $Message" -ForegroundColor Yellow
}

function Write-ErrorMsg {
    param([string]$Message)
    Write-Host "? $Message" -ForegroundColor Red
}

# Main Build Function
function Build-Application {
    Write-Step "Building Mermaid Diagram Editor"
    
    # Clean
    Write-Info "Cleaning previous builds..."
    dotnet clean -c $Configuration
    if (-not $?) { throw "Clean failed" }
    Write-Success "Clean completed"
    
    # Restore
    Write-Info "Restoring NuGet packages..."
    dotnet restore
    if (-not $?) { throw "Restore failed" }
    Write-Success "Restore completed"
    
    # Build
    Write-Info "Building application ($Configuration)..."
    $publishArgs = @(
        "publish",
        "-c", $Configuration,
        "-r", "win-x64"
    )
    
    if ($SelfContained) {
        $publishArgs += "--self-contained", "true"
        $publishArgs += "-p:PublishSingleFile=false"
    } else {
        $publishArgs += "--self-contained", "false"
    }
    
    $publishArgs += "-p:PublishReadyToRun=true"
    $publishArgs += "-p:DebugType=none"
    $publishArgs += "-p:DebugSymbols=false"
    
    & dotnet $publishArgs
    if (-not $?) { throw "Build failed" }
    Write-Success "Build completed successfully"
    
    return "bin\$Configuration\net8.0-windows10.0.19041.0\win-x64\publish"
}

# Create Portable ZIP
function Create-PortableZip {
    param([string]$PublishDir)
    
    Write-Step "Creating Portable ZIP Package"
    
    $outputDir = "bin\Portable"
    $zipFile = "MermaidEditor-Portable-v1.0.0.zip"
    
    if (Test-Path $outputDir) {
        Remove-Item $outputDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
    
    Write-Info "Copying files..."
    Copy-Item -Path "$PublishDir\*" -Destination $outputDir -Recurse -Force
    
    # Add README for portable version
    $portableReadme = @"
Mermaid Diagram Editor - Portable Version
==========================================

This is a portable version that doesn't require installation.

To run:
1. Extract this ZIP file to any folder
2. Run MermaidEditor.exe

Requirements:
- Windows 10/11 (version 1809 or later)
- .NET 8 Desktop Runtime (included if self-contained)
- WebView2 Runtime (usually pre-installed on Windows 11)

For more information, visit: https://github.com/yourcompany/mermaid-editor
"@
    
    Set-Content -Path "$outputDir\README.txt" -Value $portableReadme
    
    Write-Info "Creating ZIP archive..."
    $zipPath = "bin\$zipFile"
    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }
    
    Compress-Archive -Path "$outputDir\*" -DestinationPath $zipPath -CompressionLevel Optimal
    Write-Success "Portable ZIP created: $zipPath"
}

# Main Script
try {
    Clear-Host
    Write-Host ""
    Write-Host "??????????????????????????????????????????" -ForegroundColor Magenta
    Write-Host "?  Mermaid Diagram Editor - Builder     ?" -ForegroundColor Magenta
    Write-Host "?  Version 1.0.0                         ?" -ForegroundColor Magenta
    Write-Host "??????????????????????????????????????????" -ForegroundColor Magenta
    Write-Host ""
    Write-Info "Configuration: $Configuration"
    Write-Info "Self-Contained: $SelfContained"
    Write-Info "Target: $Target"
    Write-Host ""
    
    # Build Application
    if ($Target -in @("Build", "Portable", "MSI", "MSIX", "All")) {
        $publishDir = Build-Application
    }
    
    # Create Portable ZIP
    if ($Target -in @("Portable", "All")) {
        Create-PortableZip -PublishDir $publishDir
    }
    
    # MSIX Instructions
    if ($Target -in @("MSIX", "All")) {
        Write-Step "MSIX Package Instructions"
        Write-Info "To create MSIX package:"
        Write-Host "  1. Install Windows SDK: https://developer.microsoft.com/windows/downloads/windows-sdk/" -ForegroundColor White
        Write-Host "  2. Create app icons in Images\ folder" -ForegroundColor White
        Write-Host "  3. Run: makeappx pack /d bin\Package /p MermaidEditor.msix" -ForegroundColor White
        Write-Host "  4. Sign: signtool sign /fd SHA256 /a /f cert.pfx /p password MermaidEditor.msix" -ForegroundColor White
        Write-Host ""
        Write-Host "  Or use Visual Studio 2022 with Windows Application Packaging Project" -ForegroundColor Cyan
    }
    
    # MSI/Inno Setup Instructions
    if ($Target -in @("MSI", "All")) {
        Write-Step "Windows Installer Instructions"
        Write-Info "To create Windows installer:"
        Write-Host "  Option 1: Inno Setup (Recommended)" -ForegroundColor Cyan
        Write-Host "    1. Download Inno Setup: https://jrsoftware.org/isinfo.php" -ForegroundColor White
        Write-Host "    2. Open Setup.iss in Inno Setup Compiler" -ForegroundColor White
        Write-Host "    3. Click Build -> Compile" -ForegroundColor White
        Write-Host ""
        Write-Host "  Option 2: WiX Toolset" -ForegroundColor Cyan
        Write-Host "    1. Install WiX: dotnet tool install --global wix" -ForegroundColor White
        Write-Host "    2. Create WiX configuration" -ForegroundColor White
        Write-Host "    3. Run: wix build -o MermaidEditor.msi installer.wxs" -ForegroundColor White
    }
    
    Write-Host ""
    Write-Step "Build Complete!"
    Write-Success "All operations completed successfully"
    Write-Host ""
    Write-Info "Output locations:"
    Write-Host "  - Published app: $publishDir" -ForegroundColor Gray
    if ($Target -in @("Portable", "All")) {
        Write-Host "  - Portable ZIP: bin\MermaidEditor-Portable-v1.0.0.zip" -ForegroundColor Gray
    }
    Write-Host ""
    
} catch {
    Write-Host ""
    Write-ErrorMsg "Build failed: $_"
    Write-Host $_.ScriptStackTrace -ForegroundColor DarkRed
    exit 1
}
