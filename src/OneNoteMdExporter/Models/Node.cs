using alxnbl.OneNoteMdExporter.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace alxnbl.OneNoteMdExporter.Models
{
    public class Node(Node parent)
    {
        public string Title { get; set; }

        public Node Parent { get; internal set; } = parent;

        public List<Node> Childs { get; set; } = [];

        public string Id { get; } = Guid.NewGuid().ToString().Replace("-", string.Empty);

        public string OneNoteId { get; set; }

        public DateTime CreationDate { get; set; }

        public DateTime LastModificationDate { get; set; }

        public string OneNotePath { get; set; }

        public void ReplaceParent(Node newParentNode)
        {
            Parent.Childs.Remove(this);
            Parent = newParentNode;
            newParentNode.Childs.Add(this);
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
                return [this];
        }

        public string GetPath(int pageTitleMaxLength)
        {
            if (Parent == null)
                return Title.RemoveInvalidFileNameChars().Left(pageTitleMaxLength);
            else
                return Path.Combine(Parent.GetPath(pageTitleMaxLength), Title.RemoveInvalidFileNameChars().Left(pageTitleMaxLength));
        }

        public int GetLevel()
        {
            if (Parent == null)
                return 0;
            else
                return Parent.GetLevel() + 1;
        }

        private Node GetNotebookRootnode()
        {
            if (Parent == null)
                return this;
            else
                return Parent.GetNotebook();
        }

        public Notebook GetNotebook()
            => GetNotebookRootnode() is Notebook notebook ? notebook : null;

        public string GetNotebookPath()
        {
            if (Parent == null)
                return Title.RemoveInvalidFileNameChars();
            else
                return Parent.GetNotebookPath();
        }
    }
}
