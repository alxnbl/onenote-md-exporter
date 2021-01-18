using alxnbl.OneNoteMdExporter.Helpers;
using System.Collections.Generic;
using System.IO;

namespace alxnbl.OneNoteMdExporter.Models
{
    public class Page : Node
    {
        public int PageLevel { get; set; }

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

        public IList<Attachements> Attachements { get; set; } = new List<Attachements>();

        public Page(Section parent) : base(parent)
        {
        }

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
