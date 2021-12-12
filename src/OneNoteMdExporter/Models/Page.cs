using alxnbl.OneNoteMdExporter.Helpers;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace alxnbl.OneNoteMdExporter.Models
{
    public class Page : Node
    {
        public int PageLevel { get; set; }

        public Page ParentPage { get; set; }

        /// <summary>
        /// Ordering of the page inside the Section
        /// </summary>
        public int PageSectionOrder { get; set; }

        public string TitleWithPageLevelTabulation { get
            {
                var level = "";
                for (int i = 1; i < PageLevel; i++) level += "--";
                return level + (level.Length > 0 ? " " : "") + Title;
            } 
        }
        public string TitleWithNoInvalidChars { get => Title.RemoveInvalidFileNameChars(); }

        public IList<Attachement> Attachements { get; set; } = new List<Attachement>();
        public IList<Attachement> ImageAttachements { get => Attachements.Where(a => a.Type == AttachementType.Image).ToList(); }
        public IList<Attachement> FileAttachements { get => Attachements.Where(a => a.Type == AttachementType.File).ToList(); }

        public string Author { get; internal set; }

        public Page(Section parent) : base(parent)
        {
        }

        /// <summary>
        /// Override page md file path in case of multiple page with the same name
        /// </summary>
        public string OverridePageFilePath { get; set; }

        public string GetPageFileRelativePath()
        {
            return Path.Combine(Parent.GetPath(), TitleWithNoInvalidChars.AddPrefixLevel(PageLevel));
        }

        public string GetPageFolderRelativePath()
        {
            return Parent.GetPath();
        }
    }
}
