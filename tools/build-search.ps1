<#
.SYNOPSIS
    Builds an offline search page for the archive: <ExportRoot>\_search\index.html

.DESCRIPTION
    No app, no server, no internet: open index.html in any browser and search the full text of every page
    (titles, text, tags, recognized handwriting and OCR text that the export added). Results link to the
    page's Markdown file, its PDF and its HTML snapshot.
    Uses MiniSearch (MIT licence, bundled in tools\vendor). Rerun after each export to refresh the index.

.USAGE
    powershell -ExecutionPolicy Bypass -File .\tools\build-search.ps1 -ExportRoot "C:\Notes\OneNote-Archive"
#>
param(
    [Parameter(Mandatory = $true)][string]$ExportRoot,
    # Characters of text per page kept in the index (keeps the page fast; titles and tags are always complete)
    [int]$MaxTextPerPage = 20000
)

$ErrorActionPreference = "Stop"
# Convert-Path: no wildcard reading of [ ], and a plain path for UNC shares (Resolve-Path gives "Microsoft.PowerShell.Core\FileSystem::\\nas\...")
$ExportRoot = (Convert-Path -LiteralPath $ExportRoot).TrimEnd('\')
$outDir = Join-Path $ExportRoot "_search"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$vendor = Join-Path $PSScriptRoot "vendor\minisearch.umd.js"
if (-not (Test-Path $vendor)) { throw "Missing $vendor" }

# YAML scalar as written by the exporter: 'single quoted' ('' = one quote), "double quoted", or plain
function YamlValue([string]$v) {
    $v = $v.Trim()
    if ($v -match "^'(.*)'$") { return $Matches[1].Replace("''", "'") }
    if ($v -match '^"(.*)"$') { return $Matches[1].Replace('\"', '"').Replace('\\', '\') }
    return $v
}

function RelUrl([string]$full) {
    $rel = $full.Substring($ExportRoot.Length).TrimStart('\').Replace('\', '/')
    "../" + ((($rel -split '/') | ForEach-Object { [uri]::EscapeDataString($_) }) -join '/')
}

$files = Get-ChildItem -LiteralPath $ExportRoot -Recurse -Filter *.md -File |
    Where-Object { $_.FullName -notmatch '\\_search\\' -and $_.Name -ne "VAULT.md" }
Write-Host ("Indexing {0} pages..." -f $files.Count) -ForegroundColor Cyan

$docs = New-Object System.Collections.Generic.List[object]
$i = 0
foreach ($f in $files) {
    $i++
    if ($i % 200 -eq 0) { Write-Progress -Activity "Indexing" -Status "$i / $($files.Count)" -PercentComplete (100 * $i / $files.Count) }
    $raw = [System.IO.File]::ReadAllText($f.FullName)

    # Front matter
    $title = $f.BaseName; $date = ""; $tags = ""; $notebook = ""; $section = ""
    # Ordinal: with culture rules (PowerShell 7 / ICU) "`r`n" is one unit and "`n---" is never found after a CRLF line
    if ($raw.StartsWith("---", [StringComparison]::Ordinal)) {
        $end = $raw.IndexOf("`n---", 3, [StringComparison]::Ordinal)
        if ($end -gt 0) {
            $fm = $raw.Substring(3, $end - 3)
            $raw = $raw.Substring($end + 4)
            if ($fm -match '(?m)^title:\s*(.+?)\s*$') { $title = YamlValue $Matches[1] }
            if ($fm -match '(?m)^titleDate:\s*(.+?)\s*$') { $date = YamlValue $Matches[1] }
            elseif ($fm -match '(?m)^created:\s*(\S+)') { $date = $Matches[1].Substring(0, [math]::Min(10, $Matches[1].Length)) }
            if ($fm -match '(?m)^notebook:\s*(.+?)\s*$') { $notebook = YamlValue $Matches[1] }
            if ($fm -match '(?m)^sectionPath:\s*(.+?)\s*$') { $section = YamlValue $Matches[1] }
            $tagList = [regex]::Matches($fm, '(?ms)^tags:\s*\n((?:\s*-\s*.+\n?)+)')
            if ($tagList.Count) { $tags = (($tagList[0].Groups[1].Value -split "`n") | ForEach-Object { $_.Trim().TrimStart('-').Trim() } | Where-Object { $_ }) -join " " }
            if ($fm -match '(?m)^type:\s*index') { continue }
        }
    }

    # Plain text: drop images, keep link text, drop html and markdown symbols
    $text = $raw -replace '!\[[^\]]*\]\([^)]*\)', ' ' -replace '\[([^\]]*)\]\([^)]*\)', '$1' -replace '<[^>]+>', ' ' -replace '[#*_`>|~=]+', ' ' -replace '\s+', ' '
    if ($text.Length -gt $MaxTextPerPage) { $text = $text.Substring(0, $MaxTextPerPage) }

    $pdf = [System.IO.Path]::ChangeExtension($f.FullName, ".pdf")
    $snap = [System.IO.Path]::ChangeExtension($f.FullName, ".snapshot.html")
    $docs.Add([ordered]@{
        id = $i; t = $title; d = $date; n = $notebook; s = $section; g = $tags; x = $text.Trim()
        md = RelUrl $f.FullName
        pdf = if (Test-Path -LiteralPath $pdf) { RelUrl $pdf } else { "" }
        snap = if (Test-Path -LiteralPath $snap) { RelUrl $snap } else { "" }
    })
}
Write-Progress -Activity "Indexing" -Completed

$json = $docs | ConvertTo-Json -Depth 3 -Compress
if ($docs.Count -eq 1) { $json = "[$json]" }
$utf8 = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText((Join-Path $outDir "data.js"), "window.ARCHIVE_DOCS = $json;", $utf8)
Copy-Item $vendor (Join-Path $outDir "minisearch.js") -Force
Copy-Item (Join-Path $PSScriptRoot "vendor\minisearch.LICENSE.txt") (Join-Path $outDir "minisearch.LICENSE.txt") -Force

$html = @'
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Archive search</title>
<style>
  :root { --bg:#fbfaf7; --fg:#1d1d1b; --muted:#6b6a65; --line:#e3e0d8; --accent:#9a4b1c; --mark:#f6e3a1; }
  @media (prefers-color-scheme: dark) { :root { --bg:#191917; --fg:#ecebe6; --muted:#a3a19a; --line:#34332f; --accent:#e39a68; --mark:#6b5a1e; } }
  * { box-sizing: border-box; }
  body { margin:0; background:var(--bg); color:var(--fg); font:16px/1.5 Georgia, "Iowan Old Style", serif; }
  header { position:sticky; top:0; background:var(--bg); border-bottom:1px solid var(--line); padding:18px 16px 12px; }
  .wrap { max-width:860px; margin:0 auto; }
  h1 { font:600 13px/1 system-ui, sans-serif; letter-spacing:.12em; text-transform:uppercase; color:var(--muted); margin:0 0 10px; }
  input, select { font:inherit; color:inherit; background:transparent; border:1px solid var(--line); border-radius:6px; padding:10px 12px; }
  input { width:100%; font-size:20px; }
  .row { display:flex; gap:8px; margin-top:8px; flex-wrap:wrap; font:14px system-ui, sans-serif; color:var(--muted); align-items:center; }
  main { padding:8px 16px 60px; }
  .hit { padding:16px 0; border-bottom:1px solid var(--line); }
  .hit a.title { color:var(--fg); font-size:19px; text-decoration:none; }
  .hit a.title:hover { text-decoration:underline; }
  .meta { font:13px system-ui, sans-serif; color:var(--muted); margin:2px 0 6px; }
  .meta a { color:var(--accent); margin-right:10px; }
  .snip { color:var(--fg); opacity:.85; }
  mark { background:var(--mark); color:inherit; padding:0 1px; }
</style>
</head>
<body>
<header><div class="wrap">
  <h1>OneNote archive &middot; offline search</h1>
  <input id="q" placeholder="Search all pages..." autofocus>
  <div class="row"><select id="nb"><option value="">All notebooks</option></select><span id="count"></span></div>
</div></header>
<main><div class="wrap" id="out"></div></main>
<script src="minisearch.js"></script>
<script src="data.js"></script>
<script>
const docs = window.ARCHIVE_DOCS || [];
const ms = new MiniSearch({ fields: ['t','x','g','s'], storeFields: ['t','d','n','s','md','pdf','snap','x'],
  searchOptions: { boost: { t: 3, g: 2, s: 1.5 }, prefix: true, fuzzy: 0.15, combineWith: 'AND' } });
ms.addAll(docs);
const nb = document.getElementById('nb'), q = document.getElementById('q'), out = document.getElementById('out'), count = document.getElementById('count');
[...new Set(docs.map(d => d.n).filter(Boolean))].sort().forEach(n => nb.add(new Option(n, n)));
const esc = s => s.replace(/[&<>"]/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;'}[c]));
function snippet(text, terms) {
  const lower = text.toLowerCase(); let pos = -1;
  for (const t of terms) { const p = lower.indexOf(t.toLowerCase()); if (p >= 0 && (pos < 0 || p < pos)) pos = p; }
  const start = Math.max(0, pos - 80), s = (start ? '\u2026' : '') + text.substr(start, 260) + '\u2026';
  // Highlight on the raw text, then escape each piece (replacing inside escaped HTML broke "&amp;" and the <mark> tags)
  const ts = terms.filter(t => t.length > 1).sort((a, b) => b.length - a.length).map(t => t.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'));
  if (!ts.length) return esc(s);
  return s.split(new RegExp('(' + ts.join('|') + ')', 'gi')).map((part, i) => i % 2 ? '<mark>' + esc(part) + '</mark>' : esc(part)).join('');
}
function run() {
  const text = q.value.trim();
  if (!text) { out.innerHTML = ''; count.textContent = docs.length + ' pages indexed'; return; }
  let res = ms.search(text, { filter: r => !nb.value || r.n === nb.value });
  count.textContent = res.length + ' result' + (res.length === 1 ? '' : 's');
  out.innerHTML = res.slice(0, 200).map(r => `<div class="hit">
    <a class="title" href="${r.md}">${esc(r.t || '(untitled)')}</a>
    <div class="meta">${esc([r.d, r.n, r.s].filter(Boolean).join(' \u00b7 '))}
      ${r.pdf ? `<a href="${r.pdf}">PDF</a>` : ''}${r.snap ? `<a href="${r.snap}">Snapshot</a>` : ''}<a href="${r.md}">Text</a></div>
    <div class="snip">${snippet(r.x || '', r.terms)}</div></div>`).join('');
}
q.addEventListener('input', run); nb.addEventListener('change', run); run();
</script>
</body>
</html>
'@
[System.IO.File]::WriteAllText((Join-Path $outDir "index.html"), $html, $utf8)
Write-Host ("Search page ready: {0}" -f (Join-Path $outDir "index.html")) -ForegroundColor Green
