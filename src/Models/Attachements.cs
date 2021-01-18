namespace alxnbl.OneNoteMdExporter.Models
{
    public class Attachements
    {
        public string Id { get; set; }

        public AttachementType Type {get; set;}

        public string PanDocFilePath { get; set; }

        public string ExportFilePath { get; set; }

        public string FileName { get; set; }

        public Page Parent { get; set; }

        public Attachements(Page parent)
        {
            Parent = parent;
        }
    }
}
