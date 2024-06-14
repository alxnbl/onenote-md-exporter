using alxnbl.OneNoteMdExporter.Helpers;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace alxnbl.OneNoteMdExporter.Models
{
    public class Page(Section parent) : Node(parent)
    {
        public int PageLevel { get; set; }

        public Page ParentPage { get; private set; }

        public void SetParentPage(Page parentPage)
        {
            ParentPage = parentPage;
            parentPage.ChildPages.Add(this);
        }

        public IList<Page> ChildPages { get; set; } = [];

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
        public string TitleWithNoInvalidChars(int maxLength)
            => Title.RemoveInvalidFileNameChars().TrimEnd().Left(maxLength); // TrimEnd -> Fix https://github.com/alxnbl/onenote-md-exporter/issues/93

        public IList<Attachement> Attachements { get; set; } = [];
        public IList<Attachement> ImageAttachements { get => Attachements.Where(a => a.Type == AttachementType.Image).ToList(); }
        public IList<Attachement> FileAttachements { get => Attachements.Where(a => a.Type == AttachementType.File).ToList(); }

        public string Author { get; internal set; }

        /// <summary>
        /// Override page md file path in case of multiple page with the same name
        /// </summary>
        public string OverridePageFilePath { get; set; }

        /// <summary>
        /// Override page md OneNote ID in case of XML content changed due page preprocessing so we need to store it at temporary OneNote notebook
        /// </summary>
        public string OverrideOneNoteId { get; set; }

        public string GetPageFileRelativePath(int pageTitleMaxLength)
        {
            return Path.Combine(Parent.GetPath(pageTitleMaxLength), TitleWithNoInvalidChars(pageTitleMaxLength));
        }
    }
}
