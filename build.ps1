<#
.SYNOPSIS
    Builds OneNoteMdExporter on a Windows PC that has desktop OneNote installed.

.DESCRIPTION
    The project references the OneNote COM type library, so it can only be built on a
    machine where desktop OneNote is installed (cloud build runners do not have it).
    `dotnet build` does not support COM references, so the build uses Visual Studio's MSBuild.

    Requirements (one-time, verified 2026-10-17 on Windows 11 with Microsoft 365 OneNote):
      - .NET 10 SDK
            winget install --id Microsoft.DotNet.SDK.10 -e
      - Visual Studio 2026 Build Tools (MSBuild 18) with the ".NET desktop build tools" workload.
        The 2022 Build Tools (MSBuild 17) cannot load the .NET 10 SDK: "requires at least version 18.0.0 of MSBuild".
            winget install --id Microsoft.VisualStudio.BuildTools -e --override "--quiet --wait --norestart --add Microsoft.VisualStudio.Workload.ManagedDesktopBuildTools --includeRecommended"
      - Desktop OneNote (Microsoft 365 / 2016+), not the Store version
    Optional, used at export time (not for the build):
      - ffmpeg (recording timeline check, MP3/MP4 copies):   winget install --id Gyan.FFmpeg -e
      - Ghostscript and veraPDF for tools\pdfa.ps1: not in winget, download from ghostscript.com / verapdf.org

.USAGE
    Open Windows PowerShell in the repo folder and run:
        powershell -ExecutionPolicy Bypass -File .\build.ps1
    Output: .\dist\OneNoteMdExporter\  (and a zip next to it)
#>

$ErrorActionPreference = "Stop"
$root    = $PSScriptRoot
$proj    = Join-Path $root "src\OneNoteMdExporter\OneNoteMdExporter.csproj"
$pandocD = Join-Path $root "src\OneNoteMdExporter\pandoc"
$dist    = Join-Path $root "dist\OneNoteMdExporter"

# 1. Unzip pandoc.exe (shipped zipped in the repo)
if (-not (Test-Path (Join-Path $pandocD "pandoc.exe"))) {
    $zip = Get-ChildItem $pandocD -Filter "pandoc-*-windows-x86_64.zip" | Select-Object -First 1
    if (-not $zip) { throw "pandoc zip not found in $pandocD" }
    Write-Host "Extracting pandoc from $($zip.Name)..."
    $tmp = Join-Path $env:TEMP ("pandoc_" + [guid]::NewGuid())
    Expand-Archive -Path $zip.FullName -DestinationPath $tmp
    Copy-Item (Get-ChildItem $tmp -Recurse -Filter pandoc.exe | Select-Object -First 1).FullName $pandocD
    Remove-Item $tmp -Recurse -Force
}

# 2. .NET 10 SDK. A shell opened before the SDK was installed has no dotnet on PATH: add the default folder.
$dotnetDir = Join-Path $env:ProgramFiles "dotnet"
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue) -and (Test-Path (Join-Path $dotnetDir "dotnet.exe"))) {
    $env:PATH = "$dotnetDir;$env:PATH"
}
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw ".NET SDK not found. Install: winget install --id Microsoft.DotNet.SDK.10 -e" }
$env:DOTNET_ROOT = Split-Path (Get-Command dotnet).Source
$sdks = & dotnet --list-sdks
if (-not ($sdks | Where-Object { $_ -match "^10\." })) { throw ".NET 10 SDK not found (installed: $($sdks -join ', ')). Install: winget install --id Microsoft.DotNet.SDK.10 -e" }

# 3. Find MSBuild 18+ via vswhere (the .NET 10 SDK refuses older MSBuild)
$vswhere = Join-Path ${env:ProgramFiles(x86)} "Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $vswhere)) { throw "vswhere not found. Install Visual Studio 2026 Build Tools (see header)." }
$msbuild = & $vswhere -latest -products * -version "[18.0,)" -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
if (-not $msbuild) {
    $older = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -property installationVersion
    throw "MSBuild 18 (Visual Studio 2026 Build Tools, '.NET desktop build tools' workload) not found. Found: $(if ($older) { $older } else { 'nothing' }). See header for the winget command."
}
Write-Host "Using MSBuild: $msbuild"

# 4. Restore + publish self-contained Release build
if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
& $msbuild $proj /restore /t:Publish /p:Configuration=Release /p:PublishDir="$dist\" /v:minimal
if ($LASTEXITCODE -ne 0) { throw "Build failed (exit $LASTEXITCODE)" }

# 5. Zip it
$zipOut = "$dist.zip"
if (Test-Path $zipOut) { Remove-Item $zipOut -Force }
Compress-Archive -Path "$dist\*" -DestinationPath $zipOut
Write-Host "Done. Run: $dist\OneNoteMdExporter.exe" -ForegroundColor Green
