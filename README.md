**OneNode Md Export** is a console application running on Windows that exports your OneNote notebooks in different markdown formats. Currently, **Joplin markdown directory** and **Markdown directory** are supported.

The main objective of this repository is to offer the most simple and loseless solution for OneNote users wanting to migrate to Joplin.
It offer an alternative to migration using EverNote export (OneNote-> ENEX -> Joplin) in particuliar for people organizing their notes using hierarchy of sections inside notebooks, and hierarchy of pages inside a sections.

Technical characteristics :
* Based on DotNet 5, Office Interop API, PanDoc
* 100% offline
* Extensible : new export format can be easily added

# Requirements

Tested on : 
* Platform : Windows 10
* Office 2016
* Joplin 1.6

Please tell me if other versions work for you.

# User guide
## Installation

* Install DotNet 5 : https://dotnet.microsoft.com/download/dotnet/5.0
* Install PanDoc : https://pandoc.org/installing.html
* Unzip OneNote Md Exporter

## Usage

1. Open OneNote
   * Launch OneNote and be sur that notebooks to export are openned
2. Export
   * Start OneNoteMdExporter.exe
   * Choose the Notebook to export
   * Choose the destination format
   * Go take a coffea ☕
3. [For Joplin Users] Import
   * From Joplin windows app, File > Import > "RAW - Joplin Export Directory"

## Joplin export comparison

Comparison between OneNote Md Exporter and ENEX Export methods :

| | OneNoteMdExporter | ENEX Export |
| --- | --- | --- |
| Formated content | ✅ | ✅ |
| Page Header | ✅ Note title removed from page content | 🟠 Note title in both Joplin note title and md file |
| Page Footer | ✅ No footer | 🟠 "Created by OneNote" |
| **Hierarchy of sections** | ✅ Sub-Notebooks | 🟠 Flattened as Tag |
| **Page order inside a section** | ✅ Sub-Notebooks | 🔴 All pages part of a single Notebook |
| **Page hierarchy (level)** | 🟠 Page title prefix <br/>(--- \<Page\>) | 🔴 |
| Note metadata | ✅ | ✅ |
| Image  | ✅ | ✅ |
| Table  | ✅ | ✅ |
| Table with nested image  | 🟠 Html table, image lost | ✅ Markdown |
| Attachements  | 🔴 TODO | ✅ |
| Mark (\<mark\>)  | 🔴 | 🔴 |
| Drawing | 🟠 Flattened as image | 🟠 Flattened as image |
| Pensile text  | 🔴 Lost | 🟠 Flattened as image |
| Formula  | ? | ? |
| Revision history | 🔴 Lost | 🔴 Lost |
| Password protected sections | 🔴 Lost | 🔴 Lost |

# Disclamer

## Choose the right export method for your notes

If you are note using sections and pages hierarchy, or using complex formating, EverNote migration can offer better results for you.

## Joplin format
⚠️ Joplin RAW Export Directory format is not aim at being used by 3rd party applications. Futur Joplin version may introduce modifications  preventing this tool to import correlty notebooks exported with this tool. I will do my best to keep it up-to-date with last joplin version. 

#  Contribution

In case of error or undesired behavior, please open an issue. 

Contribution are welcome, please open a PR.