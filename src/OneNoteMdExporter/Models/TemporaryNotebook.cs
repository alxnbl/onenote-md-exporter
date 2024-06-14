using alxnbl.OneNoteMdExporter.Infrastructure;
using Microsoft.Office.Interop.OneNote;
using Microsoft.VisualBasic.FileIO;
using Serilog;
using System;
using System.IO;
using System.Xml.Linq;

namespace alxnbl.OneNoteMdExporter.Models
{
    /// <summary>
    /// TemporaryNotebook used to temporary store XML OneNote pages at temporary section for pre-processing such as:
    /// Sections unfold, Convert OneNote tags to #hash-tags, Keep checkboxes, etc.
    /// Reference - https://github.com/idvorkin/onom/blob/master/OneNoteObjectModelTests/TemporaryNoteBookHelper.cs
    /// </summary>
    public class TemporaryNotebook : Notebook
    {
        public static new string Title { get; } = "TempExporterNotebook";

        private static new string OneNoteId { get; set; }

        private static new string OneNotePath { get; set; }

        private static string SectionOneNoteId { get; set; }

        /// <summary>
        /// Create page clone at temporary notebook
        /// Reference - https://github.com/idvorkin/onom/blob/master/OneNoteObjectModel/OneNoteApplication.cs#L160
        /// </summary>
        /// <param name="xmlPageContent">Page to clone</param>
        /// <returns>Temporary OneNote ID of cloned page</returns>
        public static string ClonePage(XElement xmlPageContent)
        {
            Log.Debug($"Start cloning page {xmlPageContent.Attribute("ID")}");

            var oneNoteApp = OneNoteApp.Instance;

            if (OneNoteId == null)
            {
                OneNotePath = Path.GetFullPath(@$"{Localizer.GetString("ExportFolder")}\{Title}");

                // Create Temporary notebook if not exists and open at OneNote
                oneNoteApp.OpenHierarchy(OneNotePath, null, out string tempNotebookId, CreateFileType.cftNotebook);
                OneNoteId = tempNotebookId;

                // Create new Section at Temporary notebook if not exists and open at OneNote
                var sectionName = $"{DateTime.Now:yyyy-MM-dd HH-mm}.one";
                oneNoteApp.OpenHierarchy(sectionName, OneNoteId, out string tempSectionId, CreateFileType.cftSection);
                SectionOneNoteId = tempSectionId;

                Log.Debug(@$"Created temporary section: {OneNotePath}\{sectionName}");
            }

            // When cloning a page need to remove all object ID's as OneNote needs to write them out
            foreach (var xmlElement in xmlPageContent.Descendants())
            {
                xmlElement.Attribute("objectID")?.Remove();
            }

            // Create the new Page and write it to OneNote
            oneNoteApp.CreateNewPage(SectionOneNoteId, out string tempPageId);

            // Update the XML as it still points to the page to clone
            xmlPageContent.Attribute("ID").Value = tempPageId;

            // Replace created temp page with our page content
            oneNoteApp.UpdatePageContent(xmlPageContent.ToString(SaveOptions.DisableFormatting));

            Log.Debug($"Page successfully cloned to the temp page {tempPageId}");

            return tempPageId;
        }

        /// <summary>
        /// Close temporary notebook and move its folder to Recycle Bin
        /// </summary>
        public static void CleanUp()
        {
            if (OneNoteId != null)
            {
                OneNoteApp.Instance.CloseNotebook(OneNoteId, true);
                FileSystem.DeleteDirectory(OneNotePath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                
                OneNoteId = null;
            }
        }
    }
}
