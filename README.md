**OneNote Md Exporter** is a console application running on Windows that exports your OneNote notebooks in different markdown formats.

This tool is usefull to :
* migrate from OneNote to an other note management app based on markdown file formats
* backup your OneNote Notebooks in an interoperable and open file format
  
# Requirements

The tool requires OneNote and Word from the Microsoft Office suite. 
[OneNote for Windows 10](https://www.microsoft.com/en-us/p/onenote-for-windows-10/9wzdncrfhvjl) is not supported.

# Getting started

1. Download the last release of OneNoteMdExporter from [Releases page](https://github.com/alxnbl/onenote-md-exporter/releases)
2. Extract the content of the Zip archive
3. Launch OneNote and be sure that notebooks to export are opened
4. Export your notebooks :
   * Start `OneNoteMdExporter.exe`
   * Select the Notebook to export
   * Select the export format
   * Go take a coffee ☕
   * At the end of the export the export folder is displayed

Command line is supported, run `OneNoteMdExporter.exe --help` for instructions.

# Export formats supported

## Markdown

* Pages exported as md files (GitHub Flavor)
* Sections and section groups exported as a folder hierarchy :
   ```yaml
   Notebook1
   - Section1
     - Section1.1
       - Page1.md
       - Page2.md
       - Page3.md
   - Section2
   ```

## Markdown + Front Matter

* Similar to Markdown export, but includes a header at the beginning of each md file.
    ```yaml
    ---
    title: My page title
    updated: 2021-11-11 14:55:00Z
    created: 2021-11-11 14:54:43Z
    ---
    My page content
    ```

## Joplin Raw Directory

* Import format of the  [Joplin](https://github.com/laurent22/joplin) app
* More details [available here](/doc/migration-to-joplin.md)

# Features and limitations

| Export format: | Markdown | Joplin |
| --- | --- | --- |
| Hierarchy of sections | ✅ Folder hierarchy | ✅ Notbook hierarchy |
| Page ordering inside a section | 🔴 Lost | ✅ |
| Page hierarchy (level) | ✅ | ✅ |
| Attachments  | 🟠 File of certain extensions are lost (wma, wmv, mpg, svg, mp3)  | 🟠 File of certain extensions are lost (wma, wmv, mpg, svg, mp3) |
| Image  | ✅ | ✅ |
| Table  | ✅ | ✅ |
| Table with nested image  | 🟠 Html table, image lost | 🟠 Html table, image lost |
| Font color and background color  | 🔴 | 🔴 |
| Drawing | 🟠 Flattened as image | 🟠 Flattened as image |
| Handwriting  | 🔴 Lost | 🔴 Lost |
| Text tags (task, star...)  | 🔴 Lost | 🔴 Lost |
| Password protected sections | 🔴 Lost | 🔴 Lost |
| Notebook internal link | 🔴 onenote:// url | 🔴 onenote:// url |

# Technical characteristics

* DotNet 5 self-contained console application
* Export page as DocX and translate them in Markdown using PanDoc
* Offline : no call to Microsoft cloud
* Based on Office Interop APIs
* Apply some post-processing based on Regex to correct formatting issues

# Licence

Released under the GPL, version 3.

This software carries no warranty of any kind. Some data can be lost during the export process. I recommend to review your notes after export and keep a backup of your OneNote notebooks just in case.

## Pandoc licence terms

OneNote Md Exporter uses PanDoc universal markup converter.

Pandoc is available at https://github.com/jgm/pandoc

Pandoc is released under the following licence terms, full licence details can be found on the pandoc site.
```
© 2006-2021 John MacFarlane (jgm@berkeley.edu). Released under the GPL, version 2 or greater. This software carries no warranty of any kind.
```

# Build sources

* Install DotNet 5 : https://dotnet.microsoft.com/download/dotnet/5.0
* Clone this repository
* Extract `pandoc.exe` from `pandoc-<Version>-windows-x86_64.zip` from `/src/OneNoteMdExporter/pandoc/` folder
* Build using Visual Studio 2019 or MSBUILD.exe (`dotnet build` do not currently support COMReference : https://aka.ms/msbuild/MSB4803) 

# Contribute

Bugs and enhancements can be reported under: https://github.com/alxnbl/onenote-md-exporter/issues.

Contribution are welcome, please open a PR.

## Translations

You can contribute by adding translation in your language. You just need to :
* fork the repo
* copy file `/src/Resources/trad.en.json`, translate it, and save the new file into `trad.<TwoLetterLanguageCode>.json`  (list of language code [here](https://en.wikipedia.org/wiki/List_of_ISO_3166_country_codes)).
* send a [pull-request](https://docs.github.com/en/github/collaborating-with-issues-and-pull-requests/creating-a-pull-request) 

## Implement a new Export format

[Follow this guide](/doc/add-an-export-format.md).
