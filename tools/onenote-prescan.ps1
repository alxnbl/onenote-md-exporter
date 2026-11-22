<#
.SYNOPSIS
    OneNote pre-scan: measures scale before export. READ-ONLY.

.DESCRIPTION
    Walks every notebook, section group, section and page visible in desktop OneNote
    via the OneNote COM API and counts what each page contains:
      - ink (InkDrawing, InkWord, InkParagraph)
      - inline images
      - attachments (InsertedFile) incl. cached file size where available
      - media files, tables, approximate text length
    Writes:
      prescan_pages.csv     one row per page
      prescan_sections.csv  one row per section (totals + routing hint)
      prescan_errors.log    pages/sections that could not be read
    Changes nothing in your notebooks. Optional -Sync asks OneNote to sync first.

.USAGE
    1. Open desktop OneNote (not the Store version). Make sure all notebooks are open.
    2. Open Windows PowerShell (5.1) as your normal user, NOT as administrator.
    3. Run:
         powershell -ExecutionPolicy Bypass -File .\onenote-prescan.ps1 -OutDir D:\OneNoteArchive\_manifest
       Add -Sync to trigger a sync of every notebook before scanning.
       Add -IncludeRecycleBin to also scan deleted pages.
       Add -Notebook "Name" (repeatable) to scan only those notebooks; default: every open notebook.

.NOTES
    Runtime: roughly 0.2 to 1 sec per page. 10,000 pages = about 30 min to 3 h.
    Locked (password) sections are listed but cannot be scanned until unlocked.
#>

param(
    [string]$OutDir = (Join-Path (Get-Location) "prescan"),
    [switch]$Sync,
    [switch]$IncludeRecycleBin,
    [string[]]$Notebook = @()
)

$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------
# Setup
# ---------------------------------------------------------------------------
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
$csvEncoding = if ($PSVersionTable.PSVersion.Major -ge 6) { "utf8BOM" } else { "UTF8" }
$PagesCsv    = Join-Path $OutDir "prescan_pages.csv"
$SectionsCsv = Join-Path $OutDir "prescan_sections.csv"
$ErrorLog    = Join-Path $OutDir "prescan_errors.log"
"" | Set-Content -LiteralPath $ErrorLog -Encoding UTF8

function Write-ErrorLog([string]$msg) {
    Add-Content -LiteralPath $ErrorLog -Value ("{0:u}  {1}" -f (Get-Date), $msg) -Encoding UTF8
}

try {
    $OneNote = New-Object -ComObject OneNote.Application
} catch {
    Write-Host "Could not start OneNote COM. Is desktop OneNote installed, and is this shell NOT elevated?" -ForegroundColor Red
    throw
}

$NsUri = "http://schemas.microsoft.com/office/onenote/2013/onenote"
$HS_NOTEBOOKS = 2   # HierarchyScope.hsNotebooks
$HS_PAGES     = 4   # HierarchyScope.hsPages
$PI_BASIC     = 0   # PageInfo.piBasic (no binary data, keeps scan light)
$XS_2013      = 2   # XMLSchema.xs2013

function New-Ns([xml]$doc) {
    $ns = New-Object System.Xml.XmlNamespaceManager($doc.NameTable)
    $ns.AddNamespace("one", $NsUri)
    # XmlNamespaceManager is enumerable: a plain "return" would unroll it into its prefixes
    Write-Output -NoEnumerate $ns
}

# ---------------------------------------------------------------------------
# Optional: sync all notebooks first
# ---------------------------------------------------------------------------
if ($Sync) {
    Write-Host "Requesting sync of all notebooks..." -ForegroundColor Cyan
    $nbXml = ""
    $OneNote.GetHierarchy("", $HS_NOTEBOOKS, [ref]$nbXml, $XS_2013)
    [xml]$nbDoc = $nbXml
    foreach ($nb in $nbDoc.SelectNodes("//one:Notebook", (New-Ns $nbDoc))) {
        try {
            $OneNote.SyncHierarchy($nb.ID)
            Write-Host "  synced: $($nb.name)"
        } catch {
            Write-ErrorLog "SYNC FAILED notebook '$($nb.name)': $($_.Exception.Message)"
        }
    }
    Write-Host "Sync requested. Check File > Info > View Sync Status in OneNote before trusting results."
}

# ---------------------------------------------------------------------------
# Load full hierarchy down to pages
# ---------------------------------------------------------------------------
Write-Host "Reading hierarchy..." -ForegroundColor Cyan
$hierXml = ""
$OneNote.GetHierarchy("", $HS_PAGES, [ref]$hierXml, $XS_2013)
[xml]$hier = $hierXml
$ns = New-Ns $hier

$allSections = @($hier.SelectNodes("//one:Section", $ns))
if ($Notebook.Count -gt 0) {
    $wanted = @($Notebook | ForEach-Object { $_.Trim() })
    $allSections = @($allSections | Where-Object {
        $n = $_.ParentNode
        while ($n -ne $null -and $n.LocalName -ne "Notebook") { $n = $n.ParentNode }
        ($n -ne $null) -and ($wanted -contains $n.name)
    })
    Write-Host ("Notebook filter: {0}" -f ($wanted -join ", "))
}
$totalPages  = 0
foreach ($s in $allSections) { $totalPages += $s.SelectNodes(".//one:Page", $ns).Count }
Write-Host ("Found {0} sections, {1} pages." -f $allSections.Count, $totalPages)

# Build "Notebook/Group/Subgroup" path for a section
function Get-SectionPath($sectionNode) {
    $parts = @()
    $n = $sectionNode.ParentNode
    while ($n -ne $null -and $n.LocalName -ne "Notebooks") {
        if ($n.LocalName -in @("Notebook", "SectionGroup")) { $parts = ,$n.name + $parts }
        $n = $n.ParentNode
    }
    return ($parts -join "/")
}

function Test-InRecycleBin($sectionNode) {
    if ($sectionNode.isInRecycleBin -eq "true" -or $sectionNode.isDeletedPages -eq "true") { return $true }
    $n = $sectionNode.ParentNode
    while ($n -ne $null) {
        if ($n.LocalName -eq "SectionGroup" -and $n.isRecycleBin -eq "true") { return $true }
        $n = $n.ParentNode
    }
    return $false
}

# ---------------------------------------------------------------------------
# Scan pages
# ---------------------------------------------------------------------------
$pageRows    = New-Object System.Collections.Generic.List[object]
$sectionRows = New-Object System.Collections.Generic.List[object]
$done = 0
$started = Get-Date

foreach ($sec in $allSections) {
    $secPath  = Get-SectionPath $sec
    $nbName = ($secPath -split "/")[0]
    $inBin    = Test-InRecycleBin $sec

    if ($inBin -and -not $IncludeRecycleBin) {
        $done += $sec.SelectNodes("one:Page", $ns).Count
        continue
    }

    $secTotals = [ordered]@{
        Notebook = $nbName; SectionPath = $secPath; Section = $sec.name
        Locked = ($sec.locked -eq "true"); Encrypted = ($sec.encrypted -eq "true"); RecycleBin = $inBin
        Pages = 0; PagesWithInk = 0; InkElements = 0; Images = 0; Attachments = 0
        AttachmentMB = 0.0; MediaFiles = 0; Tables = 0; TextChars = 0; PageErrors = 0
        Links = 0; Tags = 0; Meetings = 0; Tasks = 0; Unsupported = 0; MediaIndexes = 0; Collapsed = 0; SubPages = 0
        Route = ""
    }

    if ($sec.locked -eq "true") {
        Write-ErrorLog "LOCKED section skipped: $secPath/$($sec.name) (unlock in OneNote and rerun)"
        $secTotals.Route = "LOCKED - unlock before export"
        $sectionRows.Add([pscustomobject]$secTotals)
        continue
    }

    foreach ($page in $sec.SelectNodes("one:Page", $ns)) {
        $done++
        if ($done % 25 -eq 0 -or $done -eq $totalPages) {
            $elapsed = ((Get-Date) - $started).TotalSeconds
            $eta = if ($done -gt 0) { [TimeSpan]::FromSeconds($elapsed / $done * ($totalPages - $done)) } else { [TimeSpan]::Zero }
            Write-Progress -Activity "Scanning OneNote" -Status ("{0}/{1}  ETA {2:hh\:mm\:ss}  {3}" -f $done, $totalPages, $eta, $secPath) `
                -PercentComplete ([math]::Min(100, 100 * $done / [math]::Max(1, $totalPages)))
        }

        $row = [ordered]@{
            Notebook = $nbName; SectionPath = $secPath; Section = $sec.name
            Page = $page.name; Level = $page.pageLevel
            Created = $page.dateTime; Modified = $page.lastModifiedTime
            InkElements = 0; Images = 0; Attachments = 0; AttachmentMB = 0.0
            MediaFiles = 0; Tables = 0; TextChars = 0
            Links = 0; Tags = 0; Meetings = 0; Tasks = 0; Unsupported = 0; MediaIndexes = 0; Collapsed = 0
            Error = ""; PageId = $page.ID
        }

        try {
            $pageXml = ""
            $OneNote.GetPageContent($page.ID, [ref]$pageXml, $PI_BASIC, $XS_2013)
            [xml]$p = $pageXml
            $pns = New-Ns $p

            $row.InkElements = $p.SelectNodes("//one:InkDrawing | //one:InkWord | //one:InkParagraph", $pns).Count
            $row.Images      = $p.SelectNodes("//one:Image", $pns).Count
            $row.MediaFiles  = $p.SelectNodes("//one:MediaFile", $pns).Count
            $row.Tables      = $p.SelectNodes("//one:Table", $pns).Count
            # Features the export must carry (links between pages, tags, Outlook items, objects the API cannot describe,
            # recording time indexes, collapsed paragraphs): counted so test sections can be chosen and the export checked
            $row.Links        = ([regex]::Matches($pageXml, 'href="onenote:')).Count
            $row.Tags         = $p.SelectNodes("//one:Tag", $pns).Count
            $row.Meetings     = $p.SelectNodes("//one:Meeting", $pns).Count
            $row.Tasks        = $p.SelectNodes("//one:OutlookTask", $pns).Count
            $row.Unsupported  = $p.SelectNodes("//one:FutureObject", $pns).Count
            $row.MediaIndexes = $p.SelectNodes("//one:MediaIndex", $pns).Count
            $row.Collapsed    = $p.SelectNodes("//one:OE[@collapsed='1' or @collapsed='true']", $pns).Count

            $files = $p.SelectNodes("//one:InsertedFile", $pns)
            $row.Attachments = $files.Count
            $bytes = 0
            foreach ($f in $files) {
                $cache = $f.pathCache
                if ($cache -and (Test-Path -LiteralPath $cache)) { $bytes += (Get-Item -LiteralPath $cache).Length }
            }
            $row.AttachmentMB = [math]::Round($bytes / 1MB, 2)

            $chars = 0
            foreach ($t in $p.SelectNodes("//one:T", $pns)) {
                # T contains HTML-ish CDATA; strip tags for a rough count
                $chars += ([regex]::Replace($t.InnerText, "<[^>]+>", "")).Length
            }
            $row.TextChars = $chars
        } catch {
            $row.Error = $_.Exception.Message
            $secTotals.PageErrors++
            Write-ErrorLog "PAGE FAILED: $secPath/$($sec.name)/$($page.name)  [$($page.ID)]  $($_.Exception.Message)"
        }

        $pageRows.Add([pscustomobject]$row)

        $secTotals.Pages++
        if ($row.InkElements -gt 0) { $secTotals.PagesWithInk++ }
        $secTotals.InkElements  += $row.InkElements
        $secTotals.Images       += $row.Images
        $secTotals.Attachments  += $row.Attachments
        $secTotals.AttachmentMB += $row.AttachmentMB
        $secTotals.MediaFiles   += $row.MediaFiles
        $secTotals.Tables       += $row.Tables
        foreach ($k in "Links","Tags","Meetings","Tasks","Unsupported","MediaIndexes","Collapsed") { $secTotals[$k] += $row[$k] }
        if ([int]$page.pageLevel -gt 1) { $secTotals.SubPages++ }
        $secTotals.TextChars    += $row.TextChars
    }

    # Routing hint: share of pages that carry ink
    $inkShare = if ($secTotals.Pages -gt 0) { $secTotals.PagesWithInk / $secTotals.Pages } else { 0 }
    $secTotals.AttachmentMB = [math]::Round($secTotals.AttachmentMB, 2)
    $secTotals.Route = switch ($true) {
        ($secTotals.Pages -eq 0) { "EMPTY"; break }
        ($inkShare -ge 0.5)      { "INK-HEAVY: .one + PDF primary, MD index only"; break }
        ($inkShare -gt 0)        { "MIXED: .one + MD + PDF, link PDF in frontmatter"; break }
        default                  { "TEXT: .one + MD + PDF" }
    }
    $sectionRows.Add([pscustomobject]$secTotals)
}

Write-Progress -Activity "Scanning OneNote" -Completed

# ---------------------------------------------------------------------------
# Output
# ---------------------------------------------------------------------------
$pageRows    | Export-Csv -LiteralPath $PagesCsv    -NoTypeInformation -Encoding $csvEncoding
$sectionRows | Export-Csv -LiteralPath $SectionsCsv -NoTypeInformation -Encoding $csvEncoding

Write-Host ""
Write-Host "=== Summary per notebook ===" -ForegroundColor Cyan
$sectionRows | Group-Object Notebook | ForEach-Object {
    $g = $_.Group
    [pscustomobject]@{
        Notebook     = $_.Name
        Sections     = $g.Count
        Locked       = ($g | Where-Object Locked).Count
        Pages        = ($g | Measure-Object Pages -Sum).Sum
        PagesWithInk = ($g | Measure-Object PagesWithInk -Sum).Sum
        Images       = ($g | Measure-Object Images -Sum).Sum
        Attachments  = ($g | Measure-Object Attachments -Sum).Sum
        AttachMB     = [math]::Round(($g | Measure-Object AttachmentMB -Sum).Sum, 0)
        PageErrors   = ($g | Measure-Object PageErrors -Sum).Sum
    }
} | Format-Table -AutoSize

$errCount = (Get-Content $ErrorLog | Where-Object { $_.Trim() }).Count
Write-Host ("Done in {0:hh\:mm\:ss}. Files in {1}" -f ((Get-Date) - $started), $OutDir) -ForegroundColor Green
if ($errCount -gt 0) { Write-Host "$errCount issue(s) logged in prescan_errors.log" -ForegroundColor Yellow }

[System.Runtime.InteropServices.Marshal]::ReleaseComObject($OneNote) | Out-Null
