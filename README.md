**OneNote Md Exporter** is a console application running on Windows that exports your OneNote notebooks in different markdown formats. Currently, **Joplin markdown directory** and **Markdown directory** are supported.

The first objective of this tool is to offer to OneNote users the most simple and lossless solution to migrate to Joplin.
It offers an alternative to migration based on EverNote export (OneNote-> ENEX -> Joplin), in particular for people organizing their notes using hierarchy of sections and hierarchy of pages, lost during EverNote import.

# Requirements

The software requires OneNote and Word from the Microsoft Office suite. 
[OneNote for Windows 10](https://www.microsoft.com/en-us/p/onenote-for-windows-10/9wzdncrfhvjl) is not supported.

Tested on : 
* Windows 7, 10
* Office 2016
* Joplin 1.6
* PanDoc 2.11

# Export to Joplin

Comparison between OneNote Md Exporter and ENEX Export methods. Choose the one best suited to your notes.

| | OneNote Md Exporter | ENEX Export |
| --- | --- | --- |
| Formatted content | ✅ | ✅ |
| **Hierarchy of sections** | ✅ Sub-Notebooks | 🟠 Flattened as Tag |
| **Page order inside a section** | ✅ | 🔴 All pages part of a single Notebook |
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

* Download the last release of OneNoteMdExporter from [Releases page](https://github.com/alxnbl/onenote-md-exporter/releases)
* Unzip OneNote Md Exporter

## Usage

1. Open OneNote
   * Launch OneNote and be sure that notebooks to export are opened
2. Export
  * From OneNoteMdExporter folder, start `alxnbl.OneNoteMdExporter.exe`
   * Choose the Notebook to export
   * Choose the destination format
   * Go take a coffee ☕
3. [For Joplin Users] Import
   * From Joplin windows app, File > Import > "RAW - Joplin Export Directory"

In case of error during export very that:
* OneNote and Word are correctly installed
* OneNote is open before running the tool
* Both OneNote and the tool are NOT run as administrator
* PanDoc is correctly installed

## Command line

Run `alxnbl.OneNoteMdExporter.exe --help` for instructions.

## Build sources

* Install DotNet 5 : https://dotnet.microsoft.com/download/dotnet/5.0
* Install PanDoc : https://pandoc.org/installing.html
* Clone this repository
* Build using Visual Studio 2019 or MSBUILD.exe (`dotnet build` do not currently support COMReference : https://aka.ms/msbuild/MSB4803) 

## Translation

You can contribute by adding translation in your language. You just need to :
* fork the repo
* copy file `/src/Resources/trad.en.json`, translate it, and save the new file into `trad.<TwoLetterLanguageCode>.json`  (list of language code [here](https://en.wikipedia.org/wiki/List_of_ISO_3166_country_codes)).
* send a [pull-request](https://docs.github.com/en/github/collaborating-with-issues-and-pull-requests/creating-a-pull-request) 

# Technical characteristics

* DotNet 5 self-contained console application
* Offline : no call to Microsoft cloud
* Load Notebook tree using Office Interop APIs
* Export page as DocX and translate them in Md (GitHub Flavor) using PanDoc
* Apply some post-processing based on Regex to correct formatting issues
* Extensible : new export format can be easily added to the code
* Beginning of integration test (need to be completed)

# Licence

Released under the GPL, version 3.

This software carries no warranty of any kind. Some data can be lost during the export process. I recommend to review your notes after export and keep a backup of your OneNote notebooks just in case.

# Pandoc licence terms

OneNote Md Exporter uses PanDoc universal markup converter.

Pandoc is available at https://github.com/jgm/pandoc

Pandoc is released under the following licence terms, full licence details can be found on the pandoc site.
```
© 2006-2021 John MacFarlane (jgm@berkeley.edu). Released under the GPL, version 2 or greater. This software carries no warranty of any kind.
```

#  Contributions

Bugs and enhancements can be reported under: https://github.com/alxnbl/onenote-md-exporter/issues.

Contribution are welcome, please open a PR.
