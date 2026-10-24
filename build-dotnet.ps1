<#
.SYNOPSIS
    Builds OneNoteMdExporter with the plain .NET SDK (no Visual Studio Build Tools needed).

.DESCRIPTION
    Same output as build.ps1 (self-contained single-file Release publish in .\dist\OneNoteMdExporter
    plus a zip), but built with `dotnet publish` instead of Visual Studio's MSBuild.

    This works because the project no longer uses <COMReference> for the OneNote type library:
    it references lib\Interop.Microsoft.Office.Interop.OneNote.dll, the tlbimp output of that
    type library generated once and committed (see the csproj). So the build needs neither
      - .NET Framework MSBuild (ResolveComReference / tlbimp), nor
      - desktop OneNote installed on the build machine.
    Desktop OneNote is of course still needed to RUN an export.

    Requirements:
      - .NET 10 SDK:  winget install --id Microsoft.DotNet.SDK.10 -e
    Optional, used at export time (not for the build):
      - ffmpeg:       winget install --id Gyan.FFmpeg -e
      - Ghostscript and veraPDF for tools\pdfa.ps1 (not in winget)

    build.ps1 (MSBuild) is kept and still works; it is the fallback if this route ever breaks.

.USAGE
    Open PowerShell in the repo folder and run:
        powershell -ExecutionPolicy Bypass -File .\build-dotnet.ps1
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
Write-Host "Using dotnet: $((Get-Command dotnet).Source)"

# 3. Publish self-contained, single file, Release (same settings as the csproj uses for Release)
# Round 17: never publish over a running exporter. A publish while an export ran from dist\ could not replace
# the locked exe, left dist\ with three files, and the next two exports started from it crashed at start-up.
$running = Get-Process OneNoteMdExporter -ErrorAction SilentlyContinue
if ($running) {
    Write-Host ("STOP: OneNoteMdExporter.exe is running (PID {0}). Publishing would pull dist\ out from under it. Wait for the export to finish, or build without publishing: dotnet build src\OneNoteMdExporter -c Release" -f (($running | ForEach-Object Id) -join ", ")) -ForegroundColor Red
    exit 3
}

if (Test-Path $dist) { Remove-Item $dist -Recurse -Force }
& dotnet publish $proj -c Release -r win-x64 -o "$dist" -v minimal
if ($LASTEXITCODE -ne 0) { throw "Build failed (exit $LASTEXITCODE)" }

# 4. Zip it
$zipOut = "$dist.zip"
if (Test-Path $zipOut) { Remove-Item $zipOut -Force }
Compress-Archive -Path "$dist\*" -DestinationPath $zipOut
Write-Host "Done. Run: $dist\OneNoteMdExporter.exe" -ForegroundColor Green
