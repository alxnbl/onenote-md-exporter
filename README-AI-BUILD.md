# OneNote Md Exporter — Build & Install Guide (for AI agents)

This document guides AI agents (or humans) through compiling and installing
**OneNoteMdExporter** from source on a clean Windows machine. It contains **no
machine-specific paths** — replace every `<placeholder>` with the actual value on
the machine you are working on.

---

## 1. What this project is

A Windows console application that exports OneNote notebooks into Markdown (or Joplin)
format. Internally it:

- Talks to **OneNote / Word** through the Microsoft Office COM interop APIs.
- Uses **Pandoc** to convert each page exported as `.docx` into Markdown.
- Targets **.NET 10**, builds as a self-contained console app.

---

## 2. Repository layout

| Path | Purpose |
|---|---|
| `src/OneNoteMdExporter.sln` | Solution containing the app + integration tests |
| `src/OneNoteMdExporter/OneNoteMdExporter.csproj` | Main executable project |
| `src/OneNoteMdExporter/pandoc/` | Pandoc binaries must be placed here before building |
| `src/OneNoteMdExporter/libs/` | Pre-generated `Microsoft.Office.Interop.OneNote.dll` |
| `src/OneNoteMdExporter.IntTests/` | NUnit integration tests (optional to build) |

---

## 3. Prerequisites

| Requirement | Notes |
|---|---|
| Windows 10+ | Required |
| OneNote 2013+ (desktop app, NOT the Windows Store version) | Required at **runtime** |
| Microsoft Word 2013+ | Required at **runtime** (via COM Interop) |
| .NET 10 SDK | Required at **build time** |

> **IMPORTANT** — The plain `dotnet build` command does **not** support
> `<COMReference>` (error MSB4803). This repo avoids that by referencing a
> pre-generated interop DLL (section 5.2), so **both** `dotnet build` and
> `MSBuild.exe` work.

---

## 4. Install the .NET 10 SDK

Download and install the .NET 10 SDK from <https://dotnet.microsoft.com/download/dotnet/10.0>.

### 4.1 Standard install (recommended)

Run the official installer, then open a **new** terminal so `dotnet` is on `PATH`.

### 4.2 Portable / offline layout (if you only have a zip)

You may extract a self-contained SDK zip into any folder, e.g.:

```powershell
# Example: SDK extracted to D:\tools\dotnet-sdk-10.0.400-win-x64
$env:DOTNET_ROOT = "D:\tools\dotnet-sdk-10.0.400-win-x64"
$env:PATH        = "$env:DOTNET_ROOT;" + $env:PATH
```

### 4.3 Verify

```powershell
dotnet --list-sdks   # should print 10.x.x
```

If the SDK's own `MSBuild.exe` cannot start and reports
`You must install .NET to run this application`, set `DOTNET_ROOT` to the SDK folder
(as in 4.2) and retry.

---

## 5. Prepare dependencies (one time, before building)

### 5.1 Pandoc

The csproj expects `pandoc.exe` plus its license files in
`src/OneNoteMdExporter/pandoc/`. The repo usually ships a
`pandoc-<version>-windows-x86_64.zip` in that folder. Unpack it:

```powershell
cd "src\OneNoteMdExporter\pandoc"
Expand-Archive -Path "pandoc-3.8.3-windows-x86_64.zip" -DestinationPath . -Force
```

Expected files in `src/OneNoteMdExporter/pandoc/` before building:
`pandoc.exe`, `COPYING.rtf`, `COPYRIGHT.txt`, `MANUAL.html`.

> These files are copied to the output folder at build time. If they are missing the
> build still succeeds but the app fails at runtime with
> `pandoc.exe not found in \pandoc\ subfolder`.

### 5.2 OneNote interop assembly

`src/OneNoteMdExporter/libs/Microsoft.Office.Interop.OneNote.dll` is committed to the
repo and is all that is needed in most cases. **Only regenerate it if the file is
missing.**

If you must regenerate, run TlbImp from the .NET Framework SDK against the OneNote
executable installed on the machine:

```powershell
& "C:\Program Files (x86)\Microsoft SDKs\Windows\v10.0A\bin\NETFX 4.8 Tools\TlbImp.exe" `
  "<path-to-onenote.exe>" `
  /out:"src\OneNoteMdExporter\libs\Microsoft.Office.Interop.OneNote.dll" `
  /namespace:Microsoft.Office.Interop.OneNote
```

Find the OneNote executable path by reading the registered type library:

```powershell
# OneNote typelib is registered under this GUID:
#   HKLM\SOFTWARE\Classes\TypeLib\{0EA692EE-BB50-4E3C-AEF0-356D91732725}\1.1\0\Win32
# The default value points at something like
#   C:\Program Files\Microsoft Office\Root\Office16\ONENOTE.EXE
```

Alternative: copy the interop DLL already present in the GAC:

```powershell
# Usually present on machines with OneNote installed:
#   C:\Windows\assembly\GAC_MSIL\Microsoft.Office.Interop.OneNote\15.0.0.0__71e9bce111e9429c\Microsoft.Office.Interop.OneNote.dll
```

---

## 6. Build

Use **Release** configuration. All commands must run from the repository root
(the folder containing `src\`).

### Option A — dotnet CLI (simplest)

```powershell
dotnet build "src\OneNoteMdExporter.sln" -c Release
```

### Option B — MSBuild.exe

The .NET SDK ships its own MSBuild. Find it, then build:

```powershell
# Locate the SDK version first (adjust to the installed one):
$sdkRoot = Split-Path (Split-Path (Get-Command dotnet).Source -Parent) -Parent

# Option 1: call the SDK-bundled MSBuild directly
& "$sdkRoot\MSBuild.exe" "src\OneNoteMdExporter.sln" /t:Build /p:Configuration=Release /restore /verbosity:minimal

# Option 2: if Visual Studio Build Tools is installed, use its MSBuild instead:
#   "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe"
```

If MSBuild reports `No .NET SDKs were found`, set `DOTNET_ROOT` to the SDK folder
(see section 4.2) and set `PATH` to include it, then retry.

> If a running `OneNoteMdExporter.exe` locks the output file, stop it first:
> `Stop-Process -Name OneNoteMdExporter -Force`.

### Build output

```
src\OneNoteMdExporter\bin\Release\net10.0-windows7.0\win-x86\
├── OneNoteMdExporter.exe          <- main executable
├── OneNoteMdExporter.dll
├── appSettings.json
├── pandoc\pandoc.exe              <- copied automatically from the pandoc\ folder
├── Resources\*.json               <- translations
└── (self-contained runtime files)
```

The `win-x86` RID comes from `<RuntimeIdentifier>win-x86</RuntimeIdentifier>`. This is
intentional: Word/OneNote COM interop requires a 32-bit host process.

---

## 7. Install / run

### 7.1 Run directly from the build output (dev/test)

```powershell
# OneNote must be running with the target notebooks loaded first.
.\src\OneNoteMdExporter\bin\Release\net10.0-windows7.0\win-x86\OneNoteMdExporter.exe
```

### 7.2 Produce a distributable folder

```powershell
$out = ".\release\OneNoteMdExporter.v1.6.0"
Remove-Item $out -Recurse -Force -ErrorAction SilentlyContinue
Copy-Item "src\OneNoteMdExporter\bin\Release\net10.0-windows7.0\win-x86" $out -Recurse
```

Hand the whole `$out` folder to end users. To zip for distribution:

```powershell
Compress-Archive -Path "$out\*" -DestinationPath "OneNoteMdExporter.v1.6.0.zip"
```

### 7.3 End-user usage

1. Start `OneNoteMdExporter.exe`.
2. Select the notebook to export.
3. Select the export format (Markdown / Joplin).
4. (Optional) edit advanced settings in `appSettings.json`.
5. Export finishes and opens the output folder in Explorer.

CLI mode is supported: `OneNoteMdExporter.exe --help`.

---

## 8. Runtime requirements on the target machine

- Windows 10+
- OneNote 2013+ (desktop). The Windows Store "OneNote for Windows" is **not** supported.
- Word 2013+.

The exporter drives OneNote/Word over COM
(`Microsoft.Office.Interop.OneNote`), so Office must be installed and licensed on the
machine that actually runs the export.

---

## 9. Configuration (`appSettings.json`)

Copied to the output folder at build time. Key settings:

| Setting | Purpose | Default |
|---|---|---|
| `ResourceFolderName` | Folder name for images/attachments | `resources` |
| `AddFrontMatterHeader` | Add YAML front matter to each page | `true` |
| `ResourceFolderLocation` | `RootFolder` (single folder) or `PageParentFolder` (next to each md) | `RootFolder` |
| `PanDocMarkdownFormat` | Markdown flavor passed to pandoc | `gfm` |
| `PostProcessingMdImgRef` | Rewrite pandoc `<img>` into markdown references | `true` |
| `UseHtmlStyling` | Keep HTML styling spans | `true` |
| `KeepOneNoteTempFiles` | Keep temp docx during export (debug) | `false` |

---

## 10. Known build gotchas

1. **`dotnet build` fails with MSB4803 / ResolveComReference** — you are on an old
   branch that still uses `<COMReference>`. This branch replaces it with the `libs\`
   DLL reference, so use this branch (or apply the same change).
2. **`You must install .NET to run this application`** when calling the SDK's MSBuild
   → set `DOTNET_ROOT` to the SDK folder (section 4.2).
3. **`No .NET SDKs were found`** → `DOTNET_ROOT`/`PATH` not pointing at the SDK.
4. **`OneNoteMdExporter.exe` locked during build** → an instance is running; kill it.
5. **`pandoc.exe not found in \pandoc\ subfolder`** at runtime → section 5.1 was skipped.
6. **Empty / missing images in exported Markdown** → known regex bug; always build from
   branch `fix/broken-image-export-regex` (it contains the fix in `ExportServiceBase.cs`).
7. **First restore is slow** (NuGet package download) — this is normal; be patient.

---

## 11. Quick verification checklist (for the AI)

After a successful build:

- [ ] `OneNoteMdExporter.exe` exists under `bin\Release\net10.0-windows7.0\win-x86\`
- [ ] `pandoc\pandoc.exe` exists in that same output folder
- [ ] `appSettings.json` and `Resources\*.json` are present
- [ ] `OneNoteMdExporter.exe --help` prints CLI usage without crashing
- [ ] A test export produces `.md` files whose images resolve to the `resources\` folder
      (no `C:\Users\...\Temp\...\pandoc\media\...` references inside the md files)
