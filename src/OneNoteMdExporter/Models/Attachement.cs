using System;
using System.IO;

namespace alxnbl.OneNoteMdExporter.Models
{
    public class Attachement
    {
        public string Id { get; set; }

        public AttachementType Type {get; set;}

        public string FriendlyFileName { 
            get { return OneNotePreferredFileName ?? Path.GetFileName(OriginalUserFilePath); }
        }
        /// <summary>
        /// The Friendly FileNama returned by OneNote
        /// </summary>
        public string OneNotePreferredFileName { get; set; }

        /// <summary>
        /// Use as unique identifier of image attachments extracted from the docx file
        /// </summary>
        public string PanDocFilePath { get; set; }

        /// <summary>
        /// File path of file stored in OneNote cache (C:\Users\<User>\AppData\Local\Microsoft\OneNote\<Version>\cache\<file>.bin)
        /// </summary>
        public string ActualSourceFilePath { get; set; }

        /// <summary>
        /// File path of the original file imported by user into OneNote
        /// </summary>
        public string OriginalUserFilePath { get; set; }

        /// <summary>
        /// Not null when name of the attachment file in the export folder need to be overriden
        /// </summary>
        public string OverrideExportFilePath { get; set; }

        public Page ParentPage { get; set; }

        public Attachement(Page parent)
        {
            ParentPage = parent;
            Id = Guid.NewGuid().ToString().Replace("-", string.Empty);
        }
    }
}
