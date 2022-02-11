# Introduction
One of the objectives of this tool it to offer the most simple and lossless solution to migrate from OneNote to Joplin. Indeed, the alternative way to migrate is to use the Evernote export format (OneNote-> ENEX -> Joplin) which cause lost of sections and page hierarchy.

# OneNote Md Exporter vs Evernote migration comparison

| | OneNote Md Exporter | EverNote Export (ENEX) |
| --- | --- | --- |
| Formatted content | ✅ | ✅ |
| **Hierarchy of sections** | ✅ Sub-Notebooks | 🟠 Flattened as Tag |
| **Page order inside a section** | ✅ | 🔴 All pages part of a single Notebook |
| **Page hierarchy (level)** | ✅ Sub-Notebooks (\*1)<br />✅ Prefix page title (\*2)| 🔴 |
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

(\*1) : setting `ProcessingOfPageHierarchy = HiearchyAsFolderTree` (default)<br />
(\*2) : setting `ProcessingOfPageHierarchy = HiearchyAsPageTitlePrefix`

# User guide

1. Export your notebook as described in [Readme](/README.md) by choosing `Joplin Raw Directory` format
2. Note the location of the export folder
3. Launch Joplin, clic File > Import > "RAW - Joplin Export Directory" and select the export folder location