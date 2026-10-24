<#
.SYNOPSIS
    Checks an export against the OneNote pre-scan: is every page there? READ-ONLY.

.DESCRIPTION
    Compares prescan_pages.csv (from onenote-prescan.ps1, what OneNote has) with the
    _export-report.csv files the exporter writes in each notebook folder (what was exported).

    A page counts as exported when the report has an ok, warning or skipped row for it AND its .md file exists.
    Pages are matched by OneNote page ID. When the pre-scan was taken on another copy of the notebook
    (IDs differ), pages are matched by notebook + section + title instead.

    Writes to -OutDir:
      verify_pages.csv     every pre-scanned page with its result (exported / failed / missing / md-missing)
      verify_problems.csv  only the pages that need attention
      verify_sections.csv  counts per section
    and prints a summary. Exit code 0 when everything is exported, 1 otherwise.

.USAGE
    powershell -ExecutionPolicy Bypass -File .\verify-export.ps1 `
        -Prescan "C:\Notes\prescan\prescan_pages.csv" `
        -ExportRoot "C:\Notes\OneNote-Archive" `
        [-Notebook "My Notebook"] [-OutDir "C:\Notes\verify"]
#>

param(
    [Parameter(Mandatory = $true)][string]$Prescan,
    [Parameter(Mandatory = $true)][string]$ExportRoot,
    [string]$Notebook = "",
    [string]$OutDir = (Join-Path (Get-Location) "verify"),
    # Flag pages whose exported text is shorter than this share of the OneNote text
    [double]$MinTextRatio = 0.8,
    # Ignore the text check for pages with less OneNote text than this (characters)
    [int]$MinTextChars = 40
)

$ErrorActionPreference = "Stop"
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
# Excel needs the BOM to read æøå; PowerShell 6+ writes UTF-8 without it unless asked
$csvEncoding = if ($PSVersionTable.PSVersion.Major -ge 6) { "utf8BOM" } else { "UTF8" }

# Same normalisation as the exporter: NFC, invalid file name characters removed, trailing dots/spaces trimmed, case-insensitive
$invalid = [regex]::Escape(-join [System.IO.Path]::GetInvalidFileNameChars())
function Norm([string]$s, [int]$max = 0) {
    if ($null -eq $s) { return "" }
    $t = ([regex]::Replace($s.Normalize([Text.NormalizationForm]::FormC), "[$invalid]", "")).Trim().TrimEnd('.', ' ')
    if ($max -gt 0 -and $t.Length -gt $max) { $t = $t.Substring(0, $max).TrimEnd('.', ' ') }
    return $t.ToLowerInvariant()
}
function TitleKey([string]$title) {
    # The exporter cuts the RAW title to 50 characters (PageTitleMaxLength) and adds "...", then cleans it:
    # cut first, then normalise, or titles with ":" "/" "?" never match
    $t = "$title"
    if ($t.EndsWith("...")) { $t = $t.Substring(0, $t.Length - 3) }
    if ($t.Length -gt 50) { $t = $t.Substring(0, 50) }
    return Norm $t
}

# ---------------------------------------------------------------------------
# Load pre-scan
# ---------------------------------------------------------------------------
$pre = Import-Csv -LiteralPath $Prescan -Encoding UTF8
if ($Notebook) { $pre = $pre | Where-Object { $_.Notebook -eq $Notebook } }
Write-Host ("Pre-scan: {0} pages" -f @($pre).Count)

# ---------------------------------------------------------------------------
# Load all export reports under the export root
# ---------------------------------------------------------------------------
$reports = Get-ChildItem -LiteralPath $ExportRoot -Filter "_export-report.csv" -Recurse -Depth 2 -ErrorAction SilentlyContinue
if (-not $reports) { throw "No _export-report.csv found under $ExportRoot. Was WriteExportReport enabled?" }

$byId  = @{}   # oneNoteId -> latest row that tells the page's state
$byKey = @{}   # notebook|section|title -> list of rows
$sectionRows = @{}   # folder|sectionPath -> latest locked / error row of the section
$lastPageRun = @{}   # folder|sectionPath -> latest run that exported a page of the section

foreach ($file in $reports) {
    $folder = $file.DirectoryName
    foreach ($r in (Import-Csv -LiteralPath $file.FullName -Delimiter ";" -Encoding UTF8)) {
        if ($Notebook -and $r.notebook -ne $Notebook) { continue }
        $r | Add-Member -NotePropertyName folder -NotePropertyValue $folder -Force
        $sk = "$folder|$($r.sectionPath)"

        # Section rows: locked section, or a section OneNote could not list
        if ($r.page -eq "(section)") { $sectionRows[$sk] = $r; continue }
        if ($r.status -ne "skipped" -and ($r.run -gt $lastPageRun[$sk])) { $lastPageRun[$sk] = $r.run }

        $id = $r.oneNoteId
        if ($id) {
            $prev = $byId[$id]
            # The LATEST row wins (rows are in time order): a page re-exported by a resumed run can have lost its PDF
            # or fallen back to raw text. A "skipped" row only confirms the earlier result.
            # Compared by run time, not file order: the page may also appear in an old report elsewhere under the root
            if (-not $prev -or (($r.status -ne "skipped" -or $prev.status -eq "skipped") -and $r.run -ge $prev.run)) { $byId[$id] = $r }
        }
    }
}

# A locked / unlisted section counts until a LATER run exported pages of it (unlocked and exported with --resume)
$locked = @($sectionRows.Values | Where-Object { -not ($lastPageRun["$($_.folder)|$($_.sectionPath)"] -gt $_.run) })

# Build the title index from the best rows only (one per page)
foreach ($r in $byId.Values) {
    $sectionPath = $r.sectionPath
    $key = "{0}|{1}|{2}" -f (Norm $r.notebook), (Norm (($sectionPath -split "/")[-1])), (TitleKey $r.page)
    if (-not $byKey.ContainsKey($key)) { $byKey[$key] = New-Object System.Collections.Generic.List[object] }
    $byKey[$key].Add($r)
}

Write-Host ("Export reports: {0} file(s), {1} distinct pages" -f @($reports).Count, $byId.Count)

# ---------------------------------------------------------------------------
# Match
# ---------------------------------------------------------------------------
$matchedIds = @{}
$rows = foreach ($p in $pre) {
    $r = $null; $how = ""
    if ($p.PageId -and $byId.ContainsKey($p.PageId)) { $r = $byId[$p.PageId]; $how = "id" }
    else {
        $key = "{0}|{1}|{2}" -f (Norm $p.Notebook), (Norm $p.Section), (TitleKey $p.Page)
        $cands = $byKey[$key]
        if ($cands) {
            $r = $cands | Where-Object { -not $matchedIds.ContainsKey($_.oneNoteId) } | Select-Object -First 1
            if ($r) { $how = "title" }
        }
    }
    if ($r) { $matchedIds[$r.oneNoteId] = $true }

    $result = "missing"
    $mdFull = ""
    if ($r) {
        if ($r.status -eq "error") { $result = "failed" }
        else {
            $mdFull = if ($r.mdPath) { Join-Path $r.folder $r.mdPath } else { "" }
            $result = if ($mdFull -and (Test-Path -LiteralPath $mdFull)) { "exported" } else { "md-missing" }
        }
    }

    # Content check: did the text, images and files make it?
    # $content = real gaps (count for the exit code); $info = gaps already covered by the export (raw text, snapshot, originals)
    $content = @(); $info = @()
    if ($r -and $result -eq "exported" -and $r.sourceTextChars -ne "") {
        $src = [int]("0" + $r.sourceTextChars); $md = [int]("0" + $r.mdTextChars)
        if ($src -ge $MinTextChars -and $md -lt $src * $MinTextRatio) {
            # mdTextChars is measured before the raw text block is added: with raw text the words are all there
            if ($r.rawTextAdded -eq "yes" -or $r.status -eq "fallback") { $info += ("text {0}/{1} (raw text added)" -f $md, $src) }
            else { $content += ("text {0}/{1}" -f $md, $src) }
        }
        if ([int]("0" + $r.images) -lt [int]("0" + $r.sourceImages)) {
            # The exporter keeps every OneNote original in resources\originals when Word left images out
            if ($r.notices -match 'OneNote images kept') { $info += ("images {0}/{1} (originals kept)" -f $r.images, $r.sourceImages) }
            else { $content += ("images {0}/{1}" -f $r.images, $r.sourceImages) }
        }
        if ([int]("0" + $r.attachments) -lt [int]("0" + $r.sourceFiles)) { $content += ("files {0}/{1}" -f $r.attachments, $r.sourceFiles) }
        if ([int]("0" + $r.unsupported) -gt 0) { $info += ("pdf-only objects {0}" -f $r.unsupported) }
        if ($r.pdfTextChars -ne "" -and $src -ge $MinTextChars -and [int]("0" + $r.pdfTextChars) -lt $src * $MinTextRatio) {
            if ($r.snapshot -eq "yes") { $info += ("pdf text {0}/{1} (HTML snapshot made)" -f $r.pdfTextChars, $src) }
            else { $content += ("pdf text {0}/{1}" -f $r.pdfTextChars, $src) }
        }
        if ($r.status -eq "fallback") { $info += "fallback export (raw text + PDF)" }
        elseif ($r.rawTextAdded -eq "yes" -and -not ($info -match 'raw text')) { $info += "raw text added" }
    }
    # The report says the PDF is there: is it still on disk?
    if ($r -and $result -eq "exported" -and $r.pdf -eq "ok" -and -not (Test-Path -LiteralPath ([IO.Path]::ChangeExtension($mdFull, ".pdf")))) {
        $content += "pdf file missing"
    }

    [pscustomobject]@{
        Result      = $result
        Content     = ($content -join ", ")
        Info        = ($info -join ", ")
        MatchedBy   = $how
        Notebook    = $p.Notebook
        SectionPath = $p.SectionPath
        Section     = $p.Section
        Page        = $p.Page
        Created     = $p.Created
        HasInk      = ([int]("0" + $p.InkElements) -gt 0)
        Status      = if ($r) { $r.status } else { "" }
        Pdf         = if ($r) { $r.pdf } else { "" }
        InkSvg      = if ($r) { $r.inkSvg } else { "" }
        MdPath      = $mdFull
        Messages    = if ($r) { $r.messages } else { "" }
        TextRatio   = if ($r -and $r.sourceTextChars -and [int]$r.sourceTextChars -gt 0) { [math]::Round([int]("0" + $r.mdTextChars) / [int]$r.sourceTextChars, 2) } else { "" }
        PageId      = $p.PageId
    }
}

$rows | Export-Csv -LiteralPath (Join-Path $OutDir "verify_pages.csv") -NoTypeInformation -Encoding $csvEncoding -Delimiter ";"
function IsPdfIssue($row) { $row.Result -eq "exported" -and ($row.Pdf -eq "failed" -or ($row.HasInk -and $row.Pdf -ne "ok" -and $row.InkSvg -ne "ok")) }
$problems = $rows | Where-Object { $_.Result -ne "exported" -or $_.Content -or (IsPdfIssue $_) }
$problems | Export-Csv -LiteralPath (Join-Path $OutDir "verify_problems.csv") -NoTypeInformation -Encoding $csvEncoding -Delimiter ";"

$sections = $rows | Group-Object Notebook, SectionPath, Section | ForEach-Object {
    $g = $_.Group
    [pscustomobject]@{
        Notebook  = $g[0].Notebook
        Section   = (@($g[0].SectionPath, $g[0].Section) | Where-Object { $_ }) -join "/"
        Pages     = $g.Count
        Exported  = @($g | Where-Object Result -eq "exported").Count
        Failed    = @($g | Where-Object Result -eq "failed").Count
        Missing   = @($g | Where-Object { $_.Result -eq "missing" -or $_.Result -eq "md-missing" }).Count
        PdfFailed = @($g | Where-Object Pdf -eq "failed").Count
    }
}
$sections | Export-Csv -LiteralPath (Join-Path $OutDir "verify_sections.csv") -NoTypeInformation -Encoding $csvEncoding -Delimiter ";"

# Pages exported but not in the pre-scan (pre-scan older than the export, or other notebook copy)
$extra = @($byId.Values | Where-Object { -not $matchedIds.ContainsKey($_.oneNoteId) }).Count

# ---------------------------------------------------------------------------
# Summary
# ---------------------------------------------------------------------------
$total    = @($rows).Count
$exported = @($rows | Where-Object Result -eq "exported").Count
$failed   = @($rows | Where-Object Result -eq "failed").Count
$missing  = @($rows | Where-Object Result -eq "missing").Count
$mdMiss   = @($rows | Where-Object Result -eq "md-missing").Count
$byTitle  = @($rows | Where-Object MatchedBy -eq "title").Count
$contentIssues = @($rows | Where-Object { $_.Content }).Count
$pdfIssues = @($rows | Where-Object { IsPdfIssue $_ }).Count
$covered = @($rows | Where-Object { $_.Info -and -not $_.Content }).Count
$dupes = @($byId.Values | Where-Object { $_.contentHash } | Group-Object contentHash | Where-Object { $_.Count -gt 1 })
if ($dupes.Count) {
    $dupes | ForEach-Object { $_.Group | Select-Object @{n="contentHash";e={$_.contentHash}}, notebook, sectionPath, page } |
        Export-Csv -LiteralPath (Join-Path $OutDir "verify_duplicates.csv") -NoTypeInformation -Encoding $csvEncoding -Delimiter ";"
}
$pdfOnly  = @($rows | Where-Object { $_.Info -match 'pdf-only' }).Count

Write-Host ""
Write-Host "=== Sections with gaps ===" -ForegroundColor Cyan
$sections | Where-Object { $_.Exported -lt $_.Pages } | Sort-Object Notebook, Section | Format-Table -AutoSize

Write-Host ("Pages in OneNote : {0}" -f $total)
Write-Host ("Exported         : {0}" -f $exported) -ForegroundColor Green
if ($failed)  { Write-Host ("Failed           : {0}" -f $failed) -ForegroundColor Red }
if ($missing) { Write-Host ("Missing          : {0}  (no row in any export report)" -f $missing) -ForegroundColor Red }
if ($mdMiss)  { Write-Host ("md file missing  : {0}  (reported, but file not on disk)" -f $mdMiss) -ForegroundColor Red }
if ($contentIssues) { Write-Host ("Content gaps     : {0}  (less text, images or files than in OneNote, see Content column)" -f $contentIssues) -ForegroundColor Red }
if ($pdfIssues) { Write-Host ("PDF problems     : {0}  (PDF failed, or ink page with neither PDF nor SVG)" -f $pdfIssues) -ForegroundColor Red }
if ($covered) { Write-Host ("Gaps covered     : {0}  (raw text, HTML snapshot or original images kept: nothing lost, see Info column)" -f $covered) -ForegroundColor Yellow }
if ($dupes.Count) { Write-Host ("Duplicate pages  : {0} groups with identical text (verify_duplicates.csv, informational)" -f $dupes.Count) -ForegroundColor Yellow }
if ($pdfOnly) { Write-Host ("PDF-only content : {0}  (pages with objects only the PDF shows)" -f $pdfOnly) -ForegroundColor Yellow }
if ($locked.Count) {
    Write-Host ("Sections NOT exported : {0}  (locked = password protected: unlock in OneNote; error = OneNote could not list the pages. Then run again with --resume)" -f $locked.Count) -ForegroundColor Red
    $locked | ForEach-Object { Write-Host ("  - {0} ({1})" -f $_.sectionPath, $_.status) -ForegroundColor Red }
}
if ($byTitle) { Write-Host ("Matched by title : {0}  (IDs differ: pre-scan from another copy?)" -f $byTitle) -ForegroundColor Yellow }
if ($extra)   { Write-Host ("Not in pre-scan  : {0}  (exported pages the pre-scan does not know)" -f $extra) -ForegroundColor Yellow }
Write-Host ("Details in {0}" -f $OutDir)

if ($exported -eq $total -and $contentIssues -eq 0 -and $pdfIssues -eq 0 -and $locked.Count -eq 0) { exit 0 } else { exit 1 }
