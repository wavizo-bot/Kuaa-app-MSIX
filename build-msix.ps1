<# 
.SYNOPSIS
    Build script for Kuaa-app MSIX package
.DESCRIPTION
    Builds the WebView2 host application and creates an MSIX package for Microsoft Store submission
#>

param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [switch]$SignPackage,
    [string]$CertificatePath,
    [string]$CertificatePassword
)

$ErrorActionPreference = "Stop"

$ProjectRoot = $PSScriptRoot
$HostProject = "$ProjectRoot\Host\KuaaApp.Host.csproj"
$OutputDir = "$ProjectRoot\PackageOutput"
$PackageDir = "$OutputDir\Package"
$MappingFile = "$ProjectRoot\Mapping.txt"
$ManifestFile = "$ProjectRoot\AppxManifest.xml"
$PackageName = "wavizo.Kuaa_1.3.7.0_${Platform}.msix"

Write-Host "=== Building Kuaa-app MSIX Package ===" -ForegroundColor Green
Write-Host "Configuration: $Configuration" -ForegroundColor Cyan
Write-Host "Platform: $Platform" -ForegroundColor Cyan

# Clean output directories
if (Test-Path $OutputDir) { Remove-Item $OutputDir -Recurse -Force }
New-Item -ItemType Directory -Path $PackageDir -Force | Out-Null

# Step 1: Build the WebView2 host application
Write-Host "`n[1/4] Building WebView2 host application..." -ForegroundColor Yellow
dotnet publish $HostProject `
    -c $Configuration `
    -r "win-$Platform" `
    --self-contained true `
    -p:PublishSingleFile=false `
    -p:PublishTrimmed=false `
    -o "$PackageDir" `
    -v q

if (-not (Test-Path "$PackageDir\KuaaApp.Host.exe")) {
    throw "Build failed: KuaaApp.Host.exe not found"
}
Write-Host "Host application built successfully" -ForegroundColor Green

# Step 2: Copy web assets to package directory
Write-Host "`n[2/4] Copying web assets..." -ForegroundColor Yellow

# Copy all files from the root (web assets — mesma lista do espelho do sync,
# senao o Mapping referencia arquivos que nao estao no diretorio do pacote)
$webAssets = @(
    "index.html", "manifest.webmanifest", "service-worker.js",
    "politica-privacidade.html", "_redirects", "initial-student-package.json",
    "caderno-favicon-v2.png", "favicon.png", "favicon.ico"
)

foreach ($asset in $webAssets) {
    $src = "$ProjectRoot\$asset"
    if (Test-Path $src) {
        Copy-Item $src -Destination "$PackageDir\$asset" -Force
    }
}

# Copy directories
$dirs = @("assets", "icons", "medals")
foreach ($dir in $dirs) {
    $src = "$ProjectRoot\$dir"
    if (Test-Path $src) {
        Copy-Item $src -Destination "$PackageDir\$dir" -Recurse -Force
    }
}

# Copy AppxManifest
Copy-Item $ManifestFile -Destination "$PackageDir\AppxManifest.xml" -Force

Write-Host "Web assets copied" -ForegroundColor Green

# Step 3: Create MSIX package using MakeAppx
Write-Host "`n[3/4] Creating MSIX package with MakeAppx..." -ForegroundColor Yellow

$MakeAppx = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\makeappx.exe"
if (-not (Test-Path $MakeAppx)) {
    # Try to find makeappx in Windows Kits
    $kits = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin" -Directory | Sort-Object Name -Descending
    foreach ($kit in $kits) {
        $candidate = "$($kit.FullName)\$Platform\makeappx.exe"
        if (Test-Path $candidate) {
            $MakeAppx = $candidate
            break
        }
    }
}

if (-not (Test-Path $MakeAppx)) {
    throw "MakeAppx.exe not found. Please install Windows 10 SDK."
}

# Trava: todo arquivo do Mapping precisa existir no diretorio do pacote
# (se o sync-msix.ps1 rodou, esta lista esta completa — nao editar a mao).
$mappingSources = Get-Content $MappingFile | Where-Object { $_ -match '^\s*"' } | ForEach-Object {
    ($_ -split '"')[1]
} | Where-Object { $_ -ne "AppxManifest.xml" }
$missingSources = @($mappingSources | Where-Object { -not (Test-Path (Join-Path $PackageDir $_)) })
if ($missingSources.Count) { throw "Arquivos do Mapping ausentes no pacote: $($missingSources -join ', ')" }

# O MakeAppx NAO aceita /d e /f juntos: usamos so o Mapping (caminhos relativos
# resolvidos a partir do diretorio do pacote).
Push-Location $PackageDir
try {
    & $MakeAppx pack /p "$OutputDir\$PackageName" /l /o /f $MappingFile
} finally {
    Pop-Location
}

if (-not (Test-Path "$OutputDir\$PackageName")) {
    throw "Package creation failed"
}

Write-Host "MSIX package created: $OutputDir\$PackageName" -ForegroundColor Green

# Step 4: Sign the package (optional)
if ($SignPackage) {
    Write-Host "`n[4/4] Signing MSIX package..." -ForegroundColor Yellow
    
    if (-not $CertificatePath -or -not (Test-Path $CertificatePath)) {
        throw "Certificate path not provided or not found"
    }
    
    $SignTool = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\signtool.exe"
    if (-not (Test-Path $SignTool)) {
        $kits = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin" -Directory | Sort-Object Name -Descending
        foreach ($kit in $kits) {
            $candidate = "$($kit.FullName)\$Platform\signtool.exe"
            if (Test-Path $candidate) {
                $SignTool = $candidate
                break
            }
        }
    }
    
    $signArgs = "sign /fd SHA256 /f `"$CertificatePath`""
    if ($CertificatePassword) { $signArgs += " /p $CertificatePassword" }
    $signArgs += " `"$OutputDir\$PackageName`""
    
    & $SignTool $signArgs
    Write-Host "Package signed successfully" -ForegroundColor Green
}

Write-Host "`n=== Build Complete ===" -ForegroundColor Green
Write-Host "Package: $OutputDir\$PackageName" -ForegroundColor Cyan
Write-Host "Package Family Name: wavizo.Kuaa_c5p81jb0en0bm" -ForegroundColor Cyan
Write-Host "Store ID: 9NR9N6L65XX8" -ForegroundColor Cyan