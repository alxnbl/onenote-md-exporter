**OneNode Md Export** is a console application running on Windows that exports your OneNote notebooks in different markdown formats. Currently, **Joplin markdown directory** and **Markdown directory** are supported.

The main objective of this repository is to offer to OneNote users the most simple and lossless solution to migrate to Joplin.
It offers an alternative to migration based on EverNote export (OneNote-> ENEX -> Joplin), in particular for people organizing their notes using hierarchy of sections and hierarchy of pages, lost during EverNote import.

# Requirements

Tested on : 
* Windows 10
* Office 2016
* Joplin 1.6

Please tell me if you have tested the tool on other versions.

# Import to Joplin

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

* Install DotNet 5 : https://dotnet.microsoft.com/download/dotnet/5.0
* Install PanDoc : https://pandoc.org/installing.html
* Unzip OneNote Md Exporter

## Usage

1. Open OneNote
   * Launch OneNote and be sure that notebooks to export are opened
2. Export
   * Start OneNoteMdExporter.exe
   * Choose the Notebook to export
   * Choose the destination format
   * Go take a coffee ☕
3. [For Joplin Users] Import
   * From Joplin windows app, File > Import > "RAW - Joplin Export Directory"


# Technical characteristics

* DotNet 5 console application
* Offline : no call to Microsoft cloud
* Load Notebook tree using Office Interop APIs
* Export page as DocX and translate them in Md (GitHub Flavor) using PanDoc
* Apply some post-processing based on Regex to correct some formatting issues
* Customizable : post-processing can be disabled in settings
* Extensible : new export format can be easily added to the code

# Disclaimer

⚠️ Future Joplin version may introduce modifications in the Joplin RAW Export Directory format preventing import with this tool to work correctly. If it appens please open an issue, I will do my best to keep the tool up-to-date. 

#  Contributions

In case of error or undesired behavior, please open an issue. 

Contribution are welcome, please open a PR.
