using System;

namespace alxnbl.OneNoteMdExporter.Models
{
    public class Attachements
    {
        public string Id { get; set; }

        public AttachementType Type {get; set;}

        public string FriendlyFileName { get; set; }

        public string PanDocFilePath { get; set; }

        public string ExportFilePath { get; set; }

        /// <summary>
        /// File path of file stored in OneNote cache (C:\Users\<User>\AppData\Local\Microsoft\OneNote\<Version>\cache\<file>.bin)
        /// </summary>
        public string OneNoteFilePath { get; set; }

        /// <summary>
        /// File path of the original file imported by user into OneNote
        /// </summary>
        public string OneNoteFileSourceFilePath { get; set; }

        public string ExportFileName { get; set; }

        public Page Parent { get; set; }

        public Attachements(Page parent)
        {
            Parent = parent;
            Id = Guid.NewGuid().ToString().Replace("-", string.Empty);
        }
    }
}
