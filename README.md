**OneNode Md Export** is a console application running on Windows that exports your OneNote notebooks in different markdown formats. Currently, **Joplin markdown directory** and **Markdown directory** are supported.

The first objective of this tool is to offer to OneNote users the most simple and lossless solution to migrate to Joplin.
It offers an alternative to migration based on EverNote export (OneNote-> ENEX -> Joplin), in particular for people organizing their notes using hierarchy of sections and hierarchy of pages, lost during EverNote import.

# Requirements

The tool requires Microsoft Office to be installed, [OneNote for Windows 10](https://www.microsoft.com/en-us/p/onenote-for-windows-10/9wzdncrfhvjl) is not supported.

Tested on : 
* Windows 10
* Office 2016
* Joplin 1.6
* PanDoc 2.11

I have not yet tested the tool with Office 365. Please let me know if you did.

# Export to Joplin

Comparison between OneNote Md Exporter and ENEX Export methods. Choose the one best suited to your notes.

| | OneNote Md Exporter | ENEX Export |
| --- | --- | --- |
| Formatted content | ✅ | ✅ |
| **Hierarchy of sections** | ✅ Sub-Notebooks | 🟠 Flattened as Tag |
| **Page order inside a section** | ✅ Sub-Notebooks | 🔴 All pages part of a single Notebook |
| **Page hierarchy (level)** | ✅ Page title prefix <br/>(--- \<Page\>) | 🔴 |
| Attachments  | ✅ At their original position | 🟠 At the end of the page |
| Note metadata | ✅ | ✅ |
| Image  | ✅ | ✅ |
| Table  | ✅ | ✅ |
| Table with nested image  | 🟠 Html table, image lost | ✅ Markdown |
| Page Header | ✅ Note title and date removed from beginning of pages | 🟠 Note title in both Joplin note title and md file |
| Page Footer | ✅ No footer | 🟠 "Created by OneNote" |
| Mark (\<mark\>)  | 🔴 | 🔴 |
| Drawing | 🟠 Flattened as image | 🟠 Flattened as image |
| Handwriting  | 🔴 Lost | 🟠 Flattened as image |
| Text tags (task, star...)  | 🔴 Lost | ✅ Image |
| Formula  | ? | ? |
| Revision history | 🔴 Lost | 🔴 Lost |
| Password protected sections | 🔴 Lost | 🔴 Lost |
| Notebook internal link | 🔴 onenote:// url | 🔴 onenote:// url |

<br/>

# User guide

## Installation

* Install PanDoc : https://pandoc.org/installing.html
* Download the last release of OneNoteMdExporter from [Releases page](https://github.com/alxnbl/onenote-md-exporter/releases)
* Unzip OneNote Md Exporter

## Usage

1. Open OneNote
   * Launch OneNote and be sure that notebooks to export are opened
2. Export
  * From OneNoteMdExporter folder, start *alxnbl.OneNoteMdExporter.exe*
   * Choose the Notebook to export
   * Choose the destination format
   * Go take a coffee ☕
3. [For Joplin Users] Import
   * From Joplin windows app, File > Import > "RAW - Joplin Export Directory"


## Build sources

* Install DotNet 5 : https://dotnet.microsoft.com/download/dotnet/5.0
* Install PanDoc : https://pandoc.org/installing.html
* Clone this repository
* From src folder, run `dotnet build` then `dotnet run`

# Technical characteristics

* DotNet 5 console application
* Offline : no call to Microsoft cloud
* Load Notebook tree using Office Interop APIs
* Export page as DocX and translate them in Md (GitHub Flavor) using PanDoc
* Apply some post-processing based on Regex to correct formatting issues
* Extensible : new export format can be easily added to the code

# Disclaimer

Some data can be lost during the export process. I recommand you to review your notes after Joplin import and keep a backup of your OneNote notebooks just in case.

#  Contributions

Bugs and enhancements can be reported under: https://github.com/alxnbl/onenote-md-exporter/issues. 

Contribution are welcome, please open a PR.
