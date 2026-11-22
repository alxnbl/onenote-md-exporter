**OneNote Md Exporter** is a console application running on Windows that exports your OneNote notebooks in different markdown formats.

This tool is usefull to :
* evaluate or migrate to an alternative Knowledge Management Software like Joplin, Obsidian or other softwares based on Markdown format
* backup your OneNote Notebooks in an interoperable and open file format

The tool is easy to install and use, and can produce better results than other OneNote export methods.

# Requirements

- Windows >=10
- OneNote >= 2013, OneNote for Windows (from the Windows store) is not supported
- Word >= 2013

# Getting started

1. Download the last release of OneNoteMdExporter from the [Releases page](https://github.com/alxnbl/onenote-md-exporter/releases)
2. Extract the content of the Zip archive
3. Launch OneNote and be sure that notebooks to export are loaded
4. Export your notebooks :
   * Start `OneNoteMdExporter.exe`
   * Select the Notebook to export
   * Select the export format
   * [Optional] Edit advanced settings
   * Go take a coffee ☕
   * At the end of the export the export folder is open in Windows file explorer

Command line is supported, run `OneNoteMdExporter.exe --help` for instructions.

# Export formats supported

## Markdown

* Sections and section groups are exported as a folder hierarchy containing pages :
   ```yaml
   Notebook1
   - Section1
     - Section1.1
       - Page1.md
       - Page2.md
   - Section2
   ```
* Settings
  * `ProcessingOfPageHierarchy` : page hierarchy as parent folder of md file (ParentPage\Child Page.md ; default value) or as md file prefix (ParentPage_ChildPage.md)
  * `ResourceFolderLocation` : images and file attachments can be stored in a single folder at the root of the export or in a folder next to each md file
  * `AddFrontMatterHeader` : add a YAML header containing page metadata at the beginning of each md file
    ```yaml
    ---
    title: Page title
    updated: 2021-11-11 14:55:00Z
    created: 2021-11-11 14:54:43Z
    ---
    Page content
    ```
  * `PanDocMarkdownFormat` : choose the markdown syntax to use among [those supported by pandoc](https://pandoc.org/MANUAL.html#general-options). Use *GitHub flavor* by default.
  * `OneNoteLinksHandling` : 
    * KeepOriginal: Keep the original OneNote link format (onenote://...)
    * ConvertToMarkdown: Convert OneNote links to markdown format "[text](url)"
    * ConvertToWikilink (default): Convert OneNote links to wikilink format "[[Page Title|Display Text]]"
    * Remove: Remove all OneNote links
  * `UseHtmlStyling` : disable if your markdown editor do not supports HTML.

## Joplin Raw Directory

* Import format of the  [Joplin](https://github.com/laurent22/joplin) app
* More details [available here](/doc/migration-to-joplin.md)

# Features and limitations

| Export format: | Markdown | Joplin |
| --- | --- | --- |
| Hierarchy of sections | ✅ Folder hierarchy | ✅ Notebook hierarchy |
| Page ordering inside a section | 🔴 Ordering based on md filename | ✅ Order preserved |
| Page hierarchy | ✅ Page prefix or folder prefix | ✅ |
| Notebook internal link (onenote://) | ✅ * | ✅ *

\* Cross-notebooks links and links to sections are removed
___
___

| All formats : |  |
| --- | --- |
| Attachment  | ✅ |
| Image  | ✅ |
| Simple table  | ✅ as markdown |
| Complex table  | ✅ as HTML* |
| Folded paragraphs | ✅ |
| Font color| ✅ as HTML* |
| Background color  | ✅ as HTML*  |
| Text tags (task, star...)  | ✅ Converted into emoticons |
| Drawing | 🟠 Flattened as image | 
| Password protected section | 🟠 Lost unless unlocked before export |
| Handwriting  | 🔴 Lost |

\* Support of the feature requires a mardown editor that supports HTML (like Joplin and Obsidian)

# FAQ

* An error `Unhandled exception. System.Runtime.InteropServices.COMException` occurs after starting the tool 
  * This error in usually caused by a problem specific to your computer installation. Consider :
    * Uninstall and reinstall Office
    * Export notebook from an other computer : [export your notebook](/doc/notebook-onepkg-export.md), import it on an other computer and run OneNoteMdExporter

* Some of my images are lost / broken during export
  * Try to enable the option `Download all files and images` from `File -> Options -> Sync`. Force synchronisation of your notebook and try again the export.

# Technical characteristics

* DotNet 10 self-contained console application
* Export pages as DocX files and translate them in Markdown using PanDoc
* Offline : no dependency to Microsoft cloud
* Rely on OneNote and Word apps (based on Interop APIs)
* Pre-processing stage of OneNote page XML structure
* Post-processing stage based on Regex to fix formatting issues

# Licence

Released under the GPL, version 3.

This software carries no warranty of any kind. Some data can be lost during the export process. I recommend to review your notes after export and keep a backup of your OneNote notebooks just in case.

## Pandoc licence terms

OneNote Md Exporter uses PanDoc universal markup converter.

Pandoc is available at https://github.com/jgm/pandoc

Pandoc is released under the following licence terms, full licence details can be found on the pandoc site.

> © 2006-2021 John MacFarlane (jgm@berkeley.edu). Released under the GPL, version 2 or greater. This software carries no warranty of any kind.


# Build sources

* Install DotNet 10 : https://dotnet.microsoft.com/download/dotnet/8.0
* Clone this repository
* Extract `pandoc.exe` from `pandoc-<Version>-windows-x86_64.zip` from `/src/OneNoteMdExporter/pandoc/` folder
* Build using Visual Studio 2019 or MSBUILD.exe (`dotnet build` do not currently support COMReference : https://aka.ms/msbuild/MSB4803) 

# Contribute

Bugs and feature requests can be reported under: https://github.com/alxnbl/onenote-md-exporter/issues.

You can contribute by adding an new feature or add translations. See [this page](/doc/contribute.md) for more details.

# Support

I developed this tool on a voluntary basis, but if you like my work you can [buy me a ☕](https://www.buymeacoffee.com/alxnbl).

# Related Projects

* The [OneMore add-in](https://onemoreaddin.com/) can import and export from/to Markdown.
