<#
.SYNOPSIS
    Packs a finished export as a BagIt bag (RFC 8493, Library of Congress) with SHA-256 checksums.

.DESCRIPTION
    A bag is a plain folder anyone can check without special software:
        <Bag>\bagit.txt              format declaration
        <Bag>\bag-info.txt           who, what, when, size
        <Bag>\manifest-sha256.txt    checksum of every file under data\
        <Bag>\tagmanifest-sha256.txt checksum of the three files above
        <Bag>\data\...               the archive itself (unchanged, relative links keep working)

    The export is COPIED into <Bag>\data (default) or MOVED with -Move. Run it once the export is final:
    a resumed export would no longer find its files after -Move.
    Check the bag later (yearly) with verify-bag.ps1.

.USAGE
    powershell -ExecutionPolicy Bypass -File .\tools\make-bag.ps1 -Source "C:\Notes\OneNote-Archive" -Bag "E:\Archive\OneNote-2026-10"
#>
param(
    [Parameter(Mandatory = $true)][string]$Source,
    [Parameter(Mandatory = $true)][string]$Bag,
    [string]$Organization = "Private archive",
    [string]$Description = "Export of Microsoft OneNote notebooks to open formats (Markdown, PDF, SVG, original attachments)",
    [switch]$Move
)

$ErrorActionPreference = "Stop"
# Full, plain paths: Convert-Path reads [ ] literally and gives "\\nas\share" (not "Microsoft.PowerShell.Core\FileSystem::...").
# A relative -Bag is made absolute here: .NET and robocopy do not use PowerShell's current folder, and the manifest
# paths are cut from the full path.
$Source = (Convert-Path -LiteralPath $Source).TrimEnd('\')
$Bag = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine((Get-Location -PSProvider FileSystem).ProviderPath, $Bag)).TrimEnd('\')
if ($Bag.StartsWith($Source + "\", [StringComparison]::OrdinalIgnoreCase) -or $Bag -ieq $Source) { throw "The bag cannot be inside the export folder" }
if (Test-Path -LiteralPath (Join-Path $Bag "bagit.txt")) { throw "$Bag is already a bag" }

$data = Join-Path $Bag "data"
# A half-made bag from an earlier attempt: robocopy would merge into it and stale files would be checksummed too
if ((Test-Path -LiteralPath $data) -and (Get-ChildItem -LiteralPath $data -Force | Select-Object -First 1)) {
    throw "$data is not empty (earlier attempt?). Delete it or choose another -Bag folder."
}

# Windows PowerShell 5.1 cannot read paths over 260 characters unless long paths are enabled in Windows.
# The exporter keeps paths short under the export folder, but the bag folder adds its own length.
if ($PSVersionTable.PSVersion.Major -lt 7) {
    $lp = (Get-ItemProperty -LiteralPath "HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem" -Name LongPathsEnabled -ErrorAction SilentlyContinue).LongPathsEnabled
    if ($lp -ne 1) {
        $longest = (Get-ChildItem -LiteralPath $Source -Recurse -File -Force | ForEach-Object { $_.FullName.Length - $Source.Length } | Measure-Object -Maximum).Maximum
        if ($data.Length + $longest -gt 259) {
            throw ("Some paths in the bag would be {0} characters, too long for Windows PowerShell 5.1. Use a shorter -Bag folder, run this script with PowerShell 7 (pwsh), or enable long paths in Windows." -f ($data.Length + $longest))
        }
    }
}

New-Item -ItemType Directory -Force -Path $data | Out-Null

Write-Host "Copying files into $data ..." -ForegroundColor Cyan
# robocopy keeps timestamps and handles long paths (and, unlike Move-Item, moves between drives); exit codes below 8 mean success
if ($Move) {
    & robocopy $Source $data /E /MOVE /COPY:DAT /DCOPY:DAT /R:1 /W:1 /NFL /NDL /NJH /NP | Out-Null
} else {
    & robocopy $Source $data /E /COPY:DAT /DCOPY:DAT /R:1 /W:1 /NFL /NDL /NJH /NP | Out-Null
}
if ($LASTEXITCODE -ge 8) { throw "robocopy failed with code $LASTEXITCODE" }

Write-Host "Computing SHA-256 checksums ..." -ForegroundColor Cyan
$files = Get-ChildItem -LiteralPath $data -Recurse -File -Force
$utf8 = New-Object System.Text.UTF8Encoding($false)
$manifest = New-Object System.Text.StringBuilder
$total = 0L; $i = 0
foreach ($f in $files) {
    $i++
    if ($i % 200 -eq 0) { Write-Progress -Activity "Hashing" -Status "$i / $($files.Count)" -PercentComplete (100 * $i / $files.Count) }
    $hash = (Get-FileHash -LiteralPath $f.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    # BagIt paths: relative, forward slashes, % CR LF encoded
    $rel = "data/" + $f.FullName.Substring($data.Length + 1).Replace("\", "/").Replace("%", "%25").Replace("`r", "%0D").Replace("`n", "%0A")
    [void]$manifest.Append("$hash  $rel`n")
    $total += $f.Length
}
Write-Progress -Activity "Hashing" -Completed
[System.IO.File]::WriteAllText((Join-Path $Bag "manifest-sha256.txt"), $manifest.ToString(), $utf8)

[System.IO.File]::WriteAllText((Join-Path $Bag "bagit.txt"), "BagIt-Version: 1.0`nTag-File-Character-Encoding: UTF-8`n", $utf8)

$info = @(
    "Source-Organization: $Organization",
    "External-Description: $Description",
    "Bagging-Date: $(Get-Date -Format yyyy-MM-dd)",
    "Bag-Software-Agent: onenote-md-exporter tools/make-bag.ps1",
    "Payload-Oxum: $total.$($files.Count)",
    ("Bag-Size: {0:N1} GB" -f ($total / 1GB))
) -join "`n"
[System.IO.File]::WriteAllText((Join-Path $Bag "bag-info.txt"), $info + "`n", $utf8)

$tag = foreach ($t in "bagit.txt", "bag-info.txt", "manifest-sha256.txt") {
    "{0}  {1}" -f (Get-FileHash -LiteralPath (Join-Path $Bag $t) -Algorithm SHA256).Hash.ToLowerInvariant(), $t
}
[System.IO.File]::WriteAllText((Join-Path $Bag "tagmanifest-sha256.txt"), ($tag -join "`n") + "`n", $utf8)

Write-Host ("Bag ready: {0} files, {1:N1} GB in {2}" -f $files.Count, ($total / 1GB), $Bag) -ForegroundColor Green
Write-Host "Check it any time with: tools\verify-bag.ps1 -Bag `"$Bag`""
