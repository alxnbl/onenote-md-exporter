using alxnbl.OneNoteMdExporter.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace alxnbl.OneNoteMdExporter.Models
{
    public class Node
    {
        public string Title { get; set; }

        public Node Parent { get; }

        public List<Node> Childs { get; set; } = new List<Node>();

        public string Id { get; }

        public string OneNoteId { get; set; }

        public DateTime CreationDate { get; set; }

        public DateTime LastModificationDate { get; set; }

        public string OneNotePath { get; set; }

        public Node(Node parent)
        {
            Parent = parent;
            Id = Guid.NewGuid().ToString().Replace("-", string.Empty);
        }

        public List<Node> GetChilds()
        {
            if (Childs.Count > 0)
            {
                var res = Childs.SelectMany(n => n.GetChilds()).ToList();
                res.Add(this);
                return res;
            }
            else
                return new List<Node> { this };
        }
        public string GetPath()
        {
            if (Parent == null)
                return Title;
            else
                return Path.Combine(Parent.GetPath(), Title.RemoveInvalidFileNameChars());
        }

        public int GetLevel()
        {
            if (Parent == null)
                return 0;
            else
                return Parent.GetLevel() + 1;
        }

        public string GetNotebookName()
        {
            if (Parent == null)
                return Title;
            else
                return Parent.GetNotebookName();
        }


    }
}