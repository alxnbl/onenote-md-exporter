using Microsoft.Office.Interop.OneNote;
using alxnbl.OneNoteMdExporter.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace alxnbl.OneNoteMdExporter.Helpers
{
    public static class OneNoteExtensions
    {
        public static IList<Notebook> GetNotebooks(this Application oneNoteApp)
        {
            oneNoteApp.GetHierarchy(null, HierarchyScope.hsNotebooks, out string xmlStr);

            var xmlNotebooks = XDocument.Parse(xmlStr).Root;
            var ns = xmlNotebooks.Name.Namespace;

            return xmlNotebooks.Elements(ns + "Notebook").Select(element =>
            {
                return element == null ? null : new Notebook
                {
                    OneNoteId = element.Attribute("ID")?.Value,
                    Title = element.Attribute("name")?.Value,
                    OneNotePath = element.Attribute("path")?.Value,
                    CreationDate = element.GetDate("lastModifiedTime"),
                    LastModificationDate = element.GetDate("lastModifiedTime")
                };
            }).ToList();
        }

        public static DateTime GetDate(this XElement element, string attributeName)
        {
            DateTime dt;
            if (DateTime.TryParse(element.Attribute(attributeName)?.Value, out var datetimeRes))
                dt = datetimeRes;
            else dt = DateTime.Now;
            return dt;
        }

        public static Section GetSection(this XElement element, Node parentNode)
        {
            var section = new Section(parentNode)
            {
                Title = element.Attribute("name")?.Value,
                OneNoteId = element.Attribute("ID")?.Value,
                OneNotePath = element.Attribute("path")?.Value,
                CreationDate = element.GetDate("lastModifiedTime"),
                LastModificationDate = element.GetDate("lastModifiedTime")
            };

            switch (element.Name.LocalName)
            {
                case "SectionGroup":
                    section.IsSectionGroup = true;
                    break;
                case "Section":
                    section.IsSectionGroup = false;
                    break;
                default:
                    throw new NotImplementedException();

            }

            return section;
        }

        public static Page GetPage(this XElement element, Section parentSection)
        {
            var pageId = element.Attribute("ID")?.Value;

            DateTime.TryParse(element.Attribute("dateTime").Value, out var creationDate);
            DateTime.TryParse(element.Attribute("lastModifiedTime").Value, out var lastModificationDate);
            int.TryParse(element.Attribute("pageLevel").Value, out var pageLevel);

            var title = element.Attribute("name").Value;

            // Limit title max size, especilay for notes with not title where the 1st paragraphe is returned as a title
            if (title.Length > 50)
                title = new string(title.Take(50).ToArray()) + "...";

            var page = new Page(parentSection)
            {
                OneNoteId = pageId,
                Title = title,
                PageLevel = pageLevel,
                CreationDate = creationDate,
                LastModificationDate = lastModificationDate
            };

            return page;
        }


        /// <summary>
        /// Browse notebook tree and fill Notebook (except pages and attachments)
        /// </summary>
        /// <param name="notebook"></param>
        /// <returns></returns>
        public static void FillNodebookTree(this Application oneNoteApp, Notebook notebook)
        {
            oneNoteApp.GetHierarchy(notebook.OneNoteId, HierarchyScope.hsSections, out var xmlStr);
            var xmlSections = XDocument.Parse(xmlStr).Elements().First();

            FillNodebookSections(notebook, xmlSections);
        }


        private static void FillNodebookSections(Node node, XElement xmlNode)
        {
            List<XElement> xmlChildSections =
                xmlNode.Elements()
                    .Where(e => e.Attribute("isRecycleBin") == null && e.Attribute("isDeletedPages") == null)
                    .ToList();

            foreach (XElement xmlChildSection in xmlChildSections)
            {
                Section childSection = xmlChildSection.GetSection(node);
                node.Childs.Add(childSection);

                if (childSection.IsSectionGroup)
                {
                    // If node is not a section, continue to browse section tree
                    FillNodebookSections(childSection, xmlChildSection);
                }
            }
        }


        /// <summary>
        /// Get pages of a OneNote section and attach them in the notebook tree
        /// </summary>
        /// <param name="section"></param>
        /// <returns>List of pages</returns>
        public static IList<Page> FillSectionPages(this Application oneNoteApp, Section section)
        {
            oneNoteApp.GetHierarchy(section.OneNoteId, HierarchyScope.hsPages, out var xmlStr);
            var xmlSection = XDocument.Parse(xmlStr).Root;
            var ns = xmlSection.Name.Namespace;

            var xmlPages = xmlSection.Descendants(ns + "Page")
                .Where(e => e.Attribute("isRecycleBin") == null && e.Attribute("isDeletedPages") == null);

            var childPages = xmlPages.Select(xmlP => xmlP.GetPage(section)).ToList();

            int position = 0;
            foreach (var page in childPages)
            {
                position++;
                page.PageSectionOrder = position;

                oneNoteApp.GetPageContent(page.OneNoteId, out var xmlPageContentStr, PageInfo.piAll);


                // Alternative : return page content without binaries
                //oneNoteApp.GetHierarchy(page.OneNoteId, HierarchyScope.hsChildren, out var xmlAttach);

                var xmlPageContent = XDocument.Parse(xmlPageContentStr).Root;

                var ns2 = xmlPageContent.Name.Namespace;
                var pageTitleOE = xmlPageContent.Descendants(ns + "Title")?.FirstOrDefault()?.Descendants(ns + "OE")?.FirstOrDefault();
                if (pageTitleOE != null)
                {
                    page.Author = pageTitleOE.Attribute("author")?.Value ?? "unknown";
                }

                section.Childs.Add(page);

                ProcessPageAttachments(ns, page, xmlPageContent);
            }


            return childPages;
        }

        private static void ProcessPageAttachments(XNamespace ns, Page page, XElement xmlPageContent)
        {
            foreach (var xmlInsertedFile in xmlPageContent.Descendants(ns + "InsertedFile"))
            {
                var fileAttachment = new Attachement(page)
                {
                    ActualSourceFilePath = xmlInsertedFile.Attribute("pathCache")?.Value,
                    OriginalUserFilePath = xmlInsertedFile.Attribute("pathSource")?.Value,
                    OneNotePreferredFileName = xmlInsertedFile.Attribute("preferredName")?.Value,
                    Type = AttachementType.File
                };

                if (fileAttachment.ActualSourceFilePath != null)
                {
                    page.Attachements.Add(fileAttachment);
                }
            }
        }
    }
}
