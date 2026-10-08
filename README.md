# Kuaa-app MSIX Package

This repository contains the essential files to build and submit the **kuaa Estudante** (Student app) as an MSIX package to the Microsoft Partner Center.

## Package Identity

- **Package/Identity/Name**: `wavizo.Kuaa`
- **Package/Identity/Publisher**: `CN=57BB464E-553F-45B6-A4ED-B253157408EB`
- **Package/Properties/PublisherDisplayName**: `wavizo`
- **Package Family Name (PFN)**: `wavizo.Kuaa_c5p81jb0en0bm`
- **Package SID**: `S-1-15-2-2300805751-4288129790-265490174-3265155858-4293854383-3716263767-745956416`
- **Store ID**: `9NR9N6L65XX8`
- **Version**: `1.3.9.0`

## Structure

```
Kuaa-app/
├── AppxManifest.xml          # MSIX package manifest
├── Mapping.txt               # File mapping for MakeAppx
├── build-msix.ps1            # Build script
├── assets/                   # MSIX required icons (Square44x44, Square150x150, etc.)
├── icons/                    # App icons (192x192, 512x512, maskable)
├── medals/                   # Achievement medal images
├── index.html                # PWA entry point
├── manifest.webmanifest      # PWA manifest
├── service-worker.js         # Service worker for offline support
├── politica-privacidade.html # Privacy policy
├── _redirects                # SPA redirect rules
├── initial-student-package.json # Pre-loaded exam package
└── Host/                     # WebView2 host application
    ├── KuaaApp.Host.csproj   # .NET 8 project
    ├── Program.cs            # Main entry point
    └── app.manifest          # Application manifest
```

## Prerequisites

- Windows 10/11 with Windows 10 SDK (10.0.17763.0 or later)
- .NET 8 SDK
- Visual Studio 2022 or Build Tools
- Code signing certificate (for Store submission)

## Building the MSIX Package

### Option 1: Using the Build Script (Recommended)

```powershell
# Build for x64 (default)
.\build-msix.ps1

# Build for specific platform
.\build-msix.ps1 -Platform x64
.\build-msix.ps1 -Platform x86
.\build-msix.ps1 -Platform arm64

# Build and sign (requires certificate)
.\build-msix.ps1 -SignPackage -CertificatePath "path\to\cert.pfx" -CertificatePassword "password"
```

### Option 2: Manual Build

1. **Build the host application:**
   ```powershell
   dotnet publish Host\KuaaApp.Host.csproj -c Release -r win-x64 --self-contained true -o PackageOutput\Package
   ```

2. **Copy web assets to PackageOutput\Package**

3. **Create MSIX with MakeAppx:**
   ```powershell
   makeappx pack /p PackageOutput\wavizo.Kuaa_1.3.9.0_x64.msix /l /o /f Mapping.txt
   ```

4. **Sign the package (for Store):**
   ```powershell
   signtool sign /fd SHA256 /f cert.pfx /p password PackageOutput\wavizo.Kuaa_1.3.9.0_x64.msix
   ```

## Microsoft Partner Center Submission

1. Go to [Partner Center](https://partner.microsoft.com/dashboard)
2. Create a new app submission
3. Reserve the name "Kuaa" (must match Package/Properties/DisplayName)
4. Upload the generated `.msix` file
5. Fill in the store listing details
6. Submit for certification

## App Features

- **Offline-first PWA**: Works without internet after first load
- **Exam management**: Import, organize, and take practice exams
- **Question bank**: 885 exams, 7,781 questions across multiple categories
- **Multiple modes**: Quick questions, timed exams, rhythm mode, quizzes
- **Custom themes**: Import custom visual themes
- **Progress tracking**: Statistics, history, and adaptive difficulty
- **Portuguese (Brazil)**: Full localization

## Technical Details

- **Framework**: React 19 + Vite 7 + TypeScript
- **Host**: .NET 8 + WebView2 1.0.3124.44 (WinForms, sem WindowsAppSDK — o Host usa só WebView2)
- **Capabilities**: internetClient, internetClientServer, privateNetworkClientServer, runFullTrust
- **Target**: Windows 10 1809+ (10.0.17763.0)
- **Architectures**: x86, x64, arm64

## Related Repositories

- **Kuaa-admin MSIX**: https://github.com/wavizo-bot/Kuaa-admin-MSIX.git
- **Source (Student App)**: https://github.com/wavizo-bot/Kuaa-app
- **Source (Admin Panel)**: https://github.com/wavizo-bot/Kuaa-admin
