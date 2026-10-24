<#
.SYNOPSIS
    Checks that a local copy of a notebook is complete and up to date, by comparing two pre-scans. READ-ONLY.

.DESCRIPTION
    Copies of notebooks (OneNote.com export, .onepkg, copied .one files) can silently miss pages, or hold an
    older version of a page. Run tools\onenote-prescan.ps1 twice:
      1. with the ORIGINAL (cloud) notebooks open      => -Reference
      2. with only the LOCAL copies open               => -Copy
    This script matches pages by notebook + section + title (+ created date when titles repeat) and lists:
      missing   page in the original, not in the copy
      older     page in the copy was last modified before the original (copy may be stale)
      shorter   copy has clearly less text, fewer images or fewer attachments than the original
      extra     page only in the copy (usually harmless)
    Writes compare_pages.csv and compare_problems.csv to -OutDir. Exit code 0 = copy matches.

.USAGE
    powershell -ExecutionPolicy Bypass -File .\tools\compare-prescans.ps1 `
        -Reference C:\Notes\prescan-cloud\prescan_pages.csv -Copy C:\Notes\prescan-local\prescan_pages.csv `
        [-NotebookMap "My Notebook (cloud)=My Notebook"] [-OutDir C:\Notes\compare]
#>
param(
    [Parameter(Mandatory = $true)][string]$Reference,
    [Parameter(Mandatory = $true)][string]$Copy,
    # "reference name=copy name" pairs when the copy notebook has another name
    [string[]]$NotebookMap = @(),
    [string]$OutDir = (Join-Path (Get-Location) "compare"),
    [double]$MinTextRatio = 0.95
)

$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$csvEncoding = if ($PSVersionTable.PSVersion.Major -ge 6) { "utf8BOM" } else { "UTF8" }

# "powershell -File" passes "a=b","c=d" as ONE string: also accept "a=b;c=d"
$NotebookMap = @($NotebookMap | ForEach-Object { $_ -split ';' } | Where-Object { $_.Trim() })
$rename = @{}
foreach ($pair in $NotebookMap) { $kv = $pair -split "=", 2; if ($kv.Count -eq 2) { $rename[$kv[0].Trim()] = $kv[1].Trim() } }

function Key($row, [string]$nb) {
    # SectionPath starts with the notebook name; leave it out so a renamed copy still matches
    $parts = "$($row.SectionPath)" -split "/", 2
    $groups = if ($parts.Count -gt 1) { $parts[1] } else { "" }
    ("{0}|{1}|{2}|{3}" -f $nb.ToLowerInvariant(), $groups.ToLowerInvariant(), "$($row.Section)".ToLowerInvariant(), "$($row.Page)".Trim().ToLowerInvariant())
}
function Num($v) { if ($v -match '^\d+(\.\d+)?$') { [double]$v } else { 0 } }

$ref = Import-Csv -LiteralPath $Reference -Encoding UTF8
$cpy = Import-Csv -LiteralPath $Copy -Encoding UTF8

# Locked sections give no page rows: they would be "compared" as empty and the copy would look complete
$lockedRef = @()
$refSections = Join-Path (Split-Path -Parent (Convert-Path -LiteralPath $Reference)) "prescan_sections.csv"
if (Test-Path -LiteralPath $refSections) {
    $lockedRef = @(Import-Csv -LiteralPath $refSections -Encoding UTF8 | Where-Object { $_.Locked -eq "True" })
}
$lockedCopy = @()
$copySections = Join-Path (Split-Path -Parent (Convert-Path -LiteralPath $Copy)) "prescan_sections.csv"
if (Test-Path -LiteralPath $copySections) {
    $lockedCopy = @(Import-Csv -LiteralPath $copySections -Encoding UTF8 | Where-Object { $_.Locked -eq "True" })
}
Write-Host ("Reference: {0} pages. Copy: {1} pages." -f @($ref).Count, @($cpy).Count)

# Index the copy; titles can repeat in a section, so keep a list and match in created-date order
$copyIndex = @{}
foreach ($c in $cpy) {
    $k = Key $c $c.Notebook
    if (-not $copyIndex.ContainsKey($k)) { $copyIndex[$k] = New-Object System.Collections.Generic.List[object] }
    $copyIndex[$k].Add($c)
}
$used = @{}

$rows = foreach ($r in $ref) {
    $nb = if ($rename.ContainsKey($r.Notebook)) { $rename[$r.Notebook] } else { $r.Notebook }
    $k = Key $r $nb
    $cands = $copyIndex[$k]
    $c = $null
    if ($cands) {
        $c = $cands | Where-Object { -not $used.ContainsKey($_.PageId) -and $_.Created -eq $r.Created } | Select-Object -First 1
        if (-not $c) { $c = $cands | Where-Object { -not $used.ContainsKey($_.PageId) } | Select-Object -First 1 }
    }
    $issues = @()
    if (-not $c) { $issues += "missing" }
    else {
        $used[$c.PageId] = $true
        $rm = [datetime]::MinValue; $cm = [datetime]::MinValue
        [void][datetime]::TryParse($r.Modified, [ref]$rm); [void][datetime]::TryParse($c.Modified, [ref]$cm)
        if ($rm -gt $cm.AddMinutes(1)) { $issues += ("older (copy {0:yyyy-MM-dd HH:mm}, original {1:yyyy-MM-dd HH:mm})" -f $cm, $rm) }
        if ((Num $r.TextChars) -ge 40 -and (Num $c.TextChars) -lt (Num $r.TextChars) * $MinTextRatio) { $issues += ("shorter text {0}/{1}" -f $c.TextChars, $r.TextChars) }
        if ((Num $c.Images) -lt (Num $r.Images)) { $issues += ("images {0}/{1}" -f $c.Images, $r.Images) }
        if ((Num $c.Attachments) -lt (Num $r.Attachments)) { $issues += ("attachments {0}/{1}" -f $c.Attachments, $r.Attachments) }
        if ((Num $c.InkElements) -lt (Num $r.InkElements)) { $issues += ("ink {0}/{1}" -f $c.InkElements, $r.InkElements) }
    }
    [pscustomobject]@{
        Result = if ($issues.Count) { $issues -join ", " } else { "ok" }
        Notebook = $r.Notebook; SectionPath = $r.SectionPath; Section = $r.Section; Page = $r.Page
        Created = $r.Created; ModifiedOriginal = $r.Modified; ModifiedCopy = if ($c) { $c.Modified } else { "" }
    }
}

$extra = $cpy | Where-Object { -not $used.ContainsKey($_.PageId) }
$rows | Export-Csv -LiteralPath (Join-Path $OutDir "compare_pages.csv") -NoTypeInformation -Encoding $csvEncoding -Delimiter ";"
$problems = $rows | Where-Object Result -ne "ok"
$problems | Export-Csv -LiteralPath (Join-Path $OutDir "compare_problems.csv") -NoTypeInformation -Encoding $csvEncoding -Delimiter ";"
$extra | Export-Csv -LiteralPath (Join-Path $OutDir "compare_extra.csv") -NoTypeInformation -Encoding $csvEncoding -Delimiter ";"

$missing = @($rows | Where-Object { $_.Result -like "missing*" }).Count
$older = @($rows | Where-Object { $_.Result -like "*older*" }).Count
$other = @($problems).Count - $missing - $older
Write-Host ""
Write-Host ("Matching pages : {0}" -f (@($rows).Count - @($problems).Count)) -ForegroundColor Green
if ($missing) { Write-Host ("Missing in copy: {0}" -f $missing) -ForegroundColor Red }
if ($older) { Write-Host ("Older in copy  : {0}  (the copy may be stale: export these again from the original)" -f $older) -ForegroundColor Red }
if ($other -gt 0) { Write-Host ("Less content   : {0}" -f $other) -ForegroundColor Red }
if (@($extra).Count) { Write-Host ("Only in copy   : {0}" -f @($extra).Count) -ForegroundColor Yellow }
foreach ($l in $lockedRef) { Write-Host ("LOCKED in original scan (not compared): {0}" -f (@($l.SectionPath, $l.Section) -join "/")) -ForegroundColor Red }
foreach ($l in $lockedCopy) { Write-Host ("LOCKED in copy scan (not compared): {0}" -f (@($l.SectionPath, $l.Section) -join "/")) -ForegroundColor Red }
Write-Host ("Details in {0}" -f $OutDir)
if (@($problems).Count -eq 0 -and $lockedRef.Count -eq 0 -and $lockedCopy.Count -eq 0) { exit 0 } else { exit 1 }
