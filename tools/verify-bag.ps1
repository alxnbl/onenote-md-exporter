<#
.SYNOPSIS
    Fixity check of a BagIt bag made by make-bag.ps1 (or any BagIt 1.0 bag with SHA-256). READ-ONLY.

.DESCRIPTION
    Recomputes every checksum and reports files that changed, are missing, or were added.
    Run it once a year on every copy of the archive (and after copying it to new media).
    Exit code 0 = bag intact, 1 = problems found.

.USAGE
    powershell -ExecutionPolicy Bypass -File .\tools\verify-bag.ps1 -Bag "E:\Archive\OneNote-2026-10"
#>
param([Parameter(Mandatory = $true)][string]$Bag)

$ErrorActionPreference = "Stop"
# Convert-Path: [ ] read literally, plain "\\nas\share" paths for a copy on a NAS
$Bag = (Convert-Path -LiteralPath $Bag).TrimEnd('\')
$manifestPath = Join-Path $Bag "manifest-sha256.txt"
if (-not (Test-Path -LiteralPath $manifestPath)) { throw "Not a bag (manifest-sha256.txt missing): $Bag" }

# RFC 8493: %0A %0D %25 (any case), decoded in one pass
function Decode([string]$p) {
    [regex]::Replace($p, '%(0[AaDd]|25)', { param($m) switch ($m.Groups[1].Value.ToUpperInvariant()) { "0A" { "`n" } "0D" { "`r" } default { "%" } } })
}

# Checksum, then one or more spaces or tabs, then the path
function ReadManifest([string]$path) {
    $result = @{}
    foreach ($line in [System.IO.File]::ReadAllLines($path)) {
        if (-not $line.Trim()) { continue }
        if ($line -match '^([0-9a-fA-F]{64})[ \t]+(.+)$') { $result[(Decode $Matches[2])] = $Matches[1].ToLowerInvariant() }
        else { Write-Host "BAD LINE $line" -ForegroundColor Red; $script:bad++ }
    }
    return $result
}

$bad = 0; $missing = 0; $unreadable = 0; $i = 0
$expected = ReadManifest $manifestPath

# Windows PowerShell 5.1 cannot see files with paths over 260 characters (it would report them all as MISSING)
if ($PSVersionTable.PSVersion.Major -lt 7) {
    $lp = (Get-ItemProperty -LiteralPath "HKLM:\SYSTEM\CurrentControlSet\Control\FileSystem" -Name LongPathsEnabled -ErrorAction SilentlyContinue).LongPathsEnabled
    $longest = ($expected.Keys | ForEach-Object { $_.Length } | Measure-Object -Maximum).Maximum
    if ($lp -ne 1 -and $Bag.Length + 1 + $longest -gt 259) {
        throw "This bag has paths over 260 characters. Run this script with PowerShell 7 (pwsh), or enable long paths in Windows."
    }
}

foreach ($rel in $expected.Keys) {
    $i++
    if ($i % 200 -eq 0) { Write-Progress -Activity "Checking" -Status "$i / $($expected.Count)" -PercentComplete (100 * $i / $expected.Count) }
    $full = Join-Path $Bag ($rel.Replace("/", "\"))
    if (-not (Test-Path -LiteralPath $full)) { Write-Host "MISSING  $rel" -ForegroundColor Red; $missing++; continue }
    # A bad sector is exactly what this check is for: report the file and go on
    try { $h = (Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash.ToLowerInvariant() }
    catch { Write-Host "UNREADABLE $rel ($($_.Exception.Message))" -ForegroundColor Red; $unreadable++; continue }
    if ($h -ne $expected[$rel]) { Write-Host "CHANGED  $rel" -ForegroundColor Red; $bad++ }
}
Write-Progress -Activity "Checking" -Completed

$data = Join-Path $Bag "data"
$extra = 0
foreach ($f in Get-ChildItem -LiteralPath $data -Recurse -File -Force) {
    $rel = "data/" + $f.FullName.Substring($data.Length + 1).Replace("\", "/")
    if (-not $expected.ContainsKey($rel)) { Write-Host "EXTRA    $rel" -ForegroundColor Yellow; $extra++ }
}

# Tag files
$tagPath = Join-Path $Bag "tagmanifest-sha256.txt"
if (Test-Path -LiteralPath $tagPath) {
    $tags = ReadManifest $tagPath
    foreach ($name in $tags.Keys) {
        $tagFile = Join-Path $Bag $name
        if (-not (Test-Path -LiteralPath $tagFile)) { Write-Host "MISSING  $name (tag file)" -ForegroundColor Red; $missing++; continue }
        $h = (Get-FileHash -LiteralPath $tagFile -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($h -ne $tags[$name]) { Write-Host "CHANGED  $name (tag file)" -ForegroundColor Red; $bad++ }
    }
}

# Payload-Oxum (bytes.files) from bag-info.txt: a quick second check of the totals. Tracked on its own, so a
# changed file counted above is not counted again here (it would overstate "Changed").
$oxumMismatch = $false
$infoPath = Join-Path $Bag "bag-info.txt"
if (Test-Path -LiteralPath $infoPath) {
    $oxum = [System.IO.File]::ReadAllLines($infoPath) | Where-Object { $_ -match '^Payload-Oxum:\s*(\d+)\.(\d+)' } | Select-Object -First 1
    if ($oxum -and $oxum -match '^Payload-Oxum:\s*(\d+)\.(\d+)') {
        $files = @(Get-ChildItem -LiteralPath $data -Recurse -File -Force)
        $bytes = ($files | Measure-Object -Property Length -Sum).Sum
        # Added files (EXTRA, reported above) also change the totals: only flag a mismatch they do not explain
        if ($extra -eq 0 -and ([int64]$Matches[1] -ne [int64]$bytes -or [int64]$Matches[2] -ne $files.Count)) {
            Write-Host ("Payload-Oxum differs: bag-info says {0} bytes in {1} files, found {2} bytes in {3} files" -f $Matches[1], $Matches[2], $bytes, $files.Count) -ForegroundColor Red
            $oxumMismatch = $true
        }
    }
}

Write-Host ""
Write-Host ("Files checked: {0}. Changed: {1}. Missing: {2}. Unreadable: {3}. Extra: {4}." -f $expected.Count, $bad, $missing, $unreadable, $extra)
# RFC 8493: a payload file not in the manifest also makes the bag invalid
if ($bad -eq 0 -and $missing -eq 0 -and $unreadable -eq 0 -and $extra -eq 0 -and -not $oxumMismatch) { Write-Host "Bag is intact." -ForegroundColor Green; exit 0 }
if ($bad -eq 0 -and $missing -eq 0 -and $unreadable -eq 0) {
    Write-Host "All listed files are intact, but files were ADDED to data\ after bagging (EXTRA). Remove them or make a new bag." -ForegroundColor Yellow
    exit 1
}
Write-Host "Bag has problems: restore the listed files from another copy." -ForegroundColor Red
exit 1
